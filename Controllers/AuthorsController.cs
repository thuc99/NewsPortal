using NewsPortal.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AuthorsController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        public ActionResult Index(string search)
        {
            var query = db.Authors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                query = query.Where(a => a.AuthorName.Contains(keyword)
                                      || a.Email.Contains(keyword));          // LINQ
            }

            ViewBag.Search = search;
            return View(query.OrderBy(a => a.AuthorName).ToList());
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Author author)
        {
            if (author.AuthorName != null) author.AuthorName = author.AuthorName.Trim();
            if (author.Email != null) author.Email = author.Email.Trim();

            // Business rule 1: không trùng email
            if (db.Authors.Any(a => a.Email == author.Email))
                ModelState.AddModelError("Email", "Email đã được sử dụng");

            if (ModelState.IsValid)
            {
                db.Authors.Add(author);
                db.SaveChanges();
                TempData["Success"] = "Thêm tác giả thành công";
                return RedirectToAction("Index");
            }
            return View(author);
        }

        public ActionResult Edit(int id)
        {
            var author = db.Authors.Find(id);
            if (author == null) return HttpNotFound();
            return View(author);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Author author)
        {
            if (author.AuthorName != null) author.AuthorName = author.AuthorName.Trim();
            if (author.Email != null) author.Email = author.Email.Trim();

            if (db.Authors.Any(a => a.Email == author.Email
                                 && a.AuthorID != author.AuthorID))
                ModelState.AddModelError("Email", "Email đã được sử dụng");

            if (ModelState.IsValid)
            {
                db.Entry(author).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Cập nhật thành công";
                return RedirectToAction("Index");
            }
            return View(author);
        }

        public ActionResult Delete(int id)
        {
            var author = db.Authors.Find(id);
            if (author == null) return HttpNotFound();
            return View(author);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var author = db.Authors.Find(id);
            if (author == null) return HttpNotFound();

            // Business rule 2: còn bài viết thì không được xóa
            if (db.Articles.Any(a => a.AuthorID == id))
            {
                TempData["Error"] = "Không thể xóa: tác giả này vẫn còn bài viết";
                return RedirectToAction("Index");
            }

            db.Authors.Remove(author);
            db.SaveChanges();
            TempData["Success"] = "Đã xóa tác giả";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}