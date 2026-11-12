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

        public ActionResult Index(string searchString, int? categoryId, int? authorId,
            string status, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            const int pageSize = 8;
            page = Math.Max(page, 1);

            var query = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .AsQueryable();

            if (Session["Admin"] == null)
            {
                query = query.Where(a => a.Status == "Published");
            }
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(a =>
                    a.Title.Contains(searchString) ||
                    a.Summary.Contains(searchString) ||
                    a.Content.Contains(searchString));
            }

            if (categoryId.HasValue)
                query = query.Where(a => a.CategoryID == categoryId.Value);

            if (authorId.HasValue)
                query = query.Where(a => a.AuthorID == authorId.Value);

            if (status == "Draft" || status == "Published")
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
            page = Math.Min(page, totalPages);

            var articles = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
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
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalArticles = totalArticles;

            return View(articles);
        }

        public ActionResult Details(int id)
        {
            return LoadArticleDetails(id, false);
        }

        [ActionName("View")]
        public ActionResult ViewArticle(int id)
        {
            return LoadArticleDetails(id, true);
        }

        private ActionResult LoadArticleDetails(int id, bool canComment)
        {
            var article = db.Articles
                .Include(a => a.Categories)
                .FirstOrDefault(a => a.ArticleID == id);
            if (article == null)
                return HttpNotFound();

            if (canComment && !HasViewedRecently(id))
            {
                article.ViewCount++;
                db.SaveChanges();
                Response.Cookies.Add(new HttpCookie("NewsPortalViewed_" + id, "1")
                {
                    Expires = DateTime.Now.AddHours(1),
                    HttpOnly = true
                });
            }

            ViewBag.Comments = db.Comments
                .Where(c => c.ArticleID == id)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            ViewBag.RatingCount = db.ArticleRatings.Count(r => r.ArticleID == id);
            ViewBag.AverageRating = db.ArticleRatings
                .Where(r => r.ArticleID == id)
                .Select(r => (double?)r.Rating)
                .Average() ?? 0;
            ViewBag.IsFavorite = GetFavoriteIds().Contains(id);
            ViewBag.CanComment = canComment;

            var recommendations = db.Articles
                .Include(a => a.Categories)
                .Where(a => a.ArticleID != id && a.Status == "Published" && a.CategoryID == article.CategoryID)
                .OrderByDescending(a => a.CreatedDate)
                .Take(4)
                .ToList();

            if (recommendations.Count < 4)
            {
                var existingIds = recommendations.Select(a => a.ArticleID).Concat(new[] { id }).ToList();
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
        public ActionResult AddComment(int articleId, string commenterName, string content)
        {
            commenterName = (commenterName ?? string.Empty).Trim();
            content = (content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(commenterName))
            {
                TempData["CommentError"] = "Vui lòng nhập tên của bạn.";
                return RedirectToAction("View", new { id = articleId });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["CommentError"] = "Vui lòng nhập nội dung bình luận.";
                return RedirectToAction("View", new { id = articleId });
            }

            if (commenterName.Length > 100 || content.Length > 1000)
            {
                TempData["CommentError"] = "Tên tối đa 100 ký tự và bình luận tối đa 1000 ký tự.";
                return RedirectToAction("View", new { id = articleId });
            }

            if (!db.Articles.Any(a => a.ArticleID == articleId))
                return HttpNotFound();

            db.Comments.Add(new Comments
            {
                ArticleID = articleId,
                CommenterName = commenterName,
                Content = content,
                CreatedDate = DateTime.Now
            });
            db.SaveChanges();

            TempData["CommentSuccess"] = "Bình luận của bạn đã được đăng.";
            return RedirectToAction("View", new { id = articleId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Rate(int articleId, byte rating)
        {
            if (rating < 1 || rating > 5)
            {
                TempData["RatingError"] = "Vui lòng chọn mức đánh giá từ 1 đến 5 sao.";
                return RedirectToAction("View", new { id = articleId });
            }

            if (!db.Articles.Any(a => a.ArticleID == articleId))
                return HttpNotFound();

            db.ArticleRatings.Add(new ArticleRatings
            {
                ArticleID = articleId,
                RaterName = "Ẩn danh",
                Rating = rating,
                CreatedDate = DateTime.Now
            });
            db.SaveChanges();

            TempData["RatingSuccess"] = "Cảm ơn bạn đã đánh giá bài viết.";
            return RedirectToAction("View", new { id = articleId });
        }

        private bool HasViewedRecently(int articleId)
        {
            return Request.Cookies["NewsPortalViewed_" + articleId] != null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleFavorite(int articleId)
        {
            if (!db.Articles.Any(a => a.ArticleID == articleId))
                return HttpNotFound();

            var favoriteIds = GetFavoriteIds();
            if (favoriteIds.Contains(articleId))
            {
                favoriteIds.Remove(articleId);
                TempData["FavoriteMessage"] = "Đã bỏ khỏi bài viết yêu thích.";
            }
            else
            {
                favoriteIds.Add(articleId);
                TempData["FavoriteMessage"] = "Đã lưu bài viết yêu thích.";
            }

            Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", favoriteIds))
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true
            });

            return RedirectToAction("View", new { id = articleId });
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

        [AdminAuthorize]
        public ActionResult Create()
        {
            return View(new Article());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult Create(Article article, HttpPostedFileBase imageFile)
        {
            if (article.Title != null) article.Title = article.Title.Trim();
            if (article.Content != null) article.Content = article.Content.Trim();

            if (string.IsNullOrWhiteSpace(article.Title))
                ModelState.AddModelError("Title", "Tiêu đề không được để trống");
            if (string.IsNullOrWhiteSpace(article.Content))
                ModelState.AddModelError("Content", "Nội dung không được để trống");

            // Kiểm tra ảnh tải lên (không bắt buộc có ảnh)
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var ext = System.IO.Path.GetExtension(imageFile.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowed.Contains(ext))
                    ModelState.AddModelError("", "Chỉ chấp nhận ảnh .jpg, .png, .gif, .webp");
                else if (imageFile.ContentLength > 2 * 1024 * 1024)
                    ModelState.AddModelError("", "Ảnh tối đa 2MB");
            }

            if (!ModelState.IsValid)
                return View(article);

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
            return RedirectToAction("Index");
        }
        [AdminAuthorize]
        
        public ActionResult Edit(int id)
        {
            var article = db.Articles.Find(id);
            if (article == null)
                return HttpNotFound();
            return View(ToArticleViewModel(article));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult Edit(Article article, HttpPostedFileBase imageFile)
        {
            if (!ModelState.IsValid)
                return View(article);

            var existing = db.Articles.Find(article.Id);
            if (existing == null)
                return HttpNotFound();

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
            return RedirectToAction("Index");
        }
        [AdminAuthorize]
        public ActionResult Delete(int id)
        {
            var article = db.Articles.Find(id);
            if (article == null)
                return HttpNotFound();
            return View(ToArticleViewModel(article));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult DeleteConfirmed(int id)
        {
            var article = db.Articles.Find(id);
            if (article == null)
            {
                TempData["Error"] = "Bài viết không tồn tại hoặc đã bị xóa";
                return RedirectToAction("Index");
            }

            try
            {
                db.Articles.Remove(article);
                db.SaveChanges();
                TempData["Success"] = "Đã xóa bài viết";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể xóa: bài viết còn bình luận hoặc đánh giá liên quan";
            }
            return RedirectToAction("Index");
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