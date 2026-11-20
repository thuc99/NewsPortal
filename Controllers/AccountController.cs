using System;
using System.Configuration;
using System.Data;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class AccountController : Controller
    {
        // Kết nối đến database NewsPortalDB
        private static SqlConnection CreateUsersConnection()
        {
            foreach (ConnectionStringSettings item in
                ConfigurationManager.ConnectionStrings)
            {
                if (string.IsNullOrWhiteSpace(item.ConnectionString))
                    continue;

                if (item.ConnectionString.IndexOf(
                    "metadata=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var builder =
                        new EntityConnectionStringBuilder(item.ConnectionString);

                    return new SqlConnection(
                        builder.ProviderConnectionString);
                }

                if (item.ConnectionString.IndexOf(
                    "NewsPortalDB",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new SqlConnection(item.ConnectionString);
                }
            }

            throw new InvalidOperationException(
                "Không tìm thấy chuỗi kết nối NewsPortalDB trong Web.config.");
        }

        // Mã hóa SHA256
        private static string ComputeSha256(string rawText)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(rawText));

                var result = new StringBuilder();

                foreach (byte value in bytes)
                    result.Append(value.ToString("x2"));

                return result.ToString();
            }
        }

        // Kiểm tra email
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
                return false;

            if (!Regex.IsMatch(
                email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
                return false;

            try
            {
                var addr = new MailAddress(email);

                return string.Equals(
                    addr.Address,
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // Xóa thông tin đăng nhập cũ
        private void ClearLoginSession()
        {
            Session.Remove("Admin");
            Session.Remove("User");
            Session.Remove("UserFullName");
            Session.Remove("Role");
        }

        // GET: /Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            if (Session["Admin"] != null)
                return RedirectToAction("Index", "Home");

            if (string.Equals(
                Convert.ToString(Session["Role"]),
                "Editor",
                StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            if (TempData["Error"] != null)
                ViewBag.Error = TempData["Error"];

            // Thông báo khi bị từ chối truy cập Admin
            if (Session["LoginMessage"] != null)
            {
                ViewBag.Error = Session["LoginMessage"];
                Session.Remove("LoginMessage");
            }

            // Thông báo đăng ký thành công
            if (TempData["Success"] != null)
                ViewBag.Success = TempData["Success"];

            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            // Kiểm tra thời gian khóa đăng nhập
            var lockUntil = Session["LockUntil"] as DateTime?;

            if (lockUntil.HasValue && lockUntil.Value > DateTime.Now)
            {
                int mins = (int)Math.Ceiling(
                    (lockUntil.Value - DateTime.Now).TotalMinutes);

                ViewBag.Error =
                    "Đăng nhập sai quá nhiều lần. Thử lại sau "
                    + mins + " phút.";

                return View();
            }

            username = (username ?? "").Trim();

            if (username.Length == 0 ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";

                return View();
            }

            if (username.Length > 50 || password.Length > 64)
            {
                ViewBag.Error =
                    "Tài khoản hoặc mật khẩu vượt quá độ dài cho phép.";

                return View();
            }

            string databaseUsername = null;
            string databaseFullName = null;
            string databaseRole = null;
            bool databaseAccountIsActive = false;

            using (var conn = CreateUsersConnection())
            {
                conn.Open();

                const string sql = @"
                    SELECT TOP 1
                        Username,
                        FullName,
                        PasswordHash,
                        Role,
                        IsActive
                    FROM dbo.Users
                    WHERE Username = @username;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(
                        "@username", SqlDbType.NVarChar, 50
                    ).Value = username;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            databaseAccountIsActive =
                                Convert.ToBoolean(reader["IsActive"]);

                            databaseRole =
                                Convert.ToString(reader["Role"]);

                            string storedHash =
                                Convert.ToString(reader["PasswordHash"]);

                            if (databaseAccountIsActive &&
                                string.Equals(
                                    storedHash,
                                    ComputeSha256(password.Trim()),
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                databaseUsername =
                                    Convert.ToString(reader["Username"]);

                                databaseFullName =
                                    Convert.ToString(reader["FullName"]);
                            }
                        }
                    }
                }
            }

            // Đăng nhập bằng tài khoản trong database
            if (databaseUsername != null)
            {
                Session.Remove("LockUntil");
                Session["FailCount"] = 0;

                ClearLoginSession();

                if (string.Equals(
                    databaseRole,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Session["Role"] = "Admin";
                    Session["Admin"] = databaseUsername;

                    return RedirectToAction("Index", "Home");
                }

                Session["User"] = databaseUsername;
                Session["UserFullName"] = databaseFullName;

                if (string.Equals(
                    databaseRole,
                    "Editor",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Session["Role"] = "Editor";

                    return RedirectToAction("Index", "Home");
                }

                Session["Role"] = string.Equals(databaseRole, "Author", StringComparison.OrdinalIgnoreCase)
                    ? "Author"
                    : (string.Equals(databaseRole, "Reader", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(databaseRole, "User", StringComparison.OrdinalIgnoreCase)
                        ? "Reader"
                        : databaseRole);

                return RedirectToRoute("Default", new { controller = "Articles", action = "Index" });
            }

            // Đăng nhập thất bại
            int fails = (Session["FailCount"] as int? ?? 0) + 1;

            if (fails >= 5)
            {
                Session["LockUntil"] =
                    DateTime.Now.AddMinutes(5);

                Session["FailCount"] = 0;

                ViewBag.Error =
                    "Sai 5 lần liên tiếp. Tạm khóa đăng nhập 5 phút.";
            }
            else
            {
                Session["FailCount"] = fails;

                ViewBag.Error =
                    "Sai tài khoản hoặc mật khẩu! (Lần "
                    + fails + "/5)";
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
        public ActionResult Register(
            string fullName,
            string username,
            string email,
            string password,
            string confirmPassword)
        {
            fullName = (fullName ?? "").Trim();
            username = (username ?? "").Trim();
            email = (email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(password))
            {
                ViewBag.Error =
                    "Vui lòng nhập đầy đủ họ tên, tài khoản, email và mật khẩu.";

                return View();
            }

            if (fullName.Length < 2 || fullName.Length > 100)
            {
                ViewBag.Error =
                    "Họ tên phải từ 2 đến 100 ký tự.";

                return View();
            }

            if (username.Length < 3 ||
                username.Length > 30 ||
                Regex.IsMatch(username, @"\s"))
            {
                ViewBag.Error =
                    "Tên đăng nhập phải từ 3 đến 30 ký tự và không chứa khoảng trắng.";

                return View();
            }

            if (!IsValidEmail(email))
            {
                ViewBag.Error =
                    "Email không đúng định dạng (ví dụ: tenban@gmail.com).";

                return View();
            }

            if (string.IsNullOrWhiteSpace(password) ||
                password.Trim().Length < 6 ||
                password.Length > 64)
            {
                ViewBag.Error =
                    "Mật khẩu phải từ 6 đến 64 ký tự.";

                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error =
                    "Mật khẩu xác nhận không khớp.";

                return View();
            }

            if (username.Equals(
                "admin",
                StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error =
                    "Tên đăng nhập này không được phép sử dụng.";

                return View();
            }

            try
            {
                using (var conn = CreateUsersConnection())
                {
                    conn.Open();

                    // Kiểm tra tài khoản hoặc email đã tồn tại
                    const string checkSql = @"
                        SELECT COUNT(1)
                        FROM dbo.Users
                        WHERE Username = @username
                           OR Email = @email;";

                    using (var checkCmd = new SqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.Add(
                            "@username", SqlDbType.NVarChar, 30
                        ).Value = username;

                        checkCmd.Parameters.Add(
                            "@email", SqlDbType.NVarChar, 100
                        ).Value = email;

                        int exists = Convert.ToInt32(
                            checkCmd.ExecuteScalar());

                        if (exists > 0)
                        {
                            ViewBag.Error =
                                "Tên đăng nhập hoặc email đã tồn tại.";

                            return View();
                        }
                    }

                    // Thêm tài khoản mới vào SQL Server
                    const string insertSql = @"
                        INSERT INTO dbo.Users
                        (
                            Username,
                            FullName,
                            Email,
                            PasswordHash,
                            Role,
                            IsActive
                        )
                        VALUES
                        (
                            @username,
                            @fullName,
                            @email,
                            @passwordHash,
                            @role,
                            @isActive
                        );";

                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.Add(
                            "@username", SqlDbType.NVarChar, 30
                        ).Value = username;

                        cmd.Parameters.Add(
                            "@fullName", SqlDbType.NVarChar, 100
                        ).Value = fullName;

                        cmd.Parameters.Add(
                            "@email", SqlDbType.NVarChar, 100
                        ).Value = email;

                        cmd.Parameters.Add(
                            "@passwordHash", SqlDbType.NVarChar, 64
                        ).Value = ComputeSha256(password.Trim());

                        cmd.Parameters.Add(
                            "@role", SqlDbType.NVarChar, 20
                        ).Value = "Reader";

                        cmd.Parameters.Add(
                            "@isActive", SqlDbType.Bit
                        ).Value = true;

                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["Success"] =
                    "Đăng ký thành công! Mời bạn đăng nhập.";

                return RedirectToAction("Login");
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    ViewBag.Error =
                        "Tên đăng nhập hoặc email đã được sử dụng.";
                }
                else
                {
                    ViewBag.Error =
                        "Không thể lưu tài khoản vào cơ sở dữ liệu. "
                        + "Vui lòng kiểm tra cấu trúc bảng Users.";
                }

                return View();
            }
            catch (Exception)
            {
                ViewBag.Error =
                    "Đã xảy ra lỗi khi đăng ký tài khoản.";

                return View();
            }
        }

        // GET: /Account/Logout
        public ActionResult Logout()
        {
            ClearLoginSession();

            Session.Remove("LockUntil");
            Session.Remove("FailCount");

            return RedirectToAction("Index", "Articles");
        }
    }
}