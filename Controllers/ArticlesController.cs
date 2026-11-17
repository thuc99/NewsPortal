using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;
using Ganss.Xss;
using System.Text.RegularExpressions;

namespace NewsPortal.Controllers
{
    
    public class ArticlesController : Controller
    {
        private string SanitizeArticleContent(string content)
        {
            var sanitizer = new HtmlSanitizer();

            // Cho phép giữ định dạng chữ, căn lề và màu sắc từ TinyMCE
            sanitizer.AllowedAttributes.Add("style");

            return sanitizer.Sanitize(content ?? string.Empty);
        }

        private string GetPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            string text = Regex.Replace(html, "<[^>]*>", " ");
            text = HttpUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
        private readonly NewsPortalDBEntities db = new NewsPortalDBEntities();

        private static readonly string[] ValidStatuses = new[]
        {
            "Draft", "Pending", "Published", "Rejected", "Archived"
        };

        private static bool _statusConstraintChecked = false;
        private static readonly object _constraintLock = new object();

        private void EnsureWorkflowStatusConstraint()
        {
            if (_statusConstraintChecked) return;

            lock (_constraintLock)
            {
                if (_statusConstraintChecked) return;
                try
                {
                    const string sql = @"
IF OBJECT_ID(N'dbo.Articles', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.check_constraints
       WHERE name = N'CK_Articles_Status_Workflow'
         AND parent_object_id = OBJECT_ID(N'dbo.Articles')
   )
BEGIN
    DECLARE @dropSql NVARCHAR(MAX) = N'';
    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Articles DROP CONSTRAINT [' + REPLACE(cc.name, N']', N']]') + N'];'
    FROM sys.check_constraints cc
    LEFT JOIN sys.columns col
        ON col.object_id = cc.parent_object_id
       AND col.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Articles')
      AND (col.name = N'Status' OR (cc.parent_column_id = 0 AND cc.definition LIKE N'%Draft%' AND cc.definition LIKE N'%Published%'));

    IF LEN(@dropSql) > 0
        EXEC sp_executesql @dropSql;

    UPDATE dbo.Articles
    SET Status = N'Draft'
    WHERE Status IS NULL
       OR Status NOT IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived');

