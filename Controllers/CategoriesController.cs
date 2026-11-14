
using NewsPortal.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class CategoriesController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        private bool IsAdminManagementRequest()
        {
            return (Request.AppRelativeCurrentExecutionFilePath ?? string.Empty)
                .StartsWith("~/Admin/Category", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult CheckAdminManagementAccess()
        {
            if (!IsAdminManagementRequest() || Session["Admin"] != null)
                return null;

            if (string.Equals(Convert.ToString(Session["Role"]), "Editor", StringComparison.OrdinalIgnoreCase))
                return RedirectToRoute("AdminArticles", new { action = "Index" });

            return Session["User"] != null
                ? (ActionResult)RedirectToAction("Index", "Categories")
                : RedirectToAction("Login", "Account");
        }

        private ActionResult RedirectToCategoriesIndex()
        {
            return IsAdminManagementRequest()
                ? (ActionResult)RedirectToRoute("AdminCategories", new { action = "Index" })
                : RedirectToAction("Index");
        }

        // Danh sách danh mục - mọi người đều được xem
        public ActionResult Index(string search)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            var query = db.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();

                query = query.Where(c =>
                    c.CategoryName.Contains(keyword) ||
                    c.Description.Contains(keyword));
            }

            ViewBag.Search = search;

            return View(query.OrderBy(c => c.CategoryName).ToList());
        }

        [ChildActionOnly]
        public ActionResult Navigation(string activeCategoryId)
        {
            var categories = db.Categories
                .OrderBy(c => c.CategoryName)
                .ToList();

            ViewBag.ActiveCategoryId = activeCategoryId;
            return PartialView("_Navigation", categories);
        }

        // GET: Categories/Create
        [AdminAuthorize]
        public ActionResult Create()
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminCategories", new { action = "Create" });

            ViewBag.IsAdminManagement = true;
            return View();
        }

        // POST: Categories/Create
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Category category)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if (category.CategoryName != null)
                category.CategoryName = category.CategoryName.Trim();

            // Kiểm tra tên danh mục trùng
            if (db.Categories.Any(c =>
                c.CategoryName == category.CategoryName))
            {
                ModelState.AddModelError(
                    "CategoryName",
                    "Tên danh mục đã tồn tại");
            }

            if (ModelState.IsValid)
            {
                db.Categories.Add(category);
                db.SaveChanges();

                TempData["Success"] = "Thêm danh mục thành công";

                return RedirectToCategoriesIndex();
            }

            return View(category);
        }

        // GET: Categories/Edit/5
        [AdminAuthorize]
        public ActionResult Edit(int id)
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminCategories", new { action = "Edit", id = id });

            ViewBag.IsAdminManagement = true;
            var category = db.Categories.Find(id);

            if (category == null)
                return HttpNotFound();

            return View(category);
        }

        // POST: Categories/Edit/5
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Category category)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if (category.CategoryName != null)
                category.CategoryName = category.CategoryName.Trim();

            // Kiểm tra trùng tên với danh mục khác
            if (db.Categories.Any(c =>
                c.CategoryName == category.CategoryName &&
                c.CategoryID != category.CategoryID))
            {
                ModelState.AddModelError(
                    "CategoryName",
                    "Tên danh mục đã tồn tại");
            }

            if (ModelState.IsValid)
            {
                db.Entry(category).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Success"] = "Cập nhật thành công";

                return RedirectToCategoriesIndex();
            }

            return View(category);
        }

        // GET: Categories/Delete/5
        [AdminAuthorize]
        public ActionResult Delete(int id)
        {
            if (!IsAdminManagementRequest())
                return RedirectToRoute("AdminCategories", new { action = "Delete", id = id });

            ViewBag.IsAdminManagement = true;
            var category = db.Categories.Find(id);

            if (category == null)
                return HttpNotFound();

            ViewBag.ArticleCount =
                db.Articles.Count(a => a.CategoryID == id);

            return View(category);
        }

        // POST: Categories/Delete/5
        [AdminAuthorize]
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            var category = db.Categories.Find(id);

            if (category == null)
                return HttpNotFound();

            // Không cho xóa danh mục đang có bài viết
            if (db.Articles.Any(a => a.CategoryID == id))
            {
                TempData["Error"] =
                    "Không thể xóa: danh mục này vẫn còn bài viết";

                return RedirectToCategoriesIndex();
            }

            db.Categories.Remove(category);
            db.SaveChanges();

            TempData["Success"] = "Đã xóa danh mục";

            return RedirectToCategoriesIndex();
        }

        public ActionResult Details(int? id)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var category = db.Categories.Find(id);
            if (category == null) return HttpNotFound();

            ViewBag.Articles = db.Articles
                .Where(a => a.CategoryID == id && a.Status == "Published")
                .OrderByDescending(a => a.CreatedDate)
                .ToList();

            // Tên tác giả để hiện trên thẻ bài
            ViewBag.AuthorNames = db.Authors.ToDictionary(x => x.AuthorID, x => x.AuthorName);
            return View(category);
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
