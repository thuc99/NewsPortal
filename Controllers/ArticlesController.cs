
using System;
using System.IO;
using System.Linq;
using System.Data.Entity;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class ArticlesController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        // GET: Articles
        // Hiển thị danh sách và tìm kiếm bài viết
        public ActionResult Index(string searchString)
        {
            var articles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .AsQueryable();

            // Tìm kiếm theo tiêu đề hoặc nội dung
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                articles = articles.Where(a =>
                    a.Title.Contains(searchString) ||
                    a.Content.Contains(searchString)
                );
            }

            ViewBag.SearchString = searchString;

            var result = articles
                .OrderByDescending(a => a.CreatedDate)
                .ToList();

            return View(result);
        }

        // GET: Articles/Details/5
        public ActionResult Details(int id)
        {
            var article = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .FirstOrDefault(a => a.Id == id);

            if (article == null)
            {
                return HttpNotFound();
            }

            return View(article);
        }

        // GET: Articles/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Articles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Article article, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(imageFile.FileName);

                    string path = Path.Combine(
                        Server.MapPath("~/Content/Images/"),
                        fileName
                    );

                    imageFile.SaveAs(path);
                    article.Image = fileName;
                }
                else
                {
                    article.Image = "no-image.jpg";
                }

                article.CreatedDate = DateTime.Now;

                db.Articles.Add(article);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(article);
        }

        // GET: Articles/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            var article = db.Articles.Find(id);

            if (article == null)
            {
                return HttpNotFound();
            }

            return View(article);
        }

        // POST: Articles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Article article, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                var existing = db.Articles.Find(article.Id);

                if (existing == null)
                {
                    return HttpNotFound();
                }

                existing.Title = article.Title;
                existing.Content = article.Content;

                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(imageFile.FileName);

                    string path = Path.Combine(
                        Server.MapPath("~/Content/Images/"),
                        fileName
                    );

                    imageFile.SaveAs(path);
                    existing.Image = fileName;
                }

                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(article);
        }

        // GET: Articles/Delete/5
        public ActionResult Delete(int id)
        {
            var article = db.Articles.Find(id);

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

            if (article != null)
            {
                db.Articles.Remove(article);
                db.SaveChanges();
            }

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
