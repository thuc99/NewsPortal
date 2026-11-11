using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Filters;
using NewsPortal.Helpers;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    [AdminAuthorize]
    public class ArticlesController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        private static readonly string[] ValidStatuses = new[] { "Draft", "Published" };

        private void PopulateDropDowns(int? selectedCategoryId = null, int? selectedAuthorId = null, string selectedStatus = "Draft")
        {
            ViewBag.CategoryID = new SelectList(
                db.Categories.OrderBy(c => c.CategoryName).ToList(),
                "CategoryID",
                "CategoryName",
                selectedCategoryId
            );

            ViewBag.AuthorID = new SelectList(
                db.Authors.OrderBy(a => a.AuthorName).ToList(),
                "AuthorID",
                "AuthorName",
                selectedAuthorId
            );

            var statusItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "Draft", Text = "Draft (Bản nháp)", Selected = (selectedStatus == "Draft") },
                new SelectListItem { Value = "Published", Text = "Published (Đã xuất bản)", Selected = (selectedStatus == "Published") }
            };
            ViewBag.StatusList = statusItems;
        }

        // GET: Articles
        public ActionResult Index(string search, int? categoryId, int? authorId, string status)
        {
            var query = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                query = query.Where(a => a.Title.Contains(keyword)
                                      || (a.Summary != null && a.Summary.Contains(keyword)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.CategoryID == categoryId.Value);
            }

            if (authorId.HasValue)
            {
                query = query.Where(a => a.AuthorID == authorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && ValidStatuses.Contains(status))
            {
                query = query.Where(a => a.Status == status);
            }

            ViewBag.Search = search;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedAuthorId = authorId;
            ViewBag.SelectedStatus = status;

            ViewBag.Categories = new SelectList(db.Categories.OrderBy(c => c.CategoryName).ToList(), "CategoryID", "CategoryName", categoryId);
            ViewBag.Authors = new SelectList(db.Authors.OrderBy(a => a.AuthorName).ToList(), "AuthorID", "AuthorName", authorId);

            // Thống kê nhanh theo trạng thái Draft / Published
            ViewBag.TotalCount = db.Articles.Count();
            ViewBag.PublishedCount = db.Articles.Count(a => a.Status == "Published");
            ViewBag.DraftCount = db.Articles.Count(a => a.Status == "Draft");

            var articles = query.OrderByDescending(a => a.CreatedDate).ToList();
            return View(articles);
        }

        // GET: Articles/Details/5 (Cho phép độc giả xem bài viết Published; bài Draft chỉ Admin mới xem được)
        [AllowAnonymous]
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var article = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .FirstOrDefault(a => a.ArticleID == id.Value);

            if (article == null)
            {
                return HttpNotFound();
            }

            bool isAdmin = AdminSessionHelper.IsAdminLoggedIn(HttpContext);
            if (!isAdmin && article.Status != "Published")
            {
                return HttpNotFound();
            }

            ViewBag.IsAdmin = isAdmin;
            return View(article);
        }

        // GET: Articles/Create
        public ActionResult Create()
        {
            if (!db.Categories.Any() || !db.Authors.Any())
            {
                TempData["Error"] = "Vui lòng tạo ít nhất 1 Danh mục và 1 Tác giả trước khi thêm bài viết.";
            }

            PopulateDropDowns(null, null, "Draft");
            var model = new Articles
            {
                Status = "Draft"
            };
            return View(model);
        }

        // POST: Articles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "CategoryID,AuthorID,Title,Summary,Content,Status")] Articles article)
        {
            if (article.Title != null) article.Title = article.Title.Trim();
            if (article.Summary != null) article.Summary = article.Summary.Trim();
            if (article.Content != null) article.Content = article.Content.Trim();
            if (string.IsNullOrWhiteSpace(article.Status))
            {
                article.Status = "Draft";
            }
            else
            {
                article.Status = article.Status.Trim();
            }

            // Kiểm tra trạng thái Draft / Published hợp lệ
            if (!ValidStatuses.Contains(article.Status))
            {
                ModelState.AddModelError("Status", "Trạng thái không hợp lệ. Chỉ chấp nhận 'Draft' hoặc 'Published'.");
            }

            // Kiểm tra danh mục & tác giả tồn tại
            if (!db.Categories.Any(c => c.CategoryID == article.CategoryID))
            {
                ModelState.AddModelError("CategoryID", "Danh mục được chọn không tồn tại.");
            }

            if (!db.Authors.Any(a => a.AuthorID == article.AuthorID))
            {
                ModelState.AddModelError("AuthorID", "Tác giả được chọn không tồn tại.");
            }

            if (ModelState.IsValid)
            {
                article.CreatedDate = DateTime.Now;
                article.UpdatedDate = null;

                db.Articles.Add(article);
                db.SaveChanges();
                TempData["Success"] = "Thêm bài viết mới thành công!";
                return RedirectToAction("Index");
            }

            PopulateDropDowns(article.CategoryID, article.AuthorID, article.Status);
            return View(article);
        }

        // GET: Articles/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var article = db.Articles.Find(id.Value);
            if (article == null)
            {
                return HttpNotFound();
            }

            PopulateDropDowns(article.CategoryID, article.AuthorID, article.Status);
            return View(article);
        }

        // POST: Articles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ArticleID,CategoryID,AuthorID,Title,Summary,Content,Status")] Articles article)
        {
            var existing = db.Articles.Find(article.ArticleID);
            if (existing == null)
            {
                return HttpNotFound();
            }

            if (article.Title != null) article.Title = article.Title.Trim();
            if (article.Summary != null) article.Summary = article.Summary.Trim();
            if (article.Content != null) article.Content = article.Content.Trim();
            if (article.Status != null) article.Status = article.Status.Trim();

            // Kiểm tra trạng thái Draft / Published hợp lệ
            if (string.IsNullOrWhiteSpace(article.Status) || !ValidStatuses.Contains(article.Status))
            {
                ModelState.AddModelError("Status", "Trạng thái không hợp lệ. Chỉ chấp nhận 'Draft' hoặc 'Published'.");
            }

            if (!db.Categories.Any(c => c.CategoryID == article.CategoryID))
            {
                ModelState.AddModelError("CategoryID", "Danh mục được chọn không tồn tại.");
            }

            if (!db.Authors.Any(a => a.AuthorID == article.AuthorID))
            {
                ModelState.AddModelError("AuthorID", "Tác giả được chọn không tồn tại.");
            }

            if (ModelState.IsValid)
            {
                existing.Title = article.Title;
                existing.Summary = article.Summary;
                existing.Content = article.Content;
                existing.CategoryID = article.CategoryID;
                existing.AuthorID = article.AuthorID;
                existing.Status = article.Status;
                existing.UpdatedDate = DateTime.Now;

                db.SaveChanges();
                TempData["Success"] = "Cập nhật bài viết thành công!";
                return RedirectToAction("Index");
            }

            article.CreatedDate = existing.CreatedDate;
            article.UpdatedDate = existing.UpdatedDate;
            PopulateDropDowns(article.CategoryID, article.AuthorID, article.Status);
            return View(article);
        }

        // POST: Articles/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(int id)
        {
            var article = db.Articles.Find(id);
            if (article == null)
            {
                return HttpNotFound();
            }

            article.Status = (article.Status == "Published") ? "Draft" : "Published";
            article.UpdatedDate = DateTime.Now;
            db.SaveChanges();

            TempData["Success"] = article.Status == "Published"
                ? $"Đã xuất bản (Published) bài viết: \"{article.Title}\""
                : $"Đã chuyển bài viết \"{article.Title}\" về Bản nháp (Draft)";

            return RedirectToAction("Index");
        }

        // GET: Articles/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var article = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .FirstOrDefault(a => a.ArticleID == id.Value);

            if (article == null)
            {
                return HttpNotFound();
            }

            return View(article);
        }

        // POST: Articles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var article = db.Articles.Find(id);
            if (article == null)
            {
                return HttpNotFound();
            }

            db.Articles.Remove(article);
            db.SaveChanges();
            TempData["Success"] = "Đã xóa bài viết thành công!";
            return RedirectToAction("Index");
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