    ALTER TABLE dbo.Articles WITH CHECK
    ADD CONSTRAINT [CK_Articles_Status_Workflow]
    CHECK ([Status] IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived'));
END";
                    db.Database.ExecuteSqlCommand(sql);
                    _statusConstraintChecked = true;
                }
                catch
                {
                    // Giữ an toàn nếu tài khoản SQL bị giới hạn quyền DDL
                }
            }
        }

        private bool IsAdminUser()
        {
            if (Session["Admin"] != null)
                return true;

            string username = Convert.ToString(Session["User"]);
            if (!string.IsNullOrWhiteSpace(username))
            {
                var dbUser = db.Users.FirstOrDefault(u => u.Username == username);
                if (dbUser != null)
                {
                    Session["Role"] = dbUser.Role;
                    return string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
                }
            }

            return string.Equals(Convert.ToString(Session["Role"]), "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanManageArticles()
        {
            if (Session["Admin"] != null)
                return true;

            string username = Convert.ToString(Session["User"]);
            if (string.IsNullOrWhiteSpace(username))
                return false;

            var dbUser = db.Users.FirstOrDefault(u => u.Username == username);
            if (dbUser != null)
            {
                Session["Role"] = dbUser.Role;
                return string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(dbUser.Role, "Editor", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(Convert.ToString(Session["Role"]), "Editor", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAdminArticleRequest()
        {
            return (Request.AppRelativeCurrentExecutionFilePath ?? string.Empty)
                .StartsWith("~/Admin/Article", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult RedirectToArticleManagement()
        {
            return RedirectToRoute("AdminArticles", new { action = "Index" });
        }

        private ActionResult CheckArticlePermission(string actionLabel)
        {
            if (!CanManageArticles())
            {
                if (Session["User"] != null)
                {
                    TempData["Error"] = "Bạn không có quyền " + actionLabel + " bài viết. Chức năng này chỉ dành cho Admin và Editor.";
                    return RedirectToAction("Index", "Articles");
                }

                TempData["Error"] = "Vui lòng đăng nhập tài khoản Admin hoặc Editor trước khi truy cập chức năng này.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsAdminArticleRequest())
                return RedirectToArticleManagement();

            return null;
        }

        private ActionResult CheckAdminArticlePage()
        {
            if (!IsAdminArticleRequest())
                return null;

            return CheckArticlePermission("quản lý");
        }

        private void ValidateArticleInput(
      Article article,
      HttpPostedFileBase imageFile,
      bool checkStatus = false,
      bool requireImage = false)
        {
            if (article == null)
            {
                ModelState.AddModelError("", "Dữ liệu bài viết không hợp lệ.");
                return;
            }

            article.Title = (article.Title ?? string.Empty).Trim();
            article.Content = (article.Content ?? string.Empty).Trim();

            ModelState.Remove("Title");
            ModelState.Remove("Content");
            ModelState.Remove("Status");

            if (string.IsNullOrWhiteSpace(article.Title))
                ModelState.AddModelError("Title", "Tiêu đề không được để trống.");
            else if (article.Title.Length < 10 || article.Title.Length > 250)
                ModelState.AddModelError("Title", "Tiêu đề phải từ 10 đến 250 ký tự.");

            string plainContent = GetPlainText(article.Content);

            if (string.IsNullOrWhiteSpace(plainContent))
                ModelState.AddModelError("Content", "Nội dung bài viết không được để trống.");
            else if (plainContent.Length < 50 || plainContent.Length > 20000)
                ModelState.AddModelError("Content", "Nội dung bài viết phải từ 50 đến 20000 ký tự.");

            if (checkStatus &&
                !string.IsNullOrEmpty(article.Status) &&
                !ValidStatuses.Contains(article.Status))
            {
                ModelState.AddModelError("Status", "Trạng thái bài viết không hợp lệ.");
            }

            // Bắt buộc ảnh khi tạo bài viết mới
            if (requireImage &&
                (imageFile == null || imageFile.ContentLength <= 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ảnh đại diện cho bài viết.");
                return;
            }

            // Kiểm tra ảnh nếu người dùng tải lên
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var ext = (Path.GetExtension(imageFile.FileName) ?? string.Empty)
                    .ToLowerInvariant();

                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(
                        "",
                        "Chỉ chấp nhận định dạng ảnh .jpg, .jpeg, .png, .gif, .webp.");
                }
                else if (imageFile.ContentLength > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "",
                        "Dung lượng ảnh tối đa là 2MB.");
                }
            }
        }

        public ActionResult Index(string searchString, int? categoryId, int? authorId,
            string status, DateTime? fromDate, DateTime? toDate, int? page)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckAdminArticlePage();
            if (denied != null) return denied;

            const int pageSize = 8;
            int requestedPage = page ?? 1;
            bool isAdminManagementRequest = IsAdminArticleRequest();
            bool canManageArticles = isAdminManagementRequest && CanManageArticles();
            bool isAdminUser = canManageArticles && IsAdminUser();

            var query = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .AsQueryable();

            if (!canManageArticles)
            {
                query = query.Where(a => a.Status == "Published");
            }
            else
            {
                ViewBag.CountAll = db.Articles.Count();
                ViewBag.CountDraft = db.Articles.Count(a => a.Status == "Draft");
                ViewBag.CountPending = db.Articles.Count(a => a.Status == "Pending");
                ViewBag.CountPublished = db.Articles.Count(a => a.Status == "Published");
                ViewBag.CountRejected = db.Articles.Count(a => a.Status == "Rejected");
                ViewBag.CountArchived = db.Articles.Count(a => a.Status == "Archived");
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                if (searchString.Length > 200)
                {
                    searchString = searchString.Substring(0, 200);
                }

                query = query.Where(a =>
                    a.Title.Contains(searchString) ||
                    a.Summary.Contains(searchString) ||
                    a.Content.Contains(searchString));
            }
            else
            {
                searchString = null;
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                var tempDate = fromDate;
                fromDate = toDate;
                toDate = tempDate;
                ViewBag.WarningMessage = "Ngày bắt đầu lớn hơn ngày kết thúc nên hệ thống đã tự động điều chỉnh lại khoảng ngày.";
            }

            if (categoryId.HasValue)
                query = query.Where(a => a.CategoryID == categoryId.Value);

            if (authorId.HasValue)
                query = query.Where(a => a.AuthorID == authorId.Value);

            if (canManageArticles && !string.IsNullOrWhiteSpace(status) && ValidStatuses.Contains(status))
                query = query.Where(a => a.Status == status);

            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedDate >= fromDate.Value);

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(a => a.CreatedDate < endDate);
            }

            var totalArticles = query.Count();
            var totalPages = Math.Max((int)Math.Ceiling((double)totalArticles / pageSize), 1);

            int currentPage = requestedPage;
            if (requestedPage < 1)
            {
                currentPage = 1;
                ViewBag.WarningMessage = "Số trang không hợp lệ, hệ thống đã chuyển về trang đầu tiên.";
            }
            else if (requestedPage > totalPages)
            {
                currentPage = totalPages;
                if (totalArticles > 0)
                {
                    ViewBag.WarningMessage = "Số trang bạn nhập (" + requestedPage + ") vượt quá tổng số trang hiện có (" + totalPages + " trang), hệ thống đã chuyển về trang cuối.";
                }
                else if (requestedPage > 1)
                {
                    ViewBag.WarningMessage = "Trang số " + requestedPage + " không tồn tại.";
                }
            }

            var articles = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(ToArticleViewModel)
                .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.AuthorId = authorId;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.Categories = db.Categories.OrderBy(c => c.CategoryName).ToList();
            ViewBag.Authors = db.Authors.OrderBy(a => a.AuthorName).ToList();
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalArticles = totalArticles;
            ViewBag.CanManageArticles = canManageArticles;
            ViewBag.IsAdminManagement = isAdminManagementRequest;
            ViewBag.IsAdminUser = isAdminUser;

            if (!string.IsNullOrWhiteSpace(searchString) && totalArticles == 0)
            {
                ViewBag.SearchMessage = "Không tìm thấy bài viết nào phù hợp với từ khóa \"" + searchString.Trim() + "\".";
            }

            return View(articles);
        }

        public ActionResult Details(int? id)
        {
            SyncViewAndSaved(id);
            return LoadArticleDetails(id, false);
        }

        [ActionName("View")]
        public ActionResult ViewArticle(int? id)
        {
            SyncViewAndSaved(id);
            return LoadArticleDetails(id, true);
        }

        private void SyncViewAndSaved(int? id)
        {
            if (!id.HasValue || id.Value <= 0) return;

            int artId = id.Value;
            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;
            if (!string.IsNullOrEmpty(currentUsername) && db.Articles.Any(a => a.ArticleID == artId))
            {
                var viewer = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (viewer != null)
                {
                    var history = db.ArticleViewHistory.FirstOrDefault(h => h.UserId == viewer.UserId && h.ArticleID == artId);
                    if (history != null)
                    {
                        history.LastViewedAt = DateTime.Now;
                    }
                    else
                    {
                        db.ArticleViewHistory.Add(new ArticleViewHistory
                        {
                            UserId = viewer.UserId,
                            ArticleID = artId,
                            LastViewedAt = DateTime.Now
                        });
                    }
                    db.SaveChanges();

                    ViewBag.IsSavedArticle = db.SavedArticles.Any(s => s.UserId == viewer.UserId && s.ArticleID == artId);
                }
            }
        }

        private ActionResult LoadArticleDetails(int? id, bool canComment)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckAdminArticlePage();
            if (denied != null) return denied;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết không hợp lệ.";
                return IsAdminArticleRequest() ? RedirectToArticleManagement() : RedirectToAction("Index");
            }

            int articleId = id.Value;
            var article = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .FirstOrDefault(a => a.ArticleID == articleId);

            bool hasManagePermission = CanManageArticles();
            bool canManageArticles = IsAdminArticleRequest() && hasManagePermission;
            if (article == null || (article.Status != "Published" && !hasManagePermission))
            {
                TempData["Error"] = "Bài viết bạn tìm không tồn tại hoặc chưa được xuất bản.";
                return IsAdminArticleRequest() ? RedirectToArticleManagement() : RedirectToAction("Index");
            }

            bool isPublished = article.Status == "Published";
            if (canComment && isPublished && !HasViewedRecently(articleId))
            {
                article.ViewCount++;
                db.SaveChanges();
                Response.Cookies.Add(new HttpCookie("NewsPortalViewed_" + articleId, "1")
                {
                    Expires = DateTime.Now.AddHours(1),
                    HttpOnly = true
                });
            }

            ViewBag.Comments = db.Comments
              .Where(c => c.ArticleID == articleId && c.Status == "Approved")
              .OrderByDescending(c => c.CreatedDate)
              .ToList();
            ViewBag.AuthorId = article.AuthorID;
            ViewBag.AuthorName = article.Authors == null ? null : article.Authors.AuthorName;
            ViewBag.RatingCount = db.ArticleRatings.Count(r => r.ArticleID == articleId);
            ViewBag.CanManageArticles = canManageArticles;
            ViewBag.IsAdminManagement = IsAdminArticleRequest();
            ViewBag.IsAdminUser = IsAdminUser();
            ViewBag.AverageRating = db.ArticleRatings
                .Where(r => r.ArticleID == articleId)
                .Select(r => (double?)r.Rating)
                .Average() ?? 0;
            ViewBag.IsFavorite = GetFavoriteIds().Contains(articleId);
            ViewBag.CanComment = canComment && isPublished;

            var recommendations = db.Articles
                .Include(a => a.Categories)
                .Where(a => a.ArticleID != articleId && a.Status == "Published" && a.CategoryID == article.CategoryID)
                .OrderByDescending(a => a.CreatedDate)
                .Take(4)
                .ToList();

            if (recommendations.Count < 4)
            {
                var existingIds = recommendations.Select(a => a.ArticleID).Concat(new[] { articleId }).ToList();
                recommendations.AddRange(db.Articles
                    .Include(a => a.Categories)
                    .Where(a => a.Status == "Published" && !existingIds.Contains(a.ArticleID))
                    .OrderByDescending(a => a.CreatedDate)
                    .Take(4 - recommendations.Count)
                    .ToList());
            }

            ViewBag.RecommendedArticles = recommendations;

            return View("Details", ToArticleViewModel(article));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddComment(int? articleId, string commenterName, string content)
        {
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value && a.Status == "Published"))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc chưa được xuất bản.";
                return RedirectToAction("Index");
            }

            commenterName = (commenterName ?? string.Empty).Trim();
            content = (content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(commenterName))
            {
                TempData["CommentError"] = "Vui lòng nhập tên của bạn.";
                return RedirectToAction("View", new { id = articleId.Value });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["CommentError"] = "Vui lòng nhập nội dung bình luận.";
                return RedirectToAction("View", new { id = articleId.Value });
            }

            if (commenterName.Length > 100 || content.Length > 1000)
            {
                TempData["CommentError"] = "Tên tối đa 100 ký tự và bình luận tối đa 1000 ký tự.";
                return RedirectToAction("View", new { id = articleId.Value });
            }

            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;
            var viewer = !string.IsNullOrEmpty(currentUsername)
                ? db.Users.FirstOrDefault(u => u.Username == currentUsername)
                : null;

            db.Comments.Add(new Comments
            {
                ArticleID = articleId.Value,
                UserId = viewer != null ? (int?)viewer.UserId : null,
                CommenterName = commenterName,
                Content = content,
                CreatedDate = DateTime.Now
            });
            db.SaveChanges();

            TempData["CommentSuccess"] = "Bình luận của bạn đã được đăng.";
            return RedirectToAction("View", new { id = articleId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Rate(int? articleId, int? rating)
        {
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value && a.Status == "Published"))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc chưa được xuất bản.";
                return RedirectToAction("Index");
            }

            if (!rating.HasValue || rating.Value < 1 || rating.Value > 5)
            {
                TempData["RatingError"] = "Vui lòng chọn mức đánh giá từ 1 đến 5 sao.";
                return RedirectToAction("View", new { id = articleId.Value });
            }

            db.ArticleRatings.Add(new ArticleRatings
            {
                ArticleID = articleId.Value,
                RaterName = "Ẩn danh",
                Rating = (byte)rating.Value,
                CreatedDate = DateTime.Now
            });
            db.SaveChanges();

            TempData["RatingSuccess"] = "Cảm ơn bạn đã đánh giá bài viết.";
            return RedirectToAction("View", new { id = articleId.Value });
        }

        private bool HasViewedRecently(int articleId)
        {
            return Request.Cookies["NewsPortalViewed_" + articleId] != null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleFavorite(int? articleId)
        {
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value && a.Status == "Published"))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc chưa được xuất bản.";
                return RedirectToAction("Index");
            }

            int artId = articleId.Value;

            // Giữ nguyên cookie của hệ thống cũ
            var favoriteIds = GetFavoriteIds();
            if (favoriteIds.Contains(artId))
                favoriteIds.Remove(artId);
            else
                favoriteIds.Add(artId);

            Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", favoriteIds))
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true
            });

            // Đồng bộ thêm vào bảng SavedArticles khi đã đăng nhập
            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;
            if (!string.IsNullOrEmpty(currentUsername))
            {
                var viewer = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (viewer != null)
                {
                    var existing = db.SavedArticles.FirstOrDefault(s => s.UserId == viewer.UserId && s.ArticleID == artId);
                    if (existing != null)
                    {
                        db.SavedArticles.Remove(existing);
                        TempData["FavoriteMessage"] = "Đã bỏ lưu bài viết.";
                    }
                    else
                    {
                        db.SavedArticles.Add(new SavedArticles
                        {
                            UserId = viewer.UserId,
                            ArticleID = artId,
                            SavedAt = DateTime.Now
                        });
                        TempData["FavoriteMessage"] = "Đã lưu bài viết vào Tin đã lưu.";
                    }
                    db.SaveChanges();
                }
            }
            else
            {
                TempData["FavoriteMessage"] = favoriteIds.Contains(artId) ? "Đã lưu bài viết yêu thích." : "Đã bỏ khỏi bài viết yêu thích.";
            }

            return RedirectToAction("View", new { id = artId });
        }

        private List<int> GetFavoriteIds()
        {
            var cookie = Request.Cookies["NewsPortalFavorites"];
            if (cookie == null || string.IsNullOrWhiteSpace(cookie.Value))
                return new List<int>();

            return cookie.Value.Split(',')
                .Select(value =>
                {
                    int id;
                    return int.TryParse(value, out id) ? (int?)id : null;
                })
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();
        }

        public ActionResult Favorites()
        {
            var favoriteIds = GetFavoriteIds();
            var articles = db.Articles
                .Include(a => a.Categories)
                .Where(a => favoriteIds.Contains(a.ArticleID) && a.Status == "Published")
                .OrderByDescending(a => a.CreatedDate)
                .ToList()
                .Select(ToArticleViewModel)
                .ToList();

            return View(articles);
        }

        public ActionResult Create()
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = IsAdminUser();
            return View(new Article());
        }

        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Article article, HttpPostedFileBase imageFile, string submitAction)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            bool isAdmin = IsAdminUser();
            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = isAdmin;

            if (article != null)
            {
                article.Content = SanitizeArticleContent(article.Content);
            }

            ValidateArticleInput(article, imageFile, false, true);

            if (!ModelState.IsValid)
                return View(article);

            var category = db.Categories.OrderBy(c => c.CategoryID).FirstOrDefault();
            var author = db.Authors.OrderBy(a => a.AuthorID).FirstOrDefault();
            if (category == null || author == null)
            {
                ModelState.AddModelError("", "Cần có danh mục và tác giả trước khi thêm bài viết.");
                return View(article);
            }

            string targetStatus;
            if (!isAdmin)
            {
                // Phóng viên (Editor): chỉ được lưu nháp (Draft) hoặc gửi Admin duyệt (Pending)
                targetStatus = string.Equals(submitAction, "Draft", StringComparison.OrdinalIgnoreCase)
                    ? "Draft"
                    : "Pending";
            }
            else
            {
                if (string.Equals(submitAction, "Draft", StringComparison.OrdinalIgnoreCase))
                    targetStatus = "Draft";
                else if (string.Equals(submitAction, "Pending", StringComparison.OrdinalIgnoreCase))
                    targetStatus = "Pending";
                else
                    targetStatus = "Published";
            }

            string imageUrl = SaveImage(imageFile);

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                ModelState.AddModelError("", "Không thể lưu ảnh đại diện. Vui lòng chọn lại ảnh.");
                return View(article);
            }

            db.Articles.Add(new Articles
            {
                CategoryID = category.CategoryID,
                AuthorID = author.AuthorID,
                Title = article.Title,
                Summary = CreateSummary(article.Content),
                Content = article.Content,
                CreatedDate = DateTime.Now,
                Status = targetStatus,
                ImageUrl = imageUrl,
                ViewCount = 0
            });
            db.SaveChanges();

            if (targetStatus == "Draft")
                TempData["Success"] = "Đã lưu bài viết dưới dạng Bản nháp (Draft).";
            else if (targetStatus == "Pending")
                TempData["Success"] = "Đã gửi bài viết chờ Admin phê duyệt (Pending).";
            else
                TempData["Success"] = "Đã xuất bản bài viết mới lên trang chủ (Published).";

            return RedirectToArticleManagement();
        }

        public ActionResult Edit(int? id)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            bool isAdmin = IsAdminUser();
            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = isAdmin;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết cần chỉnh sửa không hợp lệ.";
                return RedirectToArticleManagement();
            }

            var article = db.Articles.Find(id.Value);
            if (article == null)
            {
                TempData["Error"] = "Bài viết cần chỉnh sửa không tồn tại hoặc đã bị xóa.";
                return RedirectToArticleManagement();
            }

            if (!isAdmin && (article.Status == "Published" || article.Status == "Archived"))
            {
                TempData["Error"] = "Bài viết đã xuất bản hoặc lưu trữ, phóng viên không thể chỉnh sửa trực tiếp.";
                return RedirectToArticleManagement();
            }

            return View(ToArticleViewModel(article));
        }

        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Article article, HttpPostedFileBase imageFile, string submitAction)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            bool isAdmin = IsAdminUser();
            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = isAdmin;

            var existing = article != null ? db.Articles.Find(article.Id) : null;
            if (existing == null)
            {
                TempData["Error"] = "Bài viết cần chỉnh sửa không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            if (!isAdmin && (existing.Status == "Published" || existing.Status == "Archived"))
            {
                TempData["Error"] = "Bài viết đã xuất bản hoặc lưu trữ, phóng viên không thể chỉnh sửa trực tiếp.";
                return RedirectToArticleManagement();
            }

            if (article != null)
            {
                article.Content = SanitizeArticleContent(article.Content);
            }

            ValidateArticleInput(article, imageFile, isAdmin);

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(article.Image))
                    article.Image = existing.ImageUrl;
                if (!isAdmin)
                    article.Status = existing.Status;
                return View(article);
            }

            existing.Title = article.Title;
            existing.Content = article.Content;
            existing.Summary = CreateSummary(article.Content);
            existing.UpdatedDate = DateTime.Now;

            if (!isAdmin)
            {
                if (string.Equals(submitAction, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    existing.Status = "Draft";
                    TempData["Success"] = "Đã lưu chỉnh sửa vào Bản nháp (Draft).";
                }
                else
                {
                    existing.Status = "Pending";
                    TempData["Success"] = "Đã cập nhật nội dung và gửi Admin phê duyệt (Pending).";
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(submitAction) && ValidStatuses.Contains(submitAction))
                    existing.Status = submitAction;
                else if (!string.IsNullOrWhiteSpace(article.Status) && ValidStatuses.Contains(article.Status))
                    existing.Status = article.Status;

                TempData["Success"] = "Đã cập nhật bài viết thành công (Trạng thái: " + existing.Status + ").";
            }

            var imageUrl = SaveImage(imageFile);
            if (!string.IsNullOrWhiteSpace(imageUrl))
                existing.ImageUrl = imageUrl;

            db.SaveChanges();
            return RedirectToArticleManagement();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeWorkflowStatus(int? id, string newStatus, string returnStatus = "")
        {
            EnsureWorkflowStatusConstraint();

            if (!CanManageArticles())
            {
                TempData["Error"] = "Vui lòng đăng nhập tài khoản Admin hoặc Editor.";
                return RedirectToAction("Login", "Account");
            }

            if (!id.HasValue || id.Value <= 0 || string.IsNullOrWhiteSpace(newStatus) || !ValidStatuses.Contains(newStatus))
            {
                TempData["Error"] = "Yêu cầu chuyển trạng thái bài viết không hợp lệ.";
                return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
            }

            var article = db.Articles.Find(id.Value);
            if (article == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết cần cập nhật.";
                return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
            }

            bool isAdmin = IsAdminUser();
            if (!isAdmin)
            {
                bool canSubmit = (article.Status == "Draft" || article.Status == "Rejected") && newStatus == "Pending";
                bool canWithdraw = article.Status == "Pending" && newStatus == "Draft";

                if (!canSubmit && !canWithdraw)
                {
                    TempData["Error"] = "Phóng viên chỉ có quyền gửi duyệt bài nháp/bị từ chối hoặc rút bài chờ duyệt về nháp.";
                    return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
                }
            }

            article.Status = newStatus;
            article.UpdatedDate = DateTime.Now;
            db.SaveChanges();

            switch (newStatus)
            {
                case "Pending":
                    TempData["Success"] = "Đã gửi bài viết #" + article.ArticleID + " sang trạng thái Chờ duyệt (Pending).";
                    break;
                case "Published":
                    TempData["Success"] = "Đã phê duyệt và xuất bản bài viết #" + article.ArticleID + " lên trang chủ (Published).";
                    break;
                case "Rejected":
                    TempData["Success"] = "Đã từ chối bài viết #" + article.ArticleID + " — trả về phóng viên chỉnh sửa (Rejected).";
                    break;
                case "Archived":
                    TempData["Success"] = "Đã chuyển bài viết #" + article.ArticleID + " vào kho lưu trữ (Archived).";
                    break;
                default:
                    TempData["Success"] = "Đã chuyển bài viết #" + article.ArticleID + " về Bản nháp (Draft).";
                    break;
            }

            return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
        }

        public ActionResult Delete(int? id)
        {
            var denied = CheckArticlePermission("xóa");
            if (denied != null) return denied;

            bool isAdmin = IsAdminUser();
            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = isAdmin;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết cần xóa không hợp lệ.";
                return RedirectToArticleManagement();
            }

            var article = db.Articles.Find(id.Value);
            if (article == null)
            {
                TempData["Error"] = "Bài viết cần xóa không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            if (!isAdmin && (article.Status == "Published" || article.Status == "Archived"))
            {
                TempData["Error"] = "Phóng viên không thể xóa bài viết đã xuất bản hoặc đã lưu trữ.";
                return RedirectToArticleManagement();
            }

            return View(ToArticleViewModel(article));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int? id)
        {
            var denied = CheckArticlePermission("xóa");
            if (denied != null) return denied;

            bool isAdmin = IsAdminUser();
            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = isAdmin;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết cần xóa không hợp lệ.";
                return RedirectToArticleManagement();
            }

            var article = db.Articles.Find(id.Value);
            if (article == null)
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            if (!isAdmin && (article.Status == "Published" || article.Status == "Archived"))
            {
                TempData["Error"] = "Phóng viên không thể xóa bài viết đã xuất bản hoặc đã lưu trữ.";
                return RedirectToArticleManagement();
            }

            try
            {
                db.Articles.Remove(article);
                db.SaveChanges();
                TempData["Success"] = "Đã xóa bài viết thành công.";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể xóa: bài viết còn bình luận hoặc đánh giá liên quan.";
            }
            return RedirectToArticleManagement();
        }

        private Article ToArticleViewModel(Articles article)
        {
            return new Article
            {
                Id = article.ArticleID,
                Title = article.Title,
                Content = article.Content,
                CreatedDate = article.CreatedDate,
                Image = article.ImageUrl,
                ImageUrl = article.ImageUrl,
                Status = article.Status,
                ViewCount = article.ViewCount
            };
        }

        private string CreateSummary(string content)
        {
            string text = GetPlainText(content);

            if (text.Length <= 180)
                return text;

            return text.Substring(0, 180) + "...";
        }

        private string SaveImage(HttpPostedFileBase imageFile)
        {
            if (imageFile == null || imageFile.ContentLength <= 0)
                return null;

            var extension = Path.GetExtension(imageFile.FileName);
            var fileName = Guid.NewGuid().ToString("N") + extension;
            var folder = Server.MapPath("~/Content/Images/");
            Directory.CreateDirectory(folder);
            imageFile.SaveAs(Path.Combine(folder, fileName));
            return Url.Content("~/Content/Images/" + fileName);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}