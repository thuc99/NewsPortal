using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class HomeController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        public ActionResult Index(string searchString, int? categoryId, int? authorId,
            DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            const int pageSize = 6;
            page = Math.Max(page, 1);

            var query = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .Where(a => a.Status == "Published");

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(a =>
                    a.Title.Contains(searchString) ||
                    a.Summary.Contains(searchString) ||
                    a.Content.Contains(searchString));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.CategoryID == categoryId.Value);
            }

            if (authorId.HasValue)
            {
                query = query.Where(a => a.AuthorID == authorId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(a => a.CreatedDate < endDate);
            }

            var totalArticles = query.Count();
            var totalPages = Math.Max((int)Math.Ceiling((double)totalArticles / pageSize), 1);
            page = Math.Min(page, totalPages);

            var result = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.AuthorId = authorId;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.Categories = db.Categories.OrderBy(c => c.CategoryName).ToList();
            ViewBag.Authors = db.Authors.OrderBy(a => a.AuthorName).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalArticles = totalArticles;

            return View(result);
        }

        public ActionResult Details(int id)
        {
            var article = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .FirstOrDefault(a => a.ArticleID == id && a.Status == "Published");

            if (article == null)
            {
                return HttpNotFound();
            }

            ViewBag.Comments = db.Comments
                .Where(c => c.ArticleID == id)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();

            return View(article);
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
                return RedirectToAction("Details", new { id = articleId });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["CommentError"] = "Vui lòng nhập nội dung bình luận.";
                return RedirectToAction("Details", new { id = articleId });
            }

            if (commenterName.Length > 100 || content.Length > 1000)
            {
                TempData["CommentError"] = "Tên tối đa 100 ký tự và bình luận tối đa 1000 ký tự.";
                return RedirectToAction("Details", new { id = articleId });
            }

            var articleExists = db.Articles.Any(a =>
                a.ArticleID == articleId && a.Status == "Published");

            if (!articleExists)
            {
                return HttpNotFound();
            }

            db.Comments.Add(new Comments
            {
                ArticleID = articleId,
                CommenterName = commenterName,
                Content = content,
                CreatedDate = DateTime.Now
            });

            db.SaveChanges();
            TempData["CommentSuccess"] = "Bình luận của bạn đã được đăng.";

            return RedirectToAction("Details", new { id = articleId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
