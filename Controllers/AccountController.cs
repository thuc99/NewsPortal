using NewsPortal.Models;
using System;
using System.Configuration;
using System.Data;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;

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

            bool databaseAccountFound = false;
            bool databaseAccountIsActive = false;

            string databaseUsername = null;
            string databaseFullName = null;
            string databaseRole = null;

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
                            databaseAccountFound = true;

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

                Session["Role"] = string.Equals(databaseRole, "Author", StringComparison.OrdinalIgnoreCase) ? "Author" : "Reader";

                return RedirectToRoute("Default", new { controller = "Articles", action = "Index" });
            }

            // Đăng nhập Admin cũ
            bool canUseLegacyRootLogin =
                username.Equals(
                    "admin",
                    StringComparison.OrdinalIgnoreCase)
                && password == "123456"
                && (!databaseAccountFound ||
                    (databaseAccountIsActive &&
                    string.Equals(
                        databaseRole,
                        "Admin",
                        StringComparison.OrdinalIgnoreCase)));

            if (canUseLegacyRootLogin)
            {
                ClearLoginSession();

                Session.Remove("LockUntil");
                Session["Role"] = "Admin";
                Session["Admin"] = "admin";
                Session["FailCount"] = 0;

                return RedirectToAction("Index", "Home");
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

        // Tạo mã OTP 6 số ngẫu nhiên an toàn
        private static string GenerateSixDigitOtp()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = Math.Abs(BitConverter.ToInt32(data, 0)) % 1000000;
                return value.ToString("D6");
            }
        }

        // Gửi email chứa mã OTP 6 số qua Gmail SMTP
        private static void SendOtpEmail(string toEmail, string fullName, string otpCode)
        {
            string fromEmail = ConfigurationManager.AppSettings["SmtpEmail"];
            string appPassword = (ConfigurationManager.AppSettings["SmtpAppPassword"] ?? "").Replace(" ", "");

            if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(appPassword))
            {
                throw new InvalidOperationException("Chưa cấu hình SmtpEmail hoặc SmtpAppPassword trong Web.config.");
            }

            string displayName = string.IsNullOrWhiteSpace(fullName) ? toEmail : fullName;
            string subject = "[TinNhanh] Mã xác thực đặt lại mật khẩu (" + otpCode + ")";
            string body = $@"
                <div style='font-family:Arial,sans-serif;max-width:520px;margin:0 auto;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;'>
                    <div style='background:#c4161c;color:#fff;padding:18px 24px;font-size:20px;font-weight:bold;'>
                        TinNhanh - Khôi phục mật khẩu
                    </div>
                    <div style='padding:24px;color:#1f2937;line-height:1.6;'>
                        <p>Xin chào <strong>{WebUtility.HtmlEncode(displayName)}</strong>,</p>
                        <p>Bạn vừa yêu cầu đặt lại mật khẩu cho tài khoản trên <strong>TinNhanh</strong>. Mã xác thực OTP dùng một lần của bạn là:</p>
                        <div style='margin:20px 0;padding:14px;background:#f3f4f6;border-radius:8px;text-align:center;font-size:30px;font-weight:800;letter-spacing:8px;color:#c4161c;'>
                            {otpCode}
                        </div>
                        <p style='font-size:13.5px;color:#4b5563;'>
                            • Mã có hiệu lực trong vòng <strong>15 phút</strong> và chỉ sử dụng được <strong>1 lần</strong>.<br/>
                            • Tuyệt đối không chia sẻ mã này cho bất kỳ ai.
                        </p>
                    </div>
                </div>";

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(fromEmail, "TinNhanh");
                message.To.Add(toEmail);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.EnableSsl = true;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
                    smtp.Send(message);
                }
            }
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            email = (email ?? "").Trim();
            if (!IsValidEmail(email))
            {
                ViewBag.Error = "Vui lòng nhập địa chỉ email hợp lệ đã đăng ký tài khoản.";
                ViewBag.Email = email;
                return View();
            }

            try
            {
                using (var conn = CreateUsersConnection())
                {
                    conn.Open();

                    int userId = 0;
                    string fullName = "";
                    bool isActive = false;

                    using (var userCmd = new SqlCommand("SELECT TOP 1 UserId, FullName, IsActive FROM dbo.Users WHERE Email = @em;", conn))
                    {
                        userCmd.Parameters.Add("@em", SqlDbType.NVarChar, 256).Value = email;
                        using (var r = userCmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                ViewBag.Error = "Không tìm thấy tài khoản nào liên kết với email này.";
                                ViewBag.Email = email;
                                return View();
                            }

                            userId = Convert.ToInt32(r["UserId"]);
                            fullName = r["FullName"] == DBNull.Value ? "" : Convert.ToString(r["FullName"]);
                            isActive = Convert.ToBoolean(r["IsActive"]);
                        }
                    }

                    if (!isActive)
                    {
                        ViewBag.Error = "Tài khoản này đang bị khóa, không thể khôi phục mật khẩu.";
                        ViewBag.Email = email;
                        return View();
                    }

                    // Giới hạn tối đa 3 lần gửi mã OTP trong 15 phút
                    using (var rateCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.PasswordResetOtps WHERE Email = @em AND CreatedAt >= DATEADD(MINUTE, -15, GETDATE());", conn))
                    {
                        rateCmd.Parameters.Add("@em", SqlDbType.NVarChar, 256).Value = email;
                        if (Convert.ToInt32(rateCmd.ExecuteScalar()) >= 3)
                        {
                            ViewBag.Error = "Bạn đã yêu cầu gửi mã quá 3 lần trong 15 phút. Vui lòng thử lại sau.";
                            ViewBag.Email = email;
                            return View();
                        }
                    }

                    // Vô hiệu hóa các mã OTP cũ chưa sử dụng của tài khoản này
                    using (var invCmd = new SqlCommand("UPDATE dbo.PasswordResetOtps SET IsUsed = 1 WHERE UserId = @uid AND IsUsed = 0;", conn))
                    {
                        invCmd.Parameters.Add("@uid", SqlDbType.Int).Value = userId;
                        invCmd.ExecuteNonQuery();
                    }

                    string otpCode = GenerateSixDigitOtp();

                    const string insertOtpSql = @"
                        INSERT INTO dbo.PasswordResetOtps (UserId, Email, OtpCode, CreatedAt, ExpiresAt, IsUsed, AttemptCount)
                        VALUES (@uid, @em, @otp, GETDATE(), DATEADD(MINUTE, 15, GETDATE()), 0, 0);";

                    using (var insCmd = new SqlCommand(insertOtpSql, conn))
                    {
                        insCmd.Parameters.Add("@uid", SqlDbType.Int).Value = userId;
                        insCmd.Parameters.Add("@em", SqlDbType.NVarChar, 256).Value = email;
                        insCmd.Parameters.Add("@otp", SqlDbType.NVarChar, 10).Value = otpCode;
                        insCmd.ExecuteNonQuery();
                    }

                    SendOtpEmail(email, fullName, otpCode);
                }

                // Xóa trạng thái xác thực cũ (nếu có) và chuyển sang trang nhập OTP
                Session.Remove("VerifiedResetUserId");
                Session.Remove("VerifiedResetEmail");
                Session.Remove("VerifiedResetExpires");

                Session["PendingResetEmail"] = email;
                TempData["OtpInfo"] = "Mã xác thực 6 số đã được gửi tới " + email + " (hiệu lực 15 phút).";
                return RedirectToAction("VerifyOtp");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Không thể gửi email xác thực: " + ex.Message;
                ViewBag.Email = email;
                return View();
            }
        }

        // TRANG 1: XÁC THỰC MÃ OTP 6 SỐ
        // GET: /Account/VerifyOtp
        [HttpGet]
        public ActionResult VerifyOtp()
        {
            string email = Convert.ToString(Session["PendingResetEmail"]);
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = email;
            ViewBag.Info = TempData["OtpInfo"] as string;
            return View();
        }

        // POST: /Account/VerifyOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOtp(string email, string otpCode)
        {
            email = (email ?? Convert.ToString(Session["PendingResetEmail"]) ?? "").Trim();
            otpCode = (otpCode ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(otpCode) || otpCode.Length != 6)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ mã OTP gồm 6 chữ số.";
                return View();
            }

            using (var conn = CreateUsersConnection())
            {
                conn.Open();

                int otpId = 0;
                int userId = 0;
                string dbOtp = "";
                DateTime expiresAt = DateTime.MinValue;
                int attempts = 0;

                const string findOtpSql = @"
                    SELECT TOP 1 OtpId, UserId, OtpCode, ExpiresAt, AttemptCount
                    FROM dbo.PasswordResetOtps
                    WHERE Email = @em AND IsUsed = 0
                    ORDER BY OtpId DESC;";

                using (var cmd = new SqlCommand(findOtpSql, conn))
                {
                    cmd.Parameters.Add("@em", SqlDbType.NVarChar, 256).Value = email;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            ViewBag.Error = "Không tìm thấy mã OTP hợp lệ hoặc mã đã được sử dụng. Vui lòng gửi lại mã mới.";
                            return View();
                        }

                        otpId = Convert.ToInt32(r["OtpId"]);
                        userId = Convert.ToInt32(r["UserId"]);
                        dbOtp = Convert.ToString(r["OtpCode"]);
                        expiresAt = Convert.ToDateTime(r["ExpiresAt"]);
                        attempts = Convert.ToInt32(r["AttemptCount"]);
                    }
                }

                if (DateTime.Now > expiresAt || attempts >= 5)
                {
                    using (var expCmd = new SqlCommand("UPDATE dbo.PasswordResetOtps SET IsUsed = 1 WHERE OtpId = @oid;", conn))
                    {
                        expCmd.Parameters.Add("@oid", SqlDbType.Int).Value = otpId;
                        expCmd.ExecuteNonQuery();
                    }

                    ViewBag.Error = "Mã OTP đã hết hạn hoặc nhập sai quá 5 lần. Vui lòng yêu cầu gửi lại mã mới.";
                    return View();
                }

                if (!string.Equals(dbOtp, otpCode, StringComparison.Ordinal))
                {
                    using (var attCmd = new SqlCommand("UPDATE dbo.PasswordResetOtps SET AttemptCount = AttemptCount + 1 WHERE OtpId = @oid;", conn))
                    {
                        attCmd.Parameters.Add("@oid", SqlDbType.Int).Value = otpId;
                        attCmd.ExecuteNonQuery();
                    }

                    int remaining = 4 - attempts;
                    ViewBag.Error = remaining > 0
                        ? "Mã OTP không chính xác (còn " + remaining + " lần thử)."
                        : "Bạn đã nhập sai quá số lần quy định. Vui lòng yêu cầu gửi lại mã mới.";
                    return View();
                }

                // Khóa mã OTP ngay khi xác thực thành công (đảm bảo token chỉ dùng 1 lần)
                using (var usedCmd = new SqlCommand("UPDATE dbo.PasswordResetOtps SET IsUsed = 1 WHERE OtpId = @oid;", conn))
                {
                    usedCmd.Parameters.Add("@oid", SqlDbType.Int).Value = otpId;
                    usedCmd.ExecuteNonQuery();
                }

                // Cấp quyền tạm thời (10 phút) để mở trang Đặt lại mật khẩu mới
                Session.Remove("PendingResetEmail");
                Session["VerifiedResetUserId"] = userId;
                Session["VerifiedResetEmail"] = email;
                Session["VerifiedResetExpires"] = DateTime.Now.AddMinutes(10);
            }

            return RedirectToAction("ResetPassword");
        }

        // TRANG 2: ĐẶT LẠI MẬT KHẨU MỚI (CHỈ VÀO ĐƯỢC SAU KHI ĐÃ XÁC THỰC OTP)
        // GET: /Account/ResetPassword
        [HttpGet]
        public ActionResult ResetPassword()
        {
            var userIdObj = Session["VerifiedResetUserId"];
            var expiresObj = Session["VerifiedResetExpires"] as DateTime?;

            if (userIdObj == null || !expiresObj.HasValue || DateTime.Now > expiresObj.Value)
            {
                Session.Remove("VerifiedResetUserId");
                Session.Remove("VerifiedResetEmail");
                Session.Remove("VerifiedResetExpires");
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = Convert.ToString(Session["VerifiedResetEmail"]);
            return View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            var userIdObj = Session["VerifiedResetUserId"];
            var expiresObj = Session["VerifiedResetExpires"] as DateTime?;

            if (userIdObj == null || !expiresObj.HasValue || DateTime.Now > expiresObj.Value)
            {
                Session.Remove("VerifiedResetUserId");
                Session.Remove("VerifiedResetEmail");
                Session.Remove("VerifiedResetExpires");
                return RedirectToAction("ForgotPassword");
            }

            int userId = Convert.ToInt32(userIdObj);
            ViewBag.Email = Convert.ToString(Session["VerifiedResetEmail"]);

            newPassword = (newPassword ?? "").Trim();
            confirmPassword = (confirmPassword ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ mật khẩu mới và xác nhận mật khẩu.";
                return View();
            }

            if (newPassword.Length < 6 || newPassword.Length > 64)
            {
                ViewBag.Error = "Mật khẩu mới phải có độ dài từ 6 đến 64 ký tự.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp với mật khẩu mới.";
                return View();
            }

            using (var conn = CreateUsersConnection())
            {
                conn.Open();
                using (var upUserCmd = new SqlCommand("UPDATE dbo.Users SET PasswordHash = @hash WHERE UserId = @uid;", conn))
                {
                    upUserCmd.Parameters.Add("@hash", SqlDbType.NVarChar, 64).Value = ComputeSha256(newPassword);
                    upUserCmd.Parameters.Add("@uid", SqlDbType.Int).Value = userId;
                    upUserCmd.ExecuteNonQuery();
                }
            }

            // Xóa phiên xác thực sau khi đổi mật khẩu xong
            Session.Remove("VerifiedResetUserId");
            Session.Remove("VerifiedResetEmail");
            Session.Remove("VerifiedResetExpires");

            TempData["Success"] = "Đặt lại mật khẩu thành công! Mời bạn đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
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