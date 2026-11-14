using System;
using System.Collections.Generic;
using System.Configuration;
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
    [AdminAreaAuthorize]
    public class AdminController : Controller
    {
        private SqlConnection CreateConnection()
        {
            foreach (ConnectionStringSettings item in ConfigurationManager.ConnectionStrings)
            {
                if (string.IsNullOrWhiteSpace(item.ConnectionString)) continue;

                if (item.ConnectionString.IndexOf("metadata=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var efBuilder = new EntityConnectionStringBuilder(item.ConnectionString);
                    return new SqlConnection(efBuilder.ProviderConnectionString);
                }

                if (item.ConnectionString.IndexOf("NewsPortalDB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new SqlConnection(item.ConnectionString);
                }
            }
            throw new InvalidOperationException("Không tìm thấy chuỗi kết nối NewsPortalDB trong Web.config.");
        }

        private string ComputeSha256(string rawText)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawText));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // GET: /Admin
        public ActionResult Index(string search = "", string status = "")
        {
            if (Session["Admin"] == null)
                return RedirectToRoute("AdminArticles", new { action = "Index" });

            var model = new AdminDashboardViewModel
            {
                SearchQuery = search ?? "",
                StatusFilter = status ?? ""
            };

            using (var conn = CreateConnection())
            {
                conn.Open();

                string statsSql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM dbo.Articles) AS TotalArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Published') AS PublishedArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Draft') AS DraftArticles,
                        (SELECT ISNULL(SUM(ViewCount), 0) FROM dbo.Articles) AS TotalViews,
                        (SELECT COUNT(*) FROM dbo.Categories) AS TotalCategories,
                        (SELECT COUNT(*) FROM dbo.Authors) AS TotalAuthors,
                        (SELECT COUNT(*) FROM dbo.Users) AS TotalUsers;";

                using (var cmd = new SqlCommand(statsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalArticles = Convert.ToInt32(reader["TotalArticles"]);
                        model.PublishedArticles = Convert.ToInt32(reader["PublishedArticles"]);
                        model.DraftArticles = Convert.ToInt32(reader["DraftArticles"]);
                        model.TotalViews = Convert.ToInt32(reader["TotalViews"]);
                        model.TotalCategories = Convert.ToInt32(reader["TotalCategories"]);
                        model.TotalAuthors = Convert.ToInt32(reader["TotalAuthors"]);
                        model.TotalUsers = Convert.ToInt32(reader["TotalUsers"]);
                    }
                }

                string listSql = @"
                    SELECT TOP 20 
                        a.ArticleID,
                        a.Title,
                        ISNULL(c.CategoryName, N'Chưa phân loại') AS CategoryName,
                        ISNULL(au.AuthorName, N'Không rõ') AS AuthorName,
                        a.Status,
                        ISNULL(a.ViewCount, 0) AS ViewCount,
                        a.CreatedDate
                    FROM dbo.Articles a
                    LEFT JOIN dbo.Categories c ON a.CategoryID = c.CategoryID
                    LEFT JOIN dbo.Authors au ON a.AuthorID = au.AuthorID
                    WHERE (@search = N'' OR a.Title LIKE N'%' + @search + N'%')
                      AND (@status = N'' OR a.Status = @status)
                    ORDER BY a.CreatedDate DESC;";

                using (var cmd = new SqlCommand(listSql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", model.SearchQuery.Trim());
                    cmd.Parameters.AddWithValue("@status", model.StatusFilter.Trim());

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Articles.Add(new AdminArticleRow
                            {
                                ArticleID = Convert.ToInt32(reader["ArticleID"]),
                                Title = Convert.ToString(reader["Title"]),
                                CategoryName = Convert.ToString(reader["CategoryName"]),
                                AuthorName = Convert.ToString(reader["AuthorName"]),
                                Status = Convert.ToString(reader["Status"]),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                            });
                        }
                    }
                }
            }

            return View(model);
        }

        // POST: /Admin/ToggleStatus
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(int id, string search = "", string status = "")
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                string sql = @"
                    UPDATE dbo.Articles
                    SET Status = CASE WHEN Status = N'Published' THEN N'Draft' ELSE N'Published' END,
                        UpdatedDate = GETDATE()
                    WHERE ArticleID = @id;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["AdminMessage"] = "Đã cập nhật trạng thái xuất bản của bài viết #" + id + ".";
            return RedirectToAction("Index", new { search = search, status = status });
        }

        // GET: /Admin/Users
        [AdminAuthorize]
        public ActionResult Users(string search)
        {
            search = (search ?? string.Empty).Trim();
            var users = new List<AdminUserRow>();
            int activeAdminCount;

            using (var conn = CreateConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT UserId, Username, FullName, Email, Role, IsActive, CreatedAt AS CreatedDate
                    FROM dbo.Users
                    WHERE (@search = N''
                        OR Username LIKE N'%' + @search + N'%'
                        OR FullName LIKE N'%' + @search + N'%'
                        OR Email LIKE N'%' + @search + N'%'
                        OR Role LIKE N'%' + @search + N'%')
                    ORDER BY UserId ASC;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", search);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new AdminUserRow
                            {
                                UserID = Convert.ToInt32(reader["UserId"]),
                                Username = Convert.ToString(reader["Username"]),
                                FullName = Convert.ToString(reader["FullName"]),
                                Email = reader["Email"] == DBNull.Value ? "" : Convert.ToString(reader["Email"]),
                                Role = Convert.ToString(reader["Role"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                            });
                        }
                    }

                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1 AND Role = N'Admin';", conn))
                    {
                        activeAdminCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    }
                }
            }

            foreach (var user in users)
            {
                bool isRootAdmin = string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase);
                bool isLastActiveAdmin = user.IsActive &&
                    string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                    activeAdminCount <= 1;
                user.CanToggleStatus = !isLastActiveAdmin && !(isRootAdmin && user.IsActive);
                user.CanChangeRole = !isRootAdmin && !isLastActiveAdmin;
            }

            ViewBag.Search = search;
            return View(users);
        }

        // POST: /Admin/CreateUser
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser(string username, string password, string fullName, string email, string role)
        {
            username = (username ?? "").Trim();
            fullName = (fullName ?? "").Trim();
            email = (email ?? "").Trim();
            role = string.IsNullOrWhiteSpace(role) ? "User" : role.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["AdminError"] = "Vui lòng nhập đầy đủ Tài khoản, Mật khẩu, Họ tên và Email.";
                return RedirectToAction("Users");
            }

            if (username.Length < 3 || username.Length > 30 || Regex.IsMatch(username, @"\s"))
            {
                TempData["AdminError"] = "Tên đăng nhập phải từ 3 đến 30 ký tự và không chứa khoảng trắng.";
                return RedirectToAction("Users");
            }

            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminError"] = "Tên đăng nhập 'admin' được dành riêng cho tài khoản gốc.";
                return RedirectToAction("Users");
            }

            if (fullName.Length < 2 || fullName.Length > 100)
            {
                TempData["AdminError"] = "Họ tên phải từ 2 đến 100 ký tự.";
                return RedirectToAction("Users");
            }

            if (password.Trim().Length < 6 || password.Length > 64)
            {
                TempData["AdminError"] = "Mật khẩu phải từ 6 đến 64 ký tự.";
                return RedirectToAction("Users");
            }

            if (!string.IsNullOrEmpty(email))
            {
                bool validEmail = email.Length <= 256;
                try
                {
                    var address = new MailAddress(email);
                    validEmail = validEmail && string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
                }
                catch (FormatException)
                {
                    validEmail = false;
                }

                if (!validEmail)
                {
                    TempData["AdminError"] = "Email không đúng định dạng hoặc vượt quá 256 ký tự.";
                    return RedirectToAction("Users");
                }
            }

            if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
                role = "Editor";
            else if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                role = "Admin";
            else if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
                role = "User";
            else
            {
                TempData["AdminError"] = "Vai trò không hợp lệ.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Username = @u", conn))
                {
                    checkCmd.Parameters.AddWithValue("@u", username);
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        TempData["AdminError"] = "Tên đăng nhập '" + username + "' đã tồn tại.";
                        return RedirectToAction("Users");
                    }
                }

                using (var checkEmailCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Email = @email", conn))
                {
                    checkEmailCmd.Parameters.AddWithValue("@email", email);
                    if (Convert.ToInt32(checkEmailCmd.ExecuteScalar()) > 0)
                    {
                        TempData["AdminError"] = "Email này đã được sử dụng cho tài khoản khác.";
                        return RedirectToAction("Users");
                    }
                }

                string insertSql = @"
                    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                    VALUES (@u, @p, @fn, @em, @role, 1, GETDATE());";

                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", ComputeSha256(password.Trim()));
                    cmd.Parameters.AddWithValue("@fn", fullName);
                    cmd.Parameters.AddWithValue("@em", email);
                    cmd.Parameters.AddWithValue("@role", role);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["AdminMessage"] = "Đã tạo tài khoản '" + username + "' thành công.";
            return RedirectToAction("Users");
        }

        // POST: /Admin/UpdateUserRole
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateUserRole(int id, string role)
        {
            role = (role ?? "").Trim();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                role = "Admin";
            else if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
                role = "Editor";
            else if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
                role = "User";
            else
            {
                TempData["AdminError"] = "Vai trò không hợp lệ.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                const string accountSql = "SELECT Username, Role, IsActive FROM dbo.Users WHERE UserId = @id;";
                string username;
                string currentRole;
                bool isActive;

                using (var cmd = new SqlCommand(accountSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            TempData["AdminError"] = "Không tìm thấy tài khoản.";
                            return RedirectToAction("Users");
                        }

                        username = Convert.ToString(reader["Username"]);
                        currentRole = Convert.ToString(reader["Role"]);
                        isActive = Convert.ToBoolean(reader["IsActive"]);
                    }
                }

                bool isRootAdmin = string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);
                bool isSelf = string.Equals(Convert.ToString(Session["Admin"]), username, StringComparison.OrdinalIgnoreCase);
                if (role != "Admin" && (isRootAdmin || isSelf))
                {
                    TempData["AdminError"] = "Không thể hạ cấp tài khoản Admin gốc hoặc tài khoản đang đăng nhập.";
                    return RedirectToAction("Users");
                }

                if (isActive && string.Equals(currentRole, "Admin", StringComparison.OrdinalIgnoreCase) && role != "Admin")
                {
                    const string countSql = "SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin' AND IsActive = 1;";
                    using (var countCmd = new SqlCommand(countSql, conn))
                    {
                        if (Convert.ToInt32(countCmd.ExecuteScalar()) <= 1)
                        {
                            TempData["AdminError"] = "Không thể hạ cấp Admin đang hoạt động cuối cùng.";
                            return RedirectToAction("Users");
                        }
                    }
                }

                const string updateSql = "UPDATE dbo.Users SET Role = @role WHERE UserId = @id;";
                using (var updateCmd = new SqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@role", role);
                    updateCmd.Parameters.AddWithValue("@id", id);
                    updateCmd.ExecuteNonQuery();
                }
            }

            TempData["AdminMessage"] = "Đã cập nhật vai trò tài khoản.";
            return RedirectToAction("Users");
        }

        // POST: /Admin/ToggleUserActive
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserActive(int id)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();

                string accountSql = "SELECT Username, Role, IsActive FROM dbo.Users WHERE UserId = @id;";
                string username;
                string role;
                bool isActive;

                using (var accountCmd = new SqlCommand(accountSql, conn))
                {
                    accountCmd.Parameters.AddWithValue("@id", id);
                    using (var reader = accountCmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            TempData["AdminError"] = "Không tìm thấy tài khoản.";
                            return RedirectToAction("Users");
                        }

                        username = Convert.ToString(reader["Username"]);
                        role = Convert.ToString(reader["Role"]);
                        isActive = Convert.ToBoolean(reader["IsActive"]);
                    }
                }

                if (isActive && string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["AdminError"] = "Không thể khóa tài khoản Quản trị viên gốc.";
                    return RedirectToAction("Users");
                }

                if (isActive && string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    const string countSql = "SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin' AND IsActive = 1;";
                    using (var countCmd = new SqlCommand(countSql, conn))
                    {
                        if (Convert.ToInt32(countCmd.ExecuteScalar()) <= 1)
                        {
                            TempData["AdminError"] = "Không thể khóa Admin đang hoạt động cuối cùng.";
                            return RedirectToAction("Users");
                        }
                    }
                }

                string sql = "UPDATE dbo.Users SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE UserId = @id;";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        TempData["AdminError"] = "Không tìm thấy tài khoản.";
                        return RedirectToAction("Users");
                    }
                }
            }

            TempData["AdminMessage"] = "Đã cập nhật trạng thái tài khoản.";
            return RedirectToAction("Users");
        }
    }
}