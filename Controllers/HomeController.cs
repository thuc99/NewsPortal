
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class HomeController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        public ActionResult Index(string searchString)
        {
            var articles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .Where(a => a.Status == "Published");

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
