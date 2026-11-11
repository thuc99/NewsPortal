
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

        // Danh sách danh mục - mọi người đều được xem
        public ActionResult Index(string search)
        {
            var query = db.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();

                query = query.Where(c =>
                    c.CategoryName.Contains(keyword));
            }

            ViewBag.Search = search;

            return View(query.OrderBy(c => c.CategoryName).ToList());
        }

        // GET: Categories/Create
        [AdminAuthorize]
        public ActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Category category)
        {
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

                return RedirectToAction("Index");
            }

            return View(category);
        }

        // GET: Categories/Edit/5
        [AdminAuthorize]
        public ActionResult Edit(int id)
        {
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

                return RedirectToAction("Index");
            }

            return View(category);
        }

        // GET: Categories/Delete/5
        [AdminAuthorize]
        public ActionResult Delete(int id)
        {
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
            var category = db.Categories.Find(id);

            if (category == null)
                return HttpNotFound();

            // Không cho xóa danh mục đang có bài viết
            if (db.Articles.Any(a => a.CategoryID == id))
            {
                TempData["Error"] =
                    "Không thể xóa: danh mục này vẫn còn bài viết";

                return RedirectToAction("Index");
            }

            db.Categories.Remove(category);
            db.SaveChanges();

            TempData["Success"] = "Đã xóa danh mục";

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
