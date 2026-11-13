using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class AccountController : Controller
    {
        // Lưu danh sách tài khoản User đã đăng ký trong bộ nhớ ứng dụng
        private static List<UserAccount> _registeredUsers = new List<UserAccount>();

        public ActionResult Login()
        {
            if (Session["Admin"] != null)
            {
                return RedirectToAction("Index", "Admin");
            }
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

            // 1. Đăng nhập quyền Admin -> chuyển thẳng về Dashboard Admin
            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "123456")
            {
                Session.Remove("User");
                Session.Remove("UserFullName");
                Session["Admin"] = "admin";
                Session["FailCount"] = 0;
                return RedirectToAction("Index", "Admin");
            }

            // 2. Đăng nhập quyền User thường
            var user = _registeredUsers.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.Password == password);

            if (user != null)
            {
                Session.Remove("Admin");
                Session["User"] = user.Username;
                Session["UserFullName"] = user.FullName;
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
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu! (Lần " + fails + "/5)";
            }
            return View();
        }

        // GET: /Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string fullName, string username, string email, string password, string confirmPassword)
        {
            fullName = (fullName ?? "").Trim();
            username = (username ?? "").Trim();
            email = (email ?? "").Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ họ tên, tài khoản và mật khẩu.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                _registeredUsers.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                ViewBag.Error = "Tên đăng nhập này đã tồn tại. Vui lòng chọn tên khác.";
                return View();
            }

            _registeredUsers.Add(new UserAccount
            {
                FullName = fullName,
                Username = username,
                Email = email,
                Password = password
            });

            TempData["Success"] = "Đăng ký thành công! Mời bạn đăng nhập.";
            return RedirectToAction("Login");
        }

        public ActionResult Logout()
        {
            Session.Remove("Admin");
            Session.Remove("User");
            Session.Remove("UserFullName");
            return RedirectToAction("Index", "Articles");
        }
    }
}