using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            if (username == "admin" && password == "123456")
            {
                Session["Admin"] = username;
                return RedirectToAction("Index", "Articles");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Remove("Admin");
            return RedirectToAction("Index", "Articles");
        }
    }
}