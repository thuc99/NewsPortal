using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    // Chỉ cho người đã đăng nhập (Session["User"] hoặc Session["Admin"])
    public class LoggedInAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            var s = ctx.HttpContext.Session;
            if (s["User"] == null && s["Admin"] == null)
            {
                ctx.Result = new RedirectResult("/Account/Login?returnUrl=" +
                    HttpUtility.UrlEncode(ctx.HttpContext.Request.RawUrl));
            }
        }
    }

    [LoggedIn]
    public class ProfileController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        // Tên đăng nhập luôn lấy từ Session, KHÔNG nhận từ form hay URL
        private Users CurrentUser()
        {
            string un = (Session["User"] ?? Session["Admin"]) as string;
            if (string.IsNullOrEmpty(un)) return null;
            var u = db.Users.FirstOrDefault(x => x.Username == un);
            if (u != null) Session["UserAvatar"] = u.AvatarUrl;
            return u;
        }

        // ---------- 1. THÔNG TIN TÀI KHOẢN ----------
        public ActionResult Info()
        {
            var u = CurrentUser();
            if (u == null)
            {
                TempData["Error"] = "Tài khoản này không có hồ sơ trong hệ thống";
                return RedirectToAction("Index", "Articles");
            }
            return View(ToVm(u));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Info(ProfileInfoViewModel vm)
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();
            vm.Username = u.Username;
            vm.AvatarUrl = u.AvatarUrl;   // ảnh chỉ đổi qua nút máy ảnh

            vm.FullName = Regex.Replace((vm.FullName ?? "").Trim(), @"\s+", " ");
            vm.Email = (vm.Email ?? "").Trim().ToLower();
            vm.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
            vm.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();

            if (vm.BirthDate.HasValue &&
                (vm.BirthDate.Value > DateTime.Today || vm.BirthDate.Value.Year < 1900))
                ModelState.AddModelError("BirthDate", "Ngày sinh không hợp lệ");

            if (!string.IsNullOrEmpty(vm.Gender) && !new[] { "Nam", "Nữ", "Khác" }.Contains(vm.Gender))
                ModelState.AddModelError("Gender", "Giới tính không hợp lệ");

            int uid = u.UserId;
            if (ModelState.IsValidField("Email") &&
                db.Users.Any(x => x.Email == vm.Email && x.UserId != uid))
                ModelState.AddModelError("Email", "Email đã được tài khoản khác sử dụng");

            if (!ModelState.IsValid) return View(vm);

            u.FullName = vm.FullName;
            u.BirthDate = vm.BirthDate;
            u.Gender = vm.Gender;
            u.Email = vm.Email;
            u.Phone = vm.Phone;
            u.Address = vm.Address;

            try
            {
                db.SaveChanges();
                Session["UserFullName"] = u.FullName;   // cập nhật tên hiển thị
                TempData["Success"] = "Cập nhật thông tin thành công";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể lưu thông tin, vui lòng thử lại";
            }
            return RedirectToAction("Info");
        }

        // ---------- 2. ẢNH ĐẠI DIỆN ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Avatar(HttpPostedFileBase file)
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();

            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Vui lòng chọn một ảnh";
                return RedirectToAction("Info");
            }

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
            {
                TempData["Error"] = "Chỉ chấp nhận ảnh .jpg, .png, .gif, .webp";
                return RedirectToAction("Info");
            }
            if (file.ContentLength > 2 * 1024 * 1024)
            {
                TempData["Error"] = "Ảnh tối đa 2MB";
                return RedirectToAction("Info");
            }

            // Kiểm tra đúng là file ảnh (không chỉ đổi đuôi)
            if (ext != ".webp")
            {
                try { using (System.Drawing.Image.FromStream(file.InputStream)) { } }
                catch
                {
                    TempData["Error"] = "File không phải ảnh hợp lệ";
                    return RedirectToAction("Info");
                }
                file.InputStream.Position = 0;
            }

            string folder = Server.MapPath("~/Content/Avatars/");
            Directory.CreateDirectory(folder);
            string name = Guid.NewGuid().ToString("N") + ext;   // tên ngẫu nhiên
            file.SaveAs(Path.Combine(folder, name));

            string old = u.AvatarUrl;
            u.AvatarUrl = "~/Content/Avatars/" + name;
            try
            {
                db.SaveChanges();
                Session["UserAvatar"] = u.AvatarUrl;
                if (!string.IsNullOrEmpty(old) && old.StartsWith("~/Content/Avatars/"))
                {
                    string oldPath = Server.MapPath(old);
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }
                TempData["Success"] = "Đã cập nhật ảnh đại diện";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể lưu ảnh, vui lòng thử lại";
            }
            return RedirectToAction("Info");
        }

        private static ProfileInfoViewModel ToVm(Users u)
        {
            return new ProfileInfoViewModel
            {
                Username = u.Username,
                FullName = u.FullName,
                BirthDate = u.BirthDate,
                Gender = u.Gender,
                Email = u.Email,
                Phone = u.Phone,
                Address = u.Address,
                AvatarUrl = u.AvatarUrl
            };
        }

        // ---------- SIDEBAR DÙNG CHUNG ----------
        [ChildActionOnly]
        public ActionResult Sidebar(string activeTab = "Info")
        {
            var u = CurrentUser();
            var vm = new ProfileSidebarViewModel
            {
                DisplayName = u != null && !string.IsNullOrWhiteSpace(u.FullName)
                    ? u.FullName
                    : (u != null ? u.Username : "Người dùng"),
                AvatarUrl = u != null ? u.AvatarUrl : null,
                ActiveTab = activeTab
            };
            return PartialView("_Sidebar", vm);
        }

        // ---------- 3. ĐỔI MẬT KHẨU ----------
        [HttpGet]
        public ActionResult ChangePassword()
        {
            if (CurrentUser() == null) return RedirectToAction("Info");
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel vm)
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();
            if (!ModelState.IsValid) return View(vm);

            string rawCurrent = (vm.CurrentPassword ?? "").Trim();
            string rawNew = (vm.NewPassword ?? "").Trim();

            if (!VerifyUserPassword(rawCurrent, u.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
                return View(vm);
            }

            if (rawCurrent == rawNew)
            {
                ModelState.AddModelError("NewPassword", "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
                return View(vm);
            }

            u.PasswordHash = HashUserPassword(rawNew);
            try
            {
                db.SaveChanges();
                TempData["Success"] = "Đổi mật khẩu thành công.";
                return RedirectToAction("ChangePassword");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Không thể cập nhật mật khẩu, vui lòng thử lại.";
                return View(vm);
            }
        }

        private static bool VerifyUserPassword(string inputPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            return string.Equals(storedHash, HashUserPassword(inputPassword), StringComparison.OrdinalIgnoreCase)
                || storedHash == inputPassword;
        }

        private static string HashUserPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes((password ?? "").Trim()));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // ---------- 4. HOẠT ĐỘNG BÌNH LUẬN ----------
        public ActionResult Comments()
        {
            var u = CurrentUser();
            if (u == null) return RedirectToAction("Info");

            int uid = u.UserId;
            var list = db.Comments
                .Where(c => c.UserId == uid)
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new UserCommentItemViewModel
                {
                    CommentId = c.CommentID,
                    ArticleId = c.ArticleID,
                    ArticleTitle = c.Articles != null ? c.Articles.Title : ("Bài viết #" + c.ArticleID),
                    Content = c.Content,
                    Status = c.Status,
                    CreatedAt = c.CreatedDate
                })
                .ToList();

            return View(list);
        }

        // ---------- 5. TIN ĐÃ LƯU ----------
        public ActionResult Saved()
        {
            var u = CurrentUser();
            if (u == null) return RedirectToAction("Info");

            int uid = u.UserId;
            var list = db.SavedArticles
                .Where(s => s.UserId == uid)
                .OrderByDescending(s => s.SavedDate)
                .Select(s => new SavedArticleItemViewModel
                {
                    SavedId = s.Id,
                    ArticleId = s.ArticleId,
                    Title = s.Articles != null ? s.Articles.Title : ("Bài viết #" + s.ArticleId),
                    Summary = s.Articles != null ? s.Articles.Summary : "",
                    ImageUrl = s.Articles != null ? s.Articles.ImageUrl : null,
                    SavedAt = s.SavedDate
                })
                .ToList();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unsave(int articleId)
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();

            int uid = u.UserId;
            var item = db.SavedArticles.FirstOrDefault(s => s.UserId == uid && s.ArticleId == articleId);
            if (item != null)
            {
                db.SavedArticles.Remove(item);
                db.SaveChanges();
                TempData["Success"] = "Đã bỏ lưu bài viết.";
            }
            return RedirectToAction("Saved");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleSave(int articleId, string returnUrl)
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();

            int uid = u.UserId;
            var existing = db.SavedArticles.FirstOrDefault(s => s.UserId == uid && s.ArticleId == articleId);
            if (existing != null)
            {
                db.SavedArticles.Remove(existing);
                TempData["Success"] = "Đã bỏ lưu bài viết.";
            }
            else if (db.Articles.Any(a => a.ArticleID == articleId))
            {
                db.SavedArticles.Add(new SavedArticles
                {
                    UserId = uid,
                    ArticleId = articleId,
                    SavedDate = DateTime.Now
                });
                TempData["Success"] = "Đã lưu bài viết vào Tin đã lưu.";
            }
            db.SaveChanges();

            // Đồng bộ lại Cookie NewsPortalFavorites theo dữ liệu mới nhất trong SQL
            var syncedIds = db.SavedArticles
                .Where(s => s.UserId == uid)
                .OrderByDescending(s => s.SavedDate)
                .Select(s => s.ArticleId)
                .ToList();

            Response.Cookies.Add(new HttpCookie("NewsPortalFavorites", string.Join(",", syncedIds))
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true
            });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Details", "Articles", new { id = articleId });
        }

        // ---------- 6. TIN ĐÃ XEM ----------
        public ActionResult History()
        {
            var u = CurrentUser();
            if (u == null) return RedirectToAction("Info");

            int uid = u.UserId;
            var list = db.ArticleViewHistory
                .Where(h => h.UserId == uid)
                .OrderByDescending(h => h.ViewedDate)
                .Select(h => new ViewedArticleItemViewModel
                {
                    HistoryId = h.Id,
                    ArticleId = h.ArticleId,
                    Title = h.Articles != null ? h.Articles.Title : ("Bài viết #" + h.ArticleId),
                    Summary = h.Articles != null ? h.Articles.Summary : "",
                    ImageUrl = h.Articles != null ? h.Articles.ImageUrl : null,
                    ViewedAt = h.ViewedDate
                })
                .ToList();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearHistory()
        {
            var u = CurrentUser();
            if (u == null) return HttpNotFound();

            int uid = u.UserId;
            var items = db.ArticleViewHistory.Where(h => h.UserId == uid).ToList();
            if (items.Count > 0)
            {
                db.ArticleViewHistory.RemoveRange(items);
                db.SaveChanges();
                TempData["Success"] = "Đã xóa lịch sử xem bài viết.";
            }
            return RedirectToAction("History");
        }

        // ---------- 7. ĐĂNG XUẤT AN TOÀN ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("Admin");
            Session.Remove("User");
            Session.Remove("UserFullName");
            Session.Remove("UserAvatar");
            Session.Remove("Role");
            Session.Remove("LockUntil");
            Session.Remove("FailCount");
            return RedirectToAction("Index", "Articles");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}