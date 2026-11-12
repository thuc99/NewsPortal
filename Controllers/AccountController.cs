using System;
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
            // Đang bị khóa tạm vì sai nhiều lần?
            var lockUntil = Session["LockUntil"] as DateTime?;
            if (lockUntil.HasValue && lockUntil.Value > DateTime.Now)
            {
                int mins = (int)Math.Ceiling((lockUntil.Value - DateTime.Now).TotalMinutes);
                ViewBag.Error = "Đăng nhập sai quá nhiều lần. Thử lại sau " + mins + " phút";
                return View();
            }

            username = (username ?? "").Trim();
            if (username.Length == 0 || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đủ tài khoản và mật khẩu";
                return View();
            }

            if (username == "admin" && password == "123456")
            {
                Session["Admin"] = username;
                Session["FailCount"] = 0;
                return RedirectToAction("Index", "Articles");
            }

            int fails = (Session["FailCount"] as int? ?? 0) + 1;
            if (fails >= 5)
            {
                Session["LockUntil"] = DateTime.Now.AddMinutes(5);
                Session["FailCount"] = 0;
                ViewBag.Error = "Sai 5 lần liên tiếp. Tạm khóa đăng nhập 5 phút";
            }
            else
            {
                Session["FailCount"] = fails;
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu! (lần " + fails + "/5)";
            }
            return View();
        }

        public ActionResult Logout()
        {
            Session.Remove("Admin");
            return RedirectToAction("Index", "Articles");
        }
    }
}