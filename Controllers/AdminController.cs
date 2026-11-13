using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    [AdminAuthorize]
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
        public ActionResult Users()
        {
            var users = new List<AdminUserRow>();

            using (var conn = CreateConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT UserID, Username, FullName, Email, Role, IsActive, CreatedDate
                    FROM dbo.Users
                    ORDER BY UserID ASC;";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new AdminUserRow
                        {
                            UserID = Convert.ToInt32(reader["UserID"]),
                            Username = Convert.ToString(reader["Username"]),
                            FullName = Convert.ToString(reader["FullName"]),
                            Email = reader["Email"] == DBNull.Value ? "" : Convert.ToString(reader["Email"]),
                            Role = Convert.ToString(reader["Role"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                        });
                    }
                }
            }

            return View(users);
        }

        // POST: /Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser(string username, string password, string fullName, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            {
                TempData["AdminError"] = "Vui lòng nhập đầy đủ Tài khoản, Mật khẩu và Họ tên.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Username = @u", conn))
                {
                    checkCmd.Parameters.AddWithValue("@u", username.Trim());
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        TempData["AdminError"] = "Tên đăng nhập '" + username.Trim() + "' đã tồn tại.";
                        return RedirectToAction("Users");
                    }
                }

                string insertSql = @"
                    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedDate)
                    VALUES (@u, @p, @fn, @em, @role, 1, GETDATE());";

                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username.Trim());
                    cmd.Parameters.AddWithValue("@p", ComputeSha256(password.Trim()));
                    cmd.Parameters.AddWithValue("@fn", fullName.Trim());
                    cmd.Parameters.AddWithValue("@em", string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email.Trim());
                    cmd.Parameters.AddWithValue("@role", string.IsNullOrWhiteSpace(role) ? "Editor" : role.Trim());
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["AdminMessage"] = "Đã tạo tài khoản '" + username.Trim() + "' thành công.";
            return RedirectToAction("Users");
        }

        // POST: /Admin/ToggleUserActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserActive(int id)
        {
            if (id == 1)
            {
                TempData["AdminError"] = "Không thể khóa tài khoản Quản trị viên gốc.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                string sql = "UPDATE dbo.Users SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE UserID = @id;";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["AdminMessage"] = "Đã cập nhật trạng thái tài khoản.";
            return RedirectToAction("Users");
        }
    }
}