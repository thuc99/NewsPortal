using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using NewsPortal.Filters;
using NewsPortal.Helpers;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class AdminController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        // GET: Admin (Bảng điều khiển Quản trị viên)
        [AdminAuthorize]
        public ActionResult Index()
        {
            AdminDbInitializer.EnsureAdminCreated();

            var categoryStats = db.Categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryArticleCountItem
                {
                    CategoryID = c.CategoryID,
                    CategoryName = c.CategoryName,
                    TotalCount = c.Articles.Count(),
                    PublishedCount = c.Articles.Count(a => a.Status == "Published"),
                    DraftCount = c.Articles.Count(a => a.Status == "Draft")
                })
                .ToList();

            var recentArticles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToList();

            var dashboard = new AdminDashboardViewModel
            {
                AdminFullName = AdminSessionHelper.GetCurrentAdminName(Session),
                AdminUsername = Session[AdminSessionHelper.KeyUsername] as string,
                TotalArticles = db.Articles.Count(),
                PublishedArticles = db.Articles.Count(a => a.Status == "Published"),
                DraftArticles = db.Articles.Count(a => a.Status == "Draft"),
                TotalCategories = db.Categories.Count(),
                TotalAuthors = db.Authors.Count(),
                TotalAdmins = db.Users.Count(u => u.Role == "Admin" && u.IsActive),
                CategoryStats = categoryStats,
                RecentArticles = recentArticles
            };

            return View(dashboard);
        }

        // GET: Admin/Login
        [AllowAnonymous]
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            AdminDbInitializer.EnsureAdminCreated();

            if (AdminSessionHelper.IsAdminLoggedIn(HttpContext))
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Admin");
            }

            var model = new AdminLoginViewModel
            {
                ReturnUrl = returnUrl
            };
            return View(model);
        }

        // POST: Admin/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(AdminLoginViewModel model)
        {
            AdminDbInitializer.EnsureAdminCreated();

            if (model == null)
            {
                return View(new AdminLoginViewModel());
            }

            if (!string.IsNullOrWhiteSpace(model.UsernameOrEmail))
            {
                model.UsernameOrEmail = model.UsernameOrEmail.Trim();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string loginKey = model.UsernameOrEmail.ToLower();

            // Truy vấn LINQ tìm tài khoản theo Username hoặc Email
            var user = db.Users.FirstOrDefault(u =>
                u.Username.ToLower() == loginKey ||
                (u.Email != null && u.Email.ToLower() == loginKey));

            bool needsRehash;
            if (user == null || !SecurityHelper.VerifyPassword(model.Password, user.PasswordHash, out needsRehash))
            {
                ModelState.AddModelError("", "Tên đăng nhập/Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // Kiểm tra tài khoản bị khóa
            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản này đã bị vô hiệu hóa. Vui lòng liên hệ quản trị hệ thống.");
                return View(model);
            }

            // Kiểm tra quyền Admin
            if (!string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Tài khoản của bạn không có quyền Quản trị viên (Admin).");
                return View(model);
            }

            // Tự động nâng cấp lên SHA-256 nếu mật khẩu cũ chưa mã hóa
            if (needsRehash)
            {
                user.PasswordHash = SecurityHelper.HashPassword(model.Password);
                db.SaveChanges();
            }

            // Khởi tạo phiên đăng nhập Session + FormsAuthentication Cookie
            AdminSessionHelper.SetAdminSession(Session, user);
            FormsAuthentication.SetAuthCookie(user.Username, model.RememberMe);

            TempData["Success"] = "Đăng nhập thành công! Xin chào " + user.FullName + ".";

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Admin");
        }

        // POST: Admin/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout(FormCollection form)
        {
            AdminSessionHelper.ClearAdminSession(Session);
            TempData["Success"] = "Bạn đã đăng xuất khỏi hệ thống quản trị.";
            return RedirectToAction("Login", "Admin");
        }

        // GET: Admin/Logout (Hỗ trợ đăng xuất nhanh qua đường dẫn)
        [HttpGet]
        public ActionResult Logout()
        {
            AdminSessionHelper.ClearAdminSession(Session);
            TempData["Success"] = "Bạn đã đăng xuất khỏi hệ thống quản trị.";
            return RedirectToAction("Login", "Admin");
        }

        // GET: Admin/ChangePassword
        [AdminAuthorize]
        [HttpGet]
        public ActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: Admin/ChangePassword
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int? adminId = AdminSessionHelper.GetCurrentAdminId(Session);
            if (!adminId.HasValue)
            {
                return RedirectToAction("Login", "Admin");
            }

            var user = db.Users.Find(adminId.Value);
            if (user == null)
            {
                AdminSessionHelper.ClearAdminSession(Session);
                return RedirectToAction("Login", "Admin");
            }

            bool unusedRehash;
            if (!SecurityHelper.VerifyPassword(model.CurrentPassword, user.PasswordHash, out unusedRehash))
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
                return View(model);
            }

            if (model.CurrentPassword == model.NewPassword)
            {
                ModelState.AddModelError("NewPassword", "Mật khẩu mới phải khác mật khẩu hiện tại.");
                return View(model);
            }

            user.PasswordHash = SecurityHelper.HashPassword(model.NewPassword);
            db.SaveChanges();

            TempData["Success"] = "Đổi mật khẩu quản trị viên thành công!";
            return RedirectToAction("Index", "Admin");
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
