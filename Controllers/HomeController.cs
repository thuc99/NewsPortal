
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class HomeController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        public ActionResult Index()
        {
            var articles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .Where(a => a.Status == "Published")
                .OrderByDescending(a => a.CreatedDate)
                .ToList();

            return View(articles);
        }
    }
}