using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class ArticlesController : Controller
    {
        private readonly NewsPortalDBEntities db = new NewsPortalDBEntities();

        private bool CanManageArticles()
        {
            return Session["Admin"] != null ||
                string.Equals(Convert.ToString(Session["Role"]), "Editor", StringComparison.OrdinalIgnoreCase);
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

        private void ValidateArticleInput(Article article, HttpPostedFileBase imageFile, bool checkStatus = false)
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

            if (string.IsNullOrWhiteSpace(article.Title))
                ModelState.AddModelError("Title", "Tiêu đề không được để trống.");
            else if (article.Title.Length < 10 || article.Title.Length > 250)
                ModelState.AddModelError("Title", "Tiêu đề phải từ 10 đến 250 ký tự.");

            if (string.IsNullOrWhiteSpace(article.Content))
                ModelState.AddModelError("Content", "Nội dung bài viết không được để trống.");
            else if (article.Content.Length < 50 || article.Content.Length > 20000)
                ModelState.AddModelError("Content", "Nội dung bài viết phải từ 50 đến 20000 ký tự.");

            if (checkStatus && !string.IsNullOrEmpty(article.Status) &&
                article.Status != "Draft" && article.Status != "Published")
            {
                ModelState.AddModelError("Status", "Trạng thái bài viết không hợp lệ.");
            }

            // Kiểm tra ảnh tải lên (không bắt buộc có ảnh)
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var ext = (Path.GetExtension(imageFile.FileName) ?? string.Empty).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowed.Contains(ext))
                    ModelState.AddModelError("", "Chỉ chấp nhận định dạng ảnh .jpg, .jpeg, .png, .gif, .webp.");
                else if (imageFile.ContentLength > 2 * 1024 * 1024)
                    ModelState.AddModelError("", "Dung lượng ảnh tối đa là 2MB.");
            }
        }

        public ActionResult Index(string searchString, int? categoryId, int? authorId,
            string status, DateTime? fromDate, DateTime? toDate, int? page)
        {
            var denied = CheckAdminArticlePage();
            if (denied != null) return denied;

            const int pageSize = 8;
            int requestedPage = page ?? 1;
            bool isAdminManagementRequest = IsAdminArticleRequest();
            bool canManageArticles = isAdminManagementRequest && CanManageArticles();

            var query = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .AsQueryable();

            if (!canManageArticles)
            {
                query = query.Where(a => a.Status == "Published");
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

            if (canManageArticles && (status == "Draft" || status == "Published"))
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

            return View(articles);
        }

        public ActionResult Details(int? id)
        {
            return LoadArticleDetails(id, false);
        }

        [ActionName("View")]
        public ActionResult ViewArticle(int? id)
        {
            return LoadArticleDetails(id, true);
        }

        private ActionResult LoadArticleDetails(int? id, bool canComment)
        {
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

            bool canManageArticles = IsAdminArticleRequest() && CanManageArticles();
            if (article == null || (article.Status != "Published" && !canManageArticles))
            {
                TempData["Error"] = "Bài viết bạn tìm không tồn tại hoặc đã bị gỡ.";
                return IsAdminArticleRequest() ? RedirectToArticleManagement() : RedirectToAction("Index");
            }

            if (canComment && !HasViewedRecently(articleId))
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
                .Where(c => c.ArticleID == articleId)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            ViewBag.AuthorId = article.AuthorID;
            ViewBag.AuthorName = article.Authors == null ? null : article.Authors.AuthorName;
            ViewBag.RatingCount = db.ArticleRatings.Count(r => r.ArticleID == articleId);
            ViewBag.CanManageArticles = canManageArticles;
            ViewBag.IsAdminManagement = IsAdminArticleRequest();
            ViewBag.AverageRating = db.ArticleRatings
                .Where(r => r.ArticleID == articleId)
                .Select(r => (double?)r.Rating)
                .Average() ?? 0;
            ViewBag.IsFavorite = GetFavoriteIds().Contains(articleId);
            ViewBag.CanComment = canComment;

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
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc đã bị xóa.";
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

            db.Comments.Add(new Comments
            {
                ArticleID = articleId.Value,
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
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc đã bị xóa.";
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
            if (!articleId.HasValue || articleId.Value <= 0 || !db.Articles.Any(a => a.ArticleID == articleId.Value))
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc đã bị xóa.";
                return RedirectToAction("Index");
            }

            var favoriteIds = GetFavoriteIds();
            if (favoriteIds.Contains(articleId.Value))
            {
                favoriteIds.Remove(articleId.Value);
                TempData["FavoriteMessage"] = "Đã bỏ khỏi bài viết yêu thích.";
            }
            else
            {
                favoriteIds.Add(articleId.Value);
                TempData["FavoriteMessage"] = "Đã lưu bài viết yêu thích.";
            }

            Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", favoriteIds))
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true
            });

            return RedirectToAction("View", new { id = articleId.Value });
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
            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;
            return View(new Article());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Article article, HttpPostedFileBase imageFile)
        {
            var denied = CheckArticlePermission("thêm");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;

            ValidateArticleInput(article, imageFile);

            if (!ModelState.IsValid)
                return View(article);

            var category = db.Categories.OrderBy(c => c.CategoryID).FirstOrDefault();
            var author = db.Authors.OrderBy(a => a.AuthorID).FirstOrDefault();
            if (category == null || author == null)
            {
                ModelState.AddModelError("", "Cần có danh mục và tác giả trước khi thêm bài viết.");
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
                Status = "Published",
                ImageUrl = SaveImage(imageFile),
                ViewCount = 0
            });
            db.SaveChanges();

            TempData["Success"] = "Đã đăng bài viết mới thành công.";
            return RedirectToArticleManagement();
        }

        public ActionResult Edit(int? id)
        {
            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;

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

            return View(ToArticleViewModel(article));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Article article, HttpPostedFileBase imageFile)
        {
            var denied = CheckArticlePermission("chỉnh sửa");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;

            ValidateArticleInput(article, imageFile, true);

            if (!ModelState.IsValid)
                return View(article);

            var existing = db.Articles.Find(article.Id);
            if (existing == null)
            {
                TempData["Error"] = "Bài viết cần chỉnh sửa không tồn tại hoặc đã bị xóa trước đó.";
                return RedirectToArticleManagement();
            }

            existing.Title = article.Title;
            existing.Content = article.Content;
            existing.Summary = CreateSummary(article.Content);
            existing.UpdatedDate = DateTime.Now;

            if (article.Status == "Draft" || article.Status == "Published")
                existing.Status = article.Status;

            var imageUrl = SaveImage(imageFile);
            if (!string.IsNullOrWhiteSpace(imageUrl))
                existing.ImageUrl = imageUrl;

            db.SaveChanges();
            TempData["Success"] = "Đã cập nhật bài viết thành công.";
            return RedirectToArticleManagement();
        }

        public ActionResult Delete(int? id)
        {
            var denied = CheckArticlePermission("xóa");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;

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

            return View(ToArticleViewModel(article));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int? id)
        {
            var denied = CheckArticlePermission("xóa");
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = true;

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
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var text = content.Trim();
            return text.Length <= 180 ? text : text.Substring(0, 180) + "...";
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