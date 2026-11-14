
using NewsPortal.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AuthorsController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        private bool IsAdminManagementRequest()
        {
            return (Request.AppRelativeCurrentExecutionFilePath ?? string.Empty)
                .StartsWith("~/Admin/Author", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult CheckAdminManagementAccess()
        {
            if (!IsAdminManagementRequest() || Session["Admin"] != null)
                return null;

            if (string.Equals(Convert.ToString(Session["Role"]), "Editor", StringComparison.OrdinalIgnoreCase))
                return RedirectToRoute("AdminArticles", new { action = "Index" });

            return Session["User"] != null
                ? (ActionResult)RedirectToAction("Index", "Authors")
                : RedirectToAction("Login", "Account");
        }

        private ActionResult RedirectToAuthorsIndex()
        {
            return IsAdminManagementRequest()
                ? (ActionResult)RedirectToRoute("AdminAuthors", new { action = "Index" })
                : RedirectToAction("Index");
        }

        // Danh sách tác giả - mọi người đều được xem
        public ActionResult Index(string search, int? id)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            bool isAdminManagement = IsAdminManagementRequest();
            ViewBag.IsAdminManagement = isAdminManagement;
            var query = db.Authors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();

                query = query.Where(a =>
                    a.AuthorName.Contains(keyword) ||
                    a.Email.Contains(keyword));
            }

            ViewBag.Search = search;

            int? selectedAuthorId = id;
            if (!selectedAuthorId.HasValue && !isAdminManagement && string.IsNullOrWhiteSpace(search))
            {
                var mostPublishedArticles = db.Articles
                    .Where(a => a.Status == "Published")
                    .GroupBy(a => a.AuthorID)
                    .Select(group => new { AuthorID = group.Key, ArticleCount = group.Count() })
                    .OrderByDescending(group => group.ArticleCount)
                    .ThenBy(group => group.AuthorID)
                    .FirstOrDefault();

                selectedAuthorId = mostPublishedArticles != null
                    ? (int?)mostPublishedArticles.AuthorID
                    : db.Authors.OrderBy(a => a.AuthorName).Select(a => (int?)a.AuthorID).FirstOrDefault();
            }

            if (selectedAuthorId.HasValue && !isAdminManagement)
            {
                var selectedAuthor = db.Authors.Find(selectedAuthorId.Value);
                if (selectedAuthor == null) return HttpNotFound();

                var publishedArticles = db.Articles
                    .Where(a => a.AuthorID == selectedAuthorId.Value && a.Status == "Published")
                    .OrderByDescending(a => a.CreatedDate)
                    .ToList();

                ViewBag.SelectedAuthor = selectedAuthor;
                ViewBag.ShowAuthorProfile = true;
                ViewBag.Articles = publishedArticles;
                ViewBag.ArticleCount = publishedArticles.Count;
                ViewBag.SuggestedAuthors = db.Authors
                    .Where(a => a.AuthorID != selectedAuthorId.Value)
                    .OrderBy(a => a.AuthorName)
                    .Take(6)
                    .ToList();
                ViewBag.CategoryNames = db.Categories.ToDictionary(c => c.CategoryID, c => c.CategoryName);
            }

            return View(query.OrderBy(a => a.AuthorName).ToList());
        }

        // Thêm tác giả
        [AdminAuthorize]
        public ActionResult Create()
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminAuthors", new { action = "Create" });

            ViewBag.IsAdminManagement = true;
            return View();
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Author author)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if (author.AuthorName != null)
                author.AuthorName = author.AuthorName.Trim();

            if (author.Email != null)
                author.Email = author.Email.Trim();

            // Kiểm tra email đã tồn tại
            if (db.Authors.Any(a => a.Email == author.Email))
            {
                ModelState.AddModelError(
                    "Email",
                    "Email đã được sử dụng"
                );
            }

            if (ModelState.IsValid)
            {
                db.Authors.Add(author);
                db.SaveChanges();

                TempData["Success"] = "Thêm tác giả thành công";

                return RedirectToAuthorsIndex();
            }

            return View(author);
        }

        // Sửa tác giả
        [AdminAuthorize]
        public ActionResult Edit(int id)
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminAuthors", new { action = "Edit", id = id });

            ViewBag.IsAdminManagement = true;
            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            return View(author);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Author author)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if (author.AuthorName != null)
                author.AuthorName = author.AuthorName.Trim();

            if (author.Email != null)
                author.Email = author.Email.Trim();

            // Kiểm tra email trùng với tác giả khác
            if (db.Authors.Any(a =>
                a.Email == author.Email &&
                a.AuthorID != author.AuthorID))
            {
                ModelState.AddModelError(
                    "Email",
                    "Email đã được sử dụng"
                );
            }

            if (ModelState.IsValid)
            {
                db.Entry(author).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Success"] = "Cập nhật thành công";

                return RedirectToAuthorsIndex();
            }

            return View(author);
        }

        // Xóa tác giả
        [AdminAuthorize]
        public ActionResult Delete(int id)
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminAuthors", new { action = "Delete", id = id });

            ViewBag.IsAdminManagement = true;
            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            return View(author);
        }

        [AdminAuthorize]
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            // Không cho xóa tác giả đang có bài viết
            if (db.Articles.Any(a => a.AuthorID == id))
            {
                TempData["Error"] =
                    "Không thể xóa: tác giả này vẫn còn bài viết";

                return RedirectToAuthorsIndex();
            }

            db.Authors.Remove(author);
            db.SaveChanges();

            TempData["Success"] = "Đã xóa tác giả";

            return RedirectToAuthorsIndex();
        }
        // GET: Authors/Details/5  -> các bài của tác giả
        public ActionResult Details(int? id)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            return IsAdminManagementRequest()
                ? RedirectToRoute("AdminAuthors", new { action = "Index", id = id })
                : RedirectToRoute("PublicAuthor", new { action = "Index", id = id });
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
