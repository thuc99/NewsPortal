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

        public ActionResult Keyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index", "Home");
            }

            keyword = keyword.Trim();

            var articles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .Where(a => a.Status == "Published" &&
                    (
                        a.Title.Contains(keyword) ||
                        a.Summary.Contains(keyword) ||
                        a.Content.Contains(keyword)
                    ))
                .OrderByDescending(a => a.CreatedDate)
                .ToList();

            ViewBag.Keyword = keyword;
            ViewBag.TotalArticles = articles.Count;

            return View(articles);
        }

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

                const string sql = @"
IF OBJECT_ID(N'dbo.Articles', N'U') IS NOT NULL
BEGIN
    DECLARE @dropSql NVARCHAR(MAX) = N'';
    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Articles DROP CONSTRAINT [' + REPLACE(cc.name COLLATE DATABASE_DEFAULT, N']', N']]') + N'];'
    FROM sys.check_constraints cc
    LEFT JOIN sys.columns col
        ON col.object_id = cc.parent_object_id
       AND col.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Articles')
      AND (col.name COLLATE DATABASE_DEFAULT = N'Status'
           OR cc.definition COLLATE DATABASE_DEFAULT LIKE N'%Status%'
           OR cc.name COLLATE DATABASE_DEFAULT IN (N'CK_Articles_Status', N'CK_Articles_Status_Workflow'));

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
        }

        private static string NormalizeRole(string role)
        {
            role = (role ?? string.Empty).Trim();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return "Admin";
            if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
                return "Editor";
            if (string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                return "Author";
            return "Reader";
        }

        private Users GetCurrentDbUser()
        {
            string username = Convert.ToString(Session["User"] ?? Session["Admin"]);
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return db.Users.FirstOrDefault(u => u.Username == username);
        }

        private string GetCurrentRole()
        {
            if (Session["Admin"] != null)
            {
                Session["Role"] = "Admin";
                return "Admin";
            }

            string username = Convert.ToString(Session["User"]);
            if (string.IsNullOrWhiteSpace(username))
                return "Guest";

            var dbUser = db.Users.FirstOrDefault(u => u.Username == username);
            if (dbUser != null)
            {
                if (!dbUser.IsActive)
                {
                    Session.Remove("User");
                    Session.Remove("Role");
                    return "Guest";
                }

                string normalized = NormalizeRole(dbUser.Role);
                Session["Role"] = normalized;
                if (normalized == "Admin")
                {
                    Session["Admin"] = dbUser.Username;
                }
                return normalized;
            }

            return NormalizeRole(Convert.ToString(Session["Role"]));
        }

        private bool IsAdminUser()
        {
            return string.Equals(GetCurrentRole(), "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanReviewArticles()
        {
            string role = GetCurrentRole();
            return role == "Admin" || role == "Editor";
        }

        private bool CanManageArticles()
        {
            string role = GetCurrentRole();
            return role == "Admin" || role == "Editor" || role == "Author";
        }

        // Tự động lấy hoặc khởi tạo hồ sơ Tác giả (dbo.Authors) cho tài khoản đang đăng nhập
        private Author EnsureCurrentUserAuthorProfile(Users dbUser, bool createIfMissing)
        {
            if (dbUser == null)
                return null;

            var linked = db.Authors.FirstOrDefault(a => a.UserId == dbUser.UserId);
            if (linked != null)
                return linked;

            if (!string.IsNullOrWhiteSpace(dbUser.Email))
            {
                string lowerEmail = dbUser.Email.Trim().ToLower();
                var byEmail = db.Authors.FirstOrDefault(a =>
                    a.UserId == null &&
                    a.Email != null &&
                    a.Email.ToLower() == lowerEmail);

                if (byEmail != null)
                {
                    byEmail.UserId = dbUser.UserId;
                    db.SaveChanges();
                    return byEmail;
                }
            }

            if (!createIfMissing)
                return null;

            string authorName = string.IsNullOrWhiteSpace(dbUser.FullName)
                ? dbUser.Username
                : dbUser.FullName.Trim();
            if (authorName.Length > 150)
                authorName = authorName.Substring(0, 150);

            string authorEmail = string.IsNullOrWhiteSpace(dbUser.Email)
                ? (dbUser.Username + "@newsportal.local")
                : dbUser.Email.Trim();
            if (authorEmail.Length > 150)
                authorEmail = authorEmail.Substring(0, 150);

            string avatar = (!string.IsNullOrWhiteSpace(dbUser.AvatarUrl) && dbUser.AvatarUrl.Length <= 500)
                ? dbUser.AvatarUrl
                : null;

            var created = new Author
            {
                AuthorName = authorName,
                Email = authorEmail,
                Images = avatar,
                UserId = dbUser.UserId
            };

            db.Authors.Add(created);
            db.SaveChanges();
            return created;
        }

        private bool OwnsArticle(Articles article, Users currentUser, Author currentAuthor)
        {
            if (article == null) return false;
            if (currentUser != null && article.UserId.HasValue && article.UserId.Value == currentUser.UserId)
                return true;
            if (currentAuthor != null && article.AuthorID == currentAuthor.AuthorID)
                return true;
            return false;
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

        // Điều hướng về trang trước đó (nếu hợp lệ), ngược lại quay về danh sách quản lý bài viết
        private ActionResult RedirectBackOrHome(string returnUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var sessionReturnUrl = Convert.ToString(Session["ArticleReturnUrl"]);
            Session.Remove("ArticleReturnUrl");

            if (!string.IsNullOrWhiteSpace(sessionReturnUrl) && Url.IsLocalUrl(sessionReturnUrl))
            {
                return Redirect(sessionReturnUrl);
            }

            return RedirectToArticleManagement();
        }

        private void RememberPreviousPage(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                Session["ArticleReturnUrl"] = returnUrl;
                return;
            }

            if (Request.UrlReferrer != null &&
                string.Equals(Request.UrlReferrer.Host, Request.Url.Host, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Request.UrlReferrer.AbsolutePath, Request.Url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                Session["ArticleReturnUrl"] = Request.UrlReferrer.PathAndQuery;
            }
        }

        private ActionResult CheckArticlePermission(string actionLabel)
        {
            if (!CanManageArticles())
            {
                if (Session["User"] != null)
                {
                    TempData["Error"] = "Bạn không có quyền " + actionLabel + " bài viết. Chức năng này dành cho Quản trị viên, Biên tập viên và Tác giả.";
                    return new RedirectResult("~/Articles");
                }

                TempData["Error"] = "Vui lòng đăng nhập tài khoản Quản trị viên, Biên tập viên hoặc Tác giả trước khi truy cập chức năng này.";
                return new RedirectResult("~/Account/Login");
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

            if (!article.CategoryId.HasValue || article.CategoryId.Value <= 0)
            {
                ModelState.Remove("CategoryId");
                ModelState.AddModelError("CategoryId", "Vui lòng chọn danh mục bài viết.");
            }

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

            bool hasUploadedImage = imageFile != null && imageFile.ContentLength > 0;
            article.ImageUrl = (article.ImageUrl ?? string.Empty).Trim();

            if (requireImage && !hasUploadedImage && string.IsNullOrWhiteSpace(article.ImageUrl))
            {
                ModelState.AddModelError("ImageUrl", "Vui lòng tải ảnh lên hoặc nhập URL ảnh đại diện.");
            }
            else if (!hasUploadedImage && !string.IsNullOrWhiteSpace(article.ImageUrl))
            {
                Uri imageUri;
                if (article.ImageUrl.Length > 1000 ||
                    !Uri.TryCreate(article.ImageUrl, UriKind.Absolute, out imageUri) ||
                    (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError("ImageUrl", "URL ảnh phải bắt đầu bằng http:// hoặc https:// và tối đa 1000 ký tự.");
                }
            }

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
            string currentRole = canManageArticles ? GetCurrentRole() : "Reader";
            bool isAdminUser = canManageArticles && currentRole == "Admin";
            bool canReviewArticles = canManageArticles && (currentRole == "Admin" || currentRole == "Editor");
            bool isAuthorOnly = canManageArticles && currentRole == "Author";

            Users currentUser = canManageArticles ? GetCurrentDbUser() : null;
            Author currentAuthor = canManageArticles
                ? EnsureCurrentUserAuthorProfile(currentUser, isAuthorOnly)
                : null;

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
                if (isAuthorOnly)
                {
                    int myUid = currentUser != null ? currentUser.UserId : -1;
                    int myAid = currentAuthor != null ? currentAuthor.AuthorID : -1;
                    query = query.Where(a =>
                        (myAid > 0 && a.AuthorID == myAid) ||
                        (myUid > 0 && a.UserId == myUid));
                }
                else if (currentRole == "Editor")
                {
                    int myUid = currentUser != null ? currentUser.UserId : -1;
                    int myAid = currentAuthor != null ? currentAuthor.AuthorID : -1;
                    // Biên tập viên xem tất cả bài gửi duyệt/xuất bản/từ chối/lưu trữ + bản nháp của chính mình
                    query = query.Where(a =>
                        a.Status != "Draft" ||
                        (myUid > 0 && a.UserId == myUid) ||
                        (myAid > 0 && a.AuthorID == myAid));
                }

                ViewBag.CountAll = query.Count();
                ViewBag.CountDraft = query.Count(a => a.Status == "Draft");
                ViewBag.CountPending = query.Count(a => a.Status == "Pending");
                ViewBag.CountPublished = query.Count(a => a.Status == "Published");
                ViewBag.CountRejected = query.Count(a => a.Status == "Rejected");
                ViewBag.CountArchived = query.Count(a => a.Status == "Archived");
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

            if (authorId.HasValue && !isAuthorOnly)
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

            var pagedEntities = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var rejectionReasons = pagedEntities
                .Where(a => !string.IsNullOrWhiteSpace(a.RejectionReason))
                .ToDictionary(a => a.ArticleID, a => a.RejectionReason);

            var articles = pagedEntities
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
            ViewBag.CanReviewArticles = canReviewArticles;
            ViewBag.IsAuthorOnly = isAuthorOnly;
            ViewBag.CurrentRole = currentRole;
            ViewBag.RejectionReasons = rejectionReasons;

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
            if (!id.HasValue || id.Value <= 0)
                return;

            int artId = id.Value;

            // Chỉ ghi nhận lượt đọc bài viết đã xuất bản
            bool articleExists = db.Articles.Any(a =>
                a.ArticleID == artId &&
                a.Status == "Published");

            if (!articleExists)
                return;

            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;

            if (!string.IsNullOrWhiteSpace(currentUsername))
            {
                var viewer = db.Users.FirstOrDefault(u =>
                    u.Username == currentUsername);

                if (viewer != null)
                {
                    // Mỗi lần mở bài viết sẽ thêm một bản ghi lịch sử mới
                    db.ArticleViewHistory.Add(new ArticleViewHistory
                    {
                        UserId = viewer.UserId,
                        ArticleId = artId,
                        ViewedDate = DateTime.Now
                    });

                    db.SaveChanges();

                    ViewBag.IsSavedArticle = db.SavedArticles.Any(s =>
                        s.UserId == viewer.UserId &&
                        s.ArticleId == artId);
                }
            }

            ViewBag.IsSavedArticle = GetFavoriteIds().Contains(artId);
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
            string currentRole = hasManagePermission ? GetCurrentRole() : "Reader";
            Users currentUser = hasManagePermission ? GetCurrentDbUser() : null;
            Author currentAuthor = hasManagePermission
                ? EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author")
                : null;
            bool ownsArticle = OwnsArticle(article, currentUser, currentAuthor);

            bool canAccessUnpublished = false;
            if (article != null && hasManagePermission)
            {
                if (currentRole == "Admin")
                    canAccessUnpublished = true;
                else if (currentRole == "Editor")
                    canAccessUnpublished = (article.Status != "Draft") || ownsArticle;
                else if (currentRole == "Author")
                    canAccessUnpublished = ownsArticle;
            }

            bool canManageArticles = IsAdminArticleRequest() && hasManagePermission;
            if (article == null || (article.Status != "Published" && !canAccessUnpublished))
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
            ViewBag.CanEditArticle = (currentRole == "Admin" || currentRole == "Editor" || (currentRole == "Author" && ownsArticle));
            ViewBag.IsAdminManagement = IsAdminArticleRequest();
            ViewBag.IsAdminUser = (currentRole == "Admin");
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
                CreatedDate = DateTime.Now,
                Status = "Pending"
            });
            db.SaveChanges();

            TempData["CommentSuccess"] = "Bình luận của bạn đã được gửi và đang chờ Admin phê duyệt.";
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

            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;
            var viewer = !string.IsNullOrEmpty(currentUsername)
                ? db.Users.FirstOrDefault(u => u.Username == currentUsername)
                : null;

            if (viewer != null)
            {
                var existing = db.SavedArticles.FirstOrDefault(s => s.UserId == viewer.UserId && s.ArticleId == artId);
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
                        ArticleId = artId,
                        SavedDate = DateTime.Now
                    });
                    TempData["FavoriteMessage"] = "Đã lưu bài viết vào Tin đã lưu.";
                }
                db.SaveChanges();

                var syncedIds = db.SavedArticles
                    .Where(s => s.UserId == viewer.UserId)
                    .OrderByDescending(s => s.SavedDate)
                    .Select(s => s.ArticleId)
                    .ToList();

                Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", syncedIds))
                {
                    Expires = DateTime.Now.AddYears(1),
                    HttpOnly = true
                });
            }
            else
            {
                var favoriteIds = GetFavoriteIds();
                if (favoriteIds.Contains(artId))
                {
                    favoriteIds.Remove(artId);
                    TempData["FavoriteMessage"] = "Đã bỏ lưu bài viết.";
                }
                else
                {
                    favoriteIds.Add(artId);
                    TempData["FavoriteMessage"] = "Đã lưu bài viết vào Tin đã lưu.";
                }

                Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", favoriteIds))
                {
                    Expires = DateTime.Now.AddYears(1),
                    HttpOnly = true
                });
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
            string currentUsername = (Session["User"] ?? Session["Admin"]) as string;

            if (!string.IsNullOrEmpty(currentUsername))
            {
                var viewer = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (viewer != null)
                {
                    // Gộp các bài lưu tạm trong Cookie vào bảng SavedArticles (SQL) nếu chưa có
                    bool hasNew = false;
                    foreach (var artId in favoriteIds)
                    {
                        bool exists = db.SavedArticles.Any(s => s.UserId == viewer.UserId && s.ArticleId == artId);
                        if (!exists && db.Articles.Any(a => a.ArticleID == artId && a.Status == "Published"))
                        {
                            db.SavedArticles.Add(new SavedArticles
                            {
                                UserId = viewer.UserId,
                                ArticleId = artId,
                                SavedDate = DateTime.Now
                            });
                            hasNew = true;
                        }
                    }
                    if (hasNew) db.SaveChanges();

                    // Lấy toàn bộ bài đã lưu từ SQL và cập nhật ngược lại vào Cookie cho khớp
                    favoriteIds = db.SavedArticles
                        .Where(s => s.UserId == viewer.UserId)
                        .OrderByDescending(s => s.SavedDate)
                        .Select(s => s.ArticleId)
                        .ToList();

                    Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", favoriteIds))
                    {
                        Expires = DateTime.Now.AddYears(1),
                        HttpOnly = true
                    });
                }
            }

            var articles = db.Articles
                .Include(a => a.Categories)
                .Where(a => favoriteIds.Contains(a.ArticleID) && a.Status == "Published")
                .OrderByDescending(a => a.CreatedDate)
                .ToList()
                .Select(ToArticleViewModel)
                .ToList();

            return View(articles);
        }

        // Upload ảnh được chèn trực tiếp trong nội dung bài viết bằng CKEditor 5.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadContentImage(HttpPostedFileBase upload)
        {
            var denied = CheckArticlePermission("tải ảnh lên");
            if (denied != null)
            {
                Response.StatusCode = 403;
                return Json(new
                {
                    uploaded = false,
                    error = "Bạn không có quyền tải ảnh lên."
                });
            }

            if (upload == null || upload.ContentLength <= 0)
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    uploaded = false,
                    error = "Vui lòng chọn ảnh cần tải lên."
                });
            }

            // Ảnh trong nội dung được giới hạn 5MB.
            if (upload.ContentLength > 5 * 1024 * 1024)
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    uploaded = false,
                    error = "Dung lượng ảnh tối đa là 5MB."
                });
            }

            string extension = (Path.GetExtension(upload.FileName) ?? string.Empty)
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    uploaded = false,
                    error = "Chỉ chấp nhận ảnh JPG, JPEG, PNG, GIF hoặc WEBP."
                });
            }

            // Kiểm tra MIME type để hạn chế tệp giả dạng ảnh.
            string contentType = (upload.ContentType ?? string.Empty).ToLowerInvariant();
            string[] allowedContentTypes =
            {
                "image/jpeg", "image/png", "image/gif", "image/webp"
            };

            if (!allowedContentTypes.Contains(contentType))
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    uploaded = false,
                    error = "Loại tệp tải lên không phải định dạng ảnh được hỗ trợ."
                });
            }

            try
            {
                string relativeFolder = "~/Content/Images/Articles/Content/";
                string folder = Server.MapPath(relativeFolder);

                if (string.IsNullOrWhiteSpace(folder))
                    throw new IOException("Không xác định được thư mục lưu ảnh.");

                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid().ToString("N") + extension;
                string filePath = Path.Combine(folder, fileName);

                upload.SaveAs(filePath);

                string imageUrl = Url.Content(relativeFolder + fileName);

                return Json(new
                {
                    uploaded = true,
                    url = imageUrl
                });
            }
            catch (Exception)
            {
                Response.StatusCode = 500;
                return Json(new
                {
                    uploaded = false,
                    error = "Không thể lưu ảnh lên máy chủ. Vui lòng thử lại."
                });
            }
        }

        private void PopulateArticleSelections(int? selectedCategoryId, int? selectedAuthorId, string currentRole, Author currentAuthor)
        {
            ViewBag.CategoryId = new SelectList(
                db.Categories.OrderBy(c => c.CategoryName).ToList(),
                "CategoryID",
                "CategoryName",
                selectedCategoryId);

            var allAuthors = db.Authors.OrderBy(a => a.AuthorName).ToList();
            if (!selectedAuthorId.HasValue && currentAuthor != null)
            {
                selectedAuthorId = currentAuthor.AuthorID;
            }

            ViewBag.AuthorId = new SelectList(
                allAuthors,
                "AuthorID",
                "AuthorName",
                selectedAuthorId);

            ViewBag.CurrentRole = currentRole;
            ViewBag.CanReviewArticles = (currentRole == "Admin" || currentRole == "Editor");
            ViewBag.IsAuthorOnly = (currentRole == "Author");
            ViewBag.LockedAuthorName = currentAuthor != null ? currentAuthor.AuthorName : null;
        }

        public ActionResult Create(string returnUrl)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            RememberPreviousPage(returnUrl);

            string currentRole = GetCurrentRole();
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");
            ViewBag.ReturnUrl = returnUrl;

            int? defaultAuthorId = currentAuthor != null ? (int?)currentAuthor.AuthorID : null;
            PopulateArticleSelections(null, defaultAuthorId, currentRole, currentAuthor);

            return View(new Article { AuthorId = defaultAuthorId });
        }

        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Article article, HttpPostedFileBase imageFile, string submitAction, string returnUrl)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            string currentRole = GetCurrentRole();
            bool canReview = (currentRole == "Admin" || currentRole == "Editor");
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");
            ViewBag.ReturnUrl = returnUrl;

            if (article != null)
            {
                article.Content = SanitizeArticleContent(article.Content);
            }

            ModelState.Remove("AuthorId");
            ModelState.Remove("Summary");
            ModelState.Remove("Image");

            PopulateArticleSelections(
                article == null ? null : article.CategoryId,
                article == null ? null : article.AuthorId,
                currentRole,
                currentAuthor);

            ValidateArticleInput(article, imageFile, false, true);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = "Không thể tạo bài viết: " + string.Join(" | ", errors);
                return View(article);
            }

            var category = db.Categories.FirstOrDefault(c => c.CategoryID == article.CategoryId.Value);
            if (category == null)
            {
                ModelState.AddModelError("CategoryId", "Danh mục đã chọn không tồn tại.");
                PopulateArticleSelections(article.CategoryId, article.AuthorId, currentRole, currentAuthor);
                return View(article);
            }

            Author selectedAuthor = null;
            if (currentRole == "Author")
            {
                selectedAuthor = currentAuthor;
            }
            else
            {
                if (article.AuthorId.HasValue && article.AuthorId.Value > 0)
                {
                    selectedAuthor = db.Authors.FirstOrDefault(a => a.AuthorID == article.AuthorId.Value);
                }

                if (selectedAuthor == null)
                {
                    selectedAuthor = currentAuthor ?? db.Authors.OrderBy(a => a.AuthorID).FirstOrDefault();
                }

                if (selectedAuthor == null && currentUser != null)
                {
                    selectedAuthor = EnsureCurrentUserAuthorProfile(currentUser, true);
                }
            }

            if (selectedAuthor == null)
            {
                ModelState.AddModelError("", "Hệ thống chưa có tác giả nào. Vui lòng thêm tác giả trong mục Quản lý Tác giả trước.");
                PopulateArticleSelections(article.CategoryId, article.AuthorId, currentRole, currentAuthor);
                return View(article);
            }

            string targetStatus;
            if (!canReview)
            {
                // Tác giả (Author): chỉ được lưu nháp (Draft) hoặc gửi duyệt (Pending)
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
                imageUrl = article.ImageUrl;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                ModelState.AddModelError("ImageUrl", "Vui lòng chọn ảnh đại diện hoặc nhập URL ảnh hợp lệ.");
                PopulateArticleSelections(article.CategoryId, article.AuthorId, currentRole, currentAuthor);
                return View(article);
            }

            db.Articles.Add(new Articles
            {
                CategoryID = category.CategoryID,
                AuthorID = selectedAuthor.AuthorID,
                UserId = currentUser != null ? (int?)currentUser.UserId : selectedAuthor.UserId,
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
                TempData["Success"] = "Đã gửi bài viết sang hàng đợi chờ phê duyệt (Pending).";
            else
                TempData["Success"] = "Đã xuất bản bài viết mới lên trang chủ (Published).";

            return RedirectBackOrHome(returnUrl);
        }

        public ActionResult Edit(int? id, string returnUrl = null)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            RememberPreviousPage(returnUrl);

            string currentRole = GetCurrentRole();
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");
            ViewBag.ReturnUrl = returnUrl;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết cần chỉnh sửa không hợp lệ.";
                return RedirectToArticleManagement();
            }

            var article = db.Articles
                .Include(a => a.Authors)
                .Include(a => a.Categories)
                .FirstOrDefault(a => a.ArticleID == id.Value);

            if (article == null)
            {
                TempData["Error"] = "Bài viết cần chỉnh sửa không tồn tại hoặc đã bị xóa.";
                return RedirectToArticleManagement();
            }

            if (currentRole == "Author" && !OwnsArticle(article, currentUser, currentAuthor))
            {
                TempData["Error"] = "Bạn chỉ được phép chỉnh sửa bài viết của chính mình.";
                return RedirectToArticleManagement();
            }

            if (currentRole == "Editor" && article.Status == "Draft" && !OwnsArticle(article, currentUser, currentAuthor))
            {
                TempData["Error"] = "Không thể chỉnh sửa bản nháp riêng của tác giả khác khi chưa gửi duyệt.";
                return RedirectToArticleManagement();
            }

            ViewBag.RejectionReason = article.RejectionReason ?? string.Empty;
            PopulateArticleSelections(article.CategoryID, article.AuthorID, currentRole, currentAuthor);

            return View(ToArticleViewModel(article));
        }

        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Article article, HttpPostedFileBase imageFile, string submitAction, string rejectionReason = null, string returnUrl = null)
        {
            EnsureWorkflowStatusConstraint();

            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            string currentRole = GetCurrentRole();
            bool canReview = (currentRole == "Admin" || currentRole == "Editor");
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");
            ViewBag.ReturnUrl = returnUrl;

            var existing = article != null ? db.Articles.Find(article.Id) : null;
            if (existing == null)
            {
                TempData["Error"] = "Bài viết cần chỉnh sửa không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            if (currentRole == "Author" && !OwnsArticle(existing, currentUser, currentAuthor))
            {
                TempData["Error"] = "Bạn chỉ được phép chỉnh sửa bài viết của chính mình.";
                return RedirectToArticleManagement();
            }

            if (currentRole == "Editor" && existing.Status == "Draft" && !OwnsArticle(existing, currentUser, currentAuthor))
            {
                TempData["Error"] = "Không thể chỉnh sửa bản nháp riêng của tác giả khác.";
                return RedirectToArticleManagement();
            }

            if (article != null)
            {
                article.Content = SanitizeArticleContent(article.Content);
                if (!article.CategoryId.HasValue || article.CategoryId.Value <= 0)
                {
                    article.CategoryId = existing.CategoryID;
                }
            }

            ModelState.Remove("AuthorId");
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Summary");
            ModelState.Remove("Image");

            ValidateArticleInput(article, imageFile, canReview);

            var chosenCategory = (article != null && article.CategoryId.HasValue)
                ? db.Categories.FirstOrDefault(c => c.CategoryID == article.CategoryId.Value)
                : null;

            if (chosenCategory == null)
            {
                ModelState.AddModelError("CategoryId", "Danh mục đã chọn không tồn tại.");
            }

            string targetStatus = existing.Status;
            rejectionReason = (rejectionReason ?? string.Empty).Trim();
            if (rejectionReason.Length > 500)
                rejectionReason = rejectionReason.Substring(0, 500);

            if (canReview)
            {
                if (!string.IsNullOrWhiteSpace(submitAction) && ValidStatuses.Contains(submitAction))
                    targetStatus = submitAction;
                else if (article != null && !string.IsNullOrWhiteSpace(article.Status) && ValidStatuses.Contains(article.Status))
                    targetStatus = article.Status;

                if (targetStatus == "Rejected" && string.IsNullOrWhiteSpace(rejectionReason) && string.IsNullOrWhiteSpace(existing.RejectionReason))
                {
                    ModelState.AddModelError("Status", "Vui lòng nhập lý do từ chối bài viết.");
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = "Không thể lưu bài viết: " + string.Join(" | ", errors);

                if (article != null)
                {
                    if (string.IsNullOrWhiteSpace(article.Image))
                        article.Image = existing.ImageUrl;
                    if (!canReview)
                        article.Status = existing.Status;
                }

                ViewBag.RejectionReason = !string.IsNullOrWhiteSpace(rejectionReason)
                    ? rejectionReason
                    : (existing.RejectionReason ?? string.Empty);

                PopulateArticleSelections(
                    article != null ? article.CategoryId : (int?)existing.CategoryID,
                    article != null && article.AuthorId.HasValue ? article.AuthorId : (int?)existing.AuthorID,
                    currentRole,
                    currentAuthor);

                return View(article);
            }

            existing.Title = article.Title;
            existing.CategoryID = chosenCategory.CategoryID;
            existing.Content = article.Content;
            existing.Summary = CreateSummary(article.Content);
            existing.UpdatedDate = DateTime.Now;

            if (canReview && article.AuthorId.HasValue && article.AuthorId.Value > 0)
            {
                var chosenAuthor = db.Authors.FirstOrDefault(a => a.AuthorID == article.AuthorId.Value);
                if (chosenAuthor != null)
                {
                    existing.AuthorID = chosenAuthor.AuthorID;
                }
            }

            if (!canReview)
            {
                if (string.Equals(submitAction, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    existing.Status = "Draft";
                    TempData["Success"] = "Đã lưu chỉnh sửa vào Bản nháp (Draft).";
                }
                else
                {
                    existing.Status = "Pending";
                    existing.RejectionReason = null;
                    TempData["Success"] = "Đã cập nhật nội dung và gửi Biên tập viên / Admin phê duyệt (Pending).";
                }
            }
            else
            {
                existing.Status = targetStatus;
                if (targetStatus == "Rejected")
                {
                    if (!string.IsNullOrWhiteSpace(rejectionReason))
                        existing.RejectionReason = rejectionReason;
                }
                else if (targetStatus == "Published")
                {
                    existing.RejectionReason = null;
                }

                TempData["Success"] = "Đã cập nhật bài viết thành công (Trạng thái: " + existing.Status + ").";
            }

            var imageUrl = SaveImage(imageFile);
            if (!string.IsNullOrWhiteSpace(imageUrl))
                existing.ImageUrl = imageUrl;

            db.SaveChanges();

            return RedirectBackOrHome(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeWorkflowStatus(int? id, string newStatus, string rejectionReason = "", string returnStatus = "")
        {
            EnsureWorkflowStatusConstraint();

            if (!CanManageArticles())
            {
                TempData["Error"] = "Vui lòng đăng nhập tài khoản Quản trị viên, Biên tập viên hoặc Tác giả.";
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

            string currentRole = GetCurrentRole();
            bool canReview = (currentRole == "Admin" || currentRole == "Editor");
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            if (!canReview)
            {
                if (!OwnsArticle(article, currentUser, currentAuthor))
                {
                    TempData["Error"] = "Bạn chỉ được thay đổi trạng thái bài viết của chính mình.";
                    return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
                }

                bool canSubmit = (article.Status == "Draft" || article.Status == "Rejected") && newStatus == "Pending";
                bool canWithdraw = article.Status == "Pending" && newStatus == "Draft";

                if (!canSubmit && !canWithdraw)
                {
                    TempData["Error"] = "Tác giả chỉ có quyền gửi duyệt bài nháp/bị từ chối hoặc rút bài chờ duyệt về nháp.";
                    return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
                }
            }
            else if (newStatus == "Rejected")
            {
                rejectionReason = (rejectionReason ?? string.Empty).Trim();
                if (rejectionReason.Length > 500)
                    rejectionReason = rejectionReason.Substring(0, 500);

                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["Error"] = "Vui lòng nhập lý do từ chối bài viết để tác giả chỉnh sửa.";
                    return RedirectToRoute("AdminArticles", new { action = "Index", status = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus });
                }

                article.RejectionReason = rejectionReason;
            }

            if (newStatus == "Published" || (newStatus == "Pending" && !canReview))
            {
                article.RejectionReason = null;
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
                    TempData["Success"] = "Đã từ chối bài viết #" + article.ArticleID + " — trả về tác giả chỉnh sửa (Rejected).";
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

            string currentRole = GetCurrentRole();
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã bài viết cần xóa không hợp lệ.";
                return RedirectToArticleManagement();
            }

            var article = db.Articles
                .Include(a => a.Authors)
                .Include(a => a.Categories)
                .FirstOrDefault(a => a.ArticleID == id.Value);

            if (article == null)
            {
                TempData["Error"] = "Bài viết cần xóa không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            if (currentRole == "Author")
            {
                if (!OwnsArticle(article, currentUser, currentAuthor))
                {
                    TempData["Error"] = "Bạn chỉ được phép xóa bài viết của chính mình.";
                    return RedirectToArticleManagement();
                }

                if (article.Status == "Published")
                {
                    TempData["Error"] = "Bài viết đã xuất bản không thể tự xóa. Vui lòng liên hệ Biên tập viên hoặc Quản trị viên.";
                    return RedirectToArticleManagement();
                }
            }

            return View(ToArticleViewModel(article));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int? id)
        {
            var denied = CheckArticlePermission("xóa");
            if (denied != null) return denied;

            string currentRole = GetCurrentRole();
            Users currentUser = GetCurrentDbUser();
            Author currentAuthor = EnsureCurrentUserAuthorProfile(currentUser, currentRole == "Author");

            ViewBag.IsAdminManagement = true;
            ViewBag.IsAdminUser = (currentRole == "Admin");

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

            if (currentRole == "Author")
            {
                if (!OwnsArticle(article, currentUser, currentAuthor))
                {
                    TempData["Error"] = "Bạn chỉ được phép xóa bài viết của chính mình.";
                    return RedirectToArticleManagement();
                }

                if (article.Status == "Published")
                {
                    TempData["Error"] = "Bài viết đã xuất bản không thể tự xóa.";
                    return RedirectToArticleManagement();
                }
            }

            try
            {
                db.Articles.Remove(article);
                db.SaveChanges();
                TempData["Success"] = "Đã xóa bài viết thành công.";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể xóa: bài viết còn bình luận hoặc dữ liệu liên quan.";
            }
            return RedirectToArticleManagement();
        }

        private Article ToArticleViewModel(Articles article)
        {
            return new Article
            {
                Id = article.ArticleID,
                Title = article.Title,
                CategoryId = article.CategoryID,
                AuthorId = article.AuthorID,
                AuthorName = article.Authors != null ? article.Authors.AuthorName : null,
                CategoryName = article.Categories != null ? article.Categories.CategoryName : null,
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
