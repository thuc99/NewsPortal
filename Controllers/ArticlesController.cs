using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class ArticlesController : Controller
    {
        // Danh sách lưu trữ tạm thời trong bộ nhớ
        private static List<Article> dbArticles = new List<Article>()
        {
            new Article { Id = 1, Title = "Giới thiệu trang tin tức NewsPortal", Content = "Nội dung chào mừng...", Image = "default.jpg", CreatedDate = DateTime.Now },
            new Article { Id = 2, Title = "Cập nhật công nghệ lập trình Web", Content = "ASP.NET MVC...", Image = "default.jpg", CreatedDate = DateTime.Now }
        };

        // GET: Articles
        public ActionResult Index()
        {
            return View(dbArticles);
        }

        // GET: Articles/Details/5
        public ActionResult Details(int id)
        {
            var article = dbArticles.FirstOrDefault(x => x.Id == id);
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
                // Xử lý upload ảnh
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(imageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Content/Images/"), fileName);
                    imageFile.SaveAs(path);
                    article.Image = fileName;
                }
                else
                {
                    article.Image = "no-image.jpg";
                }

                article.Id = dbArticles.Count > 0 ? dbArticles.Max(x => x.Id) + 1 : 1;
                article.CreatedDate = DateTime.Now;
                dbArticles.Add(article);
                return RedirectToAction("Index");
            }

            return View(article);
        }

        // GET: Articles/Edit/5
        public ActionResult Edit(int id)
        {
            var article = dbArticles.FirstOrDefault(x => x.Id == id);
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
                var existing = dbArticles.FirstOrDefault(x => x.Id == article.Id);
                if (existing != null)
                {
                    existing.Title = article.Title;
                    existing.Content = article.Content;

                    // Nếu chọn ảnh mới khi sửa
                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string fileName = Path.GetFileName(imageFile.FileName);
                        string path = Path.Combine(Server.MapPath("~/Content/Images/"), fileName);
                        imageFile.SaveAs(path);
                        existing.Image = fileName;
                    }
                }
                return RedirectToAction("Index");
            }
            return View(article);
        }

        // GET: Articles/Delete/5
        public ActionResult Delete(int id)
        {
            var article = dbArticles.FirstOrDefault(x => x.Id == id);
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
            var article = dbArticles.FirstOrDefault(x => x.Id == id);
            if (article != null)
            {
                dbArticles.Remove(article);
            }
            return RedirectToAction("Index");
        }
    }
}