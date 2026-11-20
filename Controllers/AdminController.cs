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
        public class CategoryStatusStat
        {
            public int CategoryID { get; set; }
            public string CategoryName { get; set; }
            public int TotalArticles { get; set; }
            public int PublishedCount { get; set; }
            public int PendingCount { get; set; }
            public int DraftCount { get; set; }
            public int RejectedCount { get; set; }
            public int ArchivedCount { get; set; }
            public int TotalViews { get; set; }
        }

        public class ArticleRatingStat
        {
            public int ArticleID { get; set; }
            public string Title { get; set; }
            public string CategoryName { get; set; }
            public int RatingCount { get; set; }
            public double AverageRating { get; set; }
            public int FiveStarCount { get; set; }
            public int LowStarCount { get; set; }
        }

        private static readonly string[] ValidStatuses = new[]
        {
            "Draft", "Pending", "Published", "Rejected", "Archived"
        };

        private static bool _statusConstraintChecked = false;
        private static readonly object _constraintLock = new object();

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

        private void EnsureWorkflowStatusConstraint(SqlConnection conn)
        {
            if (_statusConstraintChecked) return;

            lock (_constraintLock)
            {
                if (_statusConstraintChecked) return;

                const string sql = @"
IF OBJECT_ID(N'dbo.Articles', N'U') IS NOT NULL
BEGIN
    DECLARE @dropSql NVARCHAR(MAX) = N'';
    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Articles DROP CONSTRAINT [' + REPLACE(cc.name, N']', N']]') + N'];'
    FROM sys.check_constraints cc
    LEFT JOIN sys.columns col
        ON col.object_id = cc.parent_object_id
       AND col.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Articles')
      AND (col.name = N'Status' OR cc.definition LIKE N'%Status%' OR cc.name IN (N'CK_Articles_Status', N'CK_Articles_Status_Workflow'));

    IF LEN(@dropSql) > 0
        EXEC sp_executesql @dropSql;

    UPDATE dbo.Articles
    SET Status = N'Draft'
    WHERE Status IS NULL
       OR Status NOT IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived');

    ALTER TABLE dbo.Articles WITH CHECK
    ADD CONSTRAINT [CK_Articles_Status_Workflow]
    CHECK ([Status] IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived'));

    IF COL_LENGTH(N'dbo.Articles', N'RejectionReason') IS NULL
    BEGIN
        ALTER TABLE dbo.Articles ADD [RejectionReason] NVARCHAR(500) NULL;
    END
END";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                _statusConstraintChecked = true;
            }
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
        public ActionResult Index(string search = "", string status = "", int page = 1)
        {
            if (Session["Admin"] == null)
                return RedirectToRoute("AdminArticles", new { action = "Index" });

            const int pageSize = 10;
            var model = new AdminDashboardViewModel
            {
                SearchQuery = search ?? "",
                StatusFilter = status ?? "",
                PageSize = pageSize
            };

            int pendingArticles = 0;
            int rejectedArticles = 0;
            int archivedArticles = 0;
            var rejectionReasons = new Dictionary<int, string>();
            var categoryStats = new List<CategoryStatusStat>();
            var articleRatingStats = new List<ArticleRatingStat>();
            int totalRatings = 0;
            double overallAverageRating = 0;
            int[] starCounts = new int[6];

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureWorkflowStatusConstraint(conn);

                string statsSql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status <> N'Draft') AS TotalArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Published') AS PublishedArticles,
                        0 AS DraftArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Pending') AS PendingArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Rejected') AS RejectedArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Archived') AS ArchivedArticles,
                        (SELECT ISNULL(SUM(ViewCount), 0) FROM dbo.Articles WHERE Status <> N'Draft') AS TotalViews,
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
                        pendingArticles = Convert.ToInt32(reader["PendingArticles"]);
                        rejectedArticles = Convert.ToInt32(reader["RejectedArticles"]);
                        archivedArticles = Convert.ToInt32(reader["ArchivedArticles"]);
                        model.TotalViews = Convert.ToInt32(reader["TotalViews"]);
                        model.TotalCategories = Convert.ToInt32(reader["TotalCategories"]);
                        model.TotalAuthors = Convert.ToInt32(reader["TotalAuthors"]);
                        model.TotalUsers = Convert.ToInt32(reader["TotalUsers"]);
                    }
                }

                const string filteredCountSql = @"
                    SELECT COUNT(*)
                    FROM dbo.Articles a
                    WHERE a.Status <> N'Draft'
                      AND (@search = N'' OR a.Title LIKE N'%' + @search + N'%')
                      AND (@status = N'' OR a.Status = @status);";

                using (var cmd = new SqlCommand(filteredCountSql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", model.SearchQuery.Trim());
                    cmd.Parameters.AddWithValue("@status", model.StatusFilter.Trim());
                    model.FilteredArticleCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                model.TotalPages = Math.Max((int)Math.Ceiling((double)model.FilteredArticleCount / pageSize), 1);
                model.CurrentPage = Math.Min(Math.Max(page, 1), model.TotalPages);

                string listSql = @"
                    SELECT
                        a.ArticleID,
                        a.Title,
                        ISNULL(c.CategoryName, N'Chưa phân loại') AS CategoryName,
                        ISNULL(au.AuthorName, N'Không rõ') AS AuthorName,
                        a.Status,
                        ISNULL(a.ViewCount, 0) AS ViewCount,
                        a.CreatedDate,
                        a.RejectionReason
                    FROM dbo.Articles a
                    LEFT JOIN dbo.Categories c ON a.CategoryID = c.CategoryID
                    LEFT JOIN dbo.Authors au ON a.AuthorID = au.AuthorID
                    WHERE a.Status <> N'Draft'
                      AND (@search = N'' OR a.Title LIKE N'%' + @search + N'%')
                      AND (@status = N'' OR a.Status = @status)
                    ORDER BY CASE WHEN a.Status = N'Pending' THEN 0 ELSE 1 END,
                             a.CreatedDate DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                using (var cmd = new SqlCommand(listSql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", model.SearchQuery.Trim());
                    cmd.Parameters.AddWithValue("@status", model.StatusFilter.Trim());
                    cmd.Parameters.AddWithValue("@offset", (model.CurrentPage - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int articleId = Convert.ToInt32(reader["ArticleID"]);
                            model.Articles.Add(new AdminArticleRow
                            {
                                ArticleID = articleId,
                                Title = Convert.ToString(reader["Title"]),
                                CategoryName = Convert.ToString(reader["CategoryName"]),
                                AuthorName = Convert.ToString(reader["AuthorName"]),
                                Status = Convert.ToString(reader["Status"]),
                                ViewCount = Convert.ToInt32(reader["ViewCount"]),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                            });

                            if (reader["RejectionReason"] != DBNull.Value)
                            {
                                string reasonText = Convert.ToString(reader["RejectionReason"]).Trim();
                                if (!string.IsNullOrEmpty(reasonText))
                                {
                                    rejectionReasons[articleId] = reasonText;
                                }
                            }
                        }
                    }
                }

                string categoryStatsSql = @"
                    SELECT
                        c.CategoryID,
                        c.CategoryName,
                        COUNT(a.ArticleID) AS TotalArticles,
                        SUM(CASE WHEN a.Status = N'Published' THEN 1 ELSE 0 END) AS PublishedCount,
                        SUM(CASE WHEN a.Status = N'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN a.Status = N'Draft' THEN 1 ELSE 0 END) AS DraftCount,
                        SUM(CASE WHEN a.Status = N'Rejected' THEN 1 ELSE 0 END) AS RejectedCount,
                        SUM(CASE WHEN a.Status = N'Archived' THEN 1 ELSE 0 END) AS ArchivedCount,
                        ISNULL(SUM(a.ViewCount), 0) AS TotalViews
                    FROM dbo.Categories c
                    LEFT JOIN dbo.Articles a ON a.CategoryID = c.CategoryID AND a.Status <> N'Draft'
                    GROUP BY c.CategoryID, c.CategoryName
                    ORDER BY COUNT(a.ArticleID) DESC, c.CategoryName ASC;";

                using (var cmd = new SqlCommand(categoryStatsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categoryStats.Add(new CategoryStatusStat
                        {
                            CategoryID = Convert.ToInt32(reader["CategoryID"]),
                            CategoryName = Convert.ToString(reader["CategoryName"]),
                            TotalArticles = Convert.ToInt32(reader["TotalArticles"]),
                            PublishedCount = Convert.ToInt32(reader["PublishedCount"]),
                            PendingCount = Convert.ToInt32(reader["PendingCount"]),
                            DraftCount = Convert.ToInt32(reader["DraftCount"]),
                            RejectedCount = Convert.ToInt32(reader["RejectedCount"]),
                            ArchivedCount = Convert.ToInt32(reader["ArchivedCount"]),
                            TotalViews = Convert.ToInt32(reader["TotalViews"])
                        });
                    }
                }

                string ratingSummarySql = @"
                    IF OBJECT_ID(N'dbo.ArticleRatings', N'U') IS NOT NULL
                    BEGIN
                        SELECT
                            COUNT(*) AS TotalRatings,
                            ISNULL(AVG(CAST(Rating AS FLOAT)), 0) AS OverallAvg,
                            SUM(CASE WHEN Rating = 5 THEN 1 ELSE 0 END) AS Star5,
                            SUM(CASE WHEN Rating = 4 THEN 1 ELSE 0 END) AS Star4,
                            SUM(CASE WHEN Rating = 3 THEN 1 ELSE 0 END) AS Star3,
                            SUM(CASE WHEN Rating = 2 THEN 1 ELSE 0 END) AS Star2,
                            SUM(CASE WHEN Rating = 1 THEN 1 ELSE 0 END) AS Star1
                        FROM dbo.ArticleRatings;
                    END";

                using (var cmd = new SqlCommand(ratingSummarySql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalRatings = reader["TotalRatings"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalRatings"]);
                        overallAverageRating = reader["OverallAvg"] == DBNull.Value ? 0 : Convert.ToDouble(reader["OverallAvg"]);
                        starCounts[5] = reader["Star5"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Star5"]);
                        starCounts[4] = reader["Star4"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Star4"]);
                        starCounts[3] = reader["Star3"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Star3"]);
                        starCounts[2] = reader["Star2"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Star2"]);
                        starCounts[1] = reader["Star1"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Star1"]);
                    }
                }

                string articleRatingsSql = @"
                    IF OBJECT_ID(N'dbo.ArticleRatings', N'U') IS NOT NULL
                    BEGIN
                        SELECT TOP 15
                            a.ArticleID,
                            a.Title,
                            ISNULL(c.CategoryName, N'Chưa phân loại') AS CategoryName,
                            COUNT(r.RatingID) AS RatingCount,
                            AVG(CAST(r.Rating AS FLOAT)) AS AverageRating,
                            SUM(CASE WHEN r.Rating = 5 THEN 1 ELSE 0 END) AS FiveStarCount,
                            SUM(CASE WHEN r.Rating <= 2 THEN 1 ELSE 0 END) AS LowStarCount
                        FROM dbo.Articles a
                        INNER JOIN dbo.ArticleRatings r ON r.ArticleID = a.ArticleID
                        LEFT JOIN dbo.Categories c ON c.CategoryID = a.CategoryID
                        GROUP BY a.ArticleID, a.Title, c.CategoryName
                        ORDER BY AVG(CAST(r.Rating AS FLOAT)) DESC, COUNT(r.RatingID) DESC;
                    END";

                using (var cmd = new SqlCommand(articleRatingsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        articleRatingStats.Add(new ArticleRatingStat
                        {
                            ArticleID = Convert.ToInt32(reader["ArticleID"]),
                            Title = Convert.ToString(reader["Title"]),
                            CategoryName = Convert.ToString(reader["CategoryName"]),
                            RatingCount = Convert.ToInt32(reader["RatingCount"]),
                            AverageRating = reader["AverageRating"] == DBNull.Value ? 0 : Convert.ToDouble(reader["AverageRating"]),
                            FiveStarCount = reader["FiveStarCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FiveStarCount"]),
                            LowStarCount = reader["LowStarCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LowStarCount"])
                        });
                    }
                }
            }

            ViewBag.PendingArticles = pendingArticles;
            ViewBag.RejectedArticles = rejectedArticles;
            ViewBag.ArchivedArticles = archivedArticles;
            ViewBag.RejectionReasons = rejectionReasons;
            ViewBag.CategoryStats = categoryStats;
            ViewBag.ArticleRatingStats = articleRatingStats;
            ViewBag.TotalRatings = totalRatings;
            ViewBag.OverallAverageRating = overallAverageRating;
            ViewBag.StarCounts = starCounts;

            return View(model);
        }

        // POST: /Admin/UpdateArticleStatus
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateArticleStatus(int id, string newStatus, string search = "", string status = "", string rejectionReason = "")
        {
            newStatus = (newStatus ?? "").Trim();
            rejectionReason = (rejectionReason ?? "").Trim();
            if (!ValidStatuses.Contains(newStatus))
            {
                TempData["AdminError"] = "Trạng thái bài viết không hợp lệ.";
                return RedirectToAction("Index", new { search = search, status = status });
            }

            if (newStatus == "Rejected")
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["AdminError"] = "Vui lòng nhập lý do từ chối để người viết biết cần sửa gì.";
                    return RedirectToAction("Index", new { search = search, status = status });
                }

                if (rejectionReason.Length > 500)
                {
                    rejectionReason = rejectionReason.Substring(0, 500);
                }
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureWorkflowStatusConstraint(conn);

                const string checkDraftSql = "SELECT Status FROM dbo.Articles WHERE ArticleID = @id;";
                using (var checkCmd = new SqlCommand(checkDraftSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@id", id);
                    object currentStatusObj = checkCmd.ExecuteScalar();
                    if (currentStatusObj == null || currentStatusObj == DBNull.Value)
                    {
                        TempData["AdminError"] = "Không tìm thấy bài viết #" + id + ".";
                        return RedirectToAction("Index", new { search = search, status = status });
                    }
                    if (string.Equals(Convert.ToString(currentStatusObj), "Draft", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["AdminError"] = "Bài viết #" + id + " đang ở trạng thái Bản nháp (Draft) — chỉ người viết bài mới có quyền xem và thao tác.";
                        return RedirectToAction("Index", new { search = search, status = status });
                    }
                }

                const string sql = @"
                    UPDATE dbo.Articles
                    SET Status = @newStatus,
                        RejectionReason = @rejectionReason,
                        UpdatedDate = GETDATE()
                    WHERE ArticleID = @id AND Status <> N'Draft';";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@newStatus", newStatus);
                    cmd.Parameters.AddWithValue(
                        "@rejectionReason",
                        newStatus == "Rejected" ? (object)rejectionReason : DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        TempData["AdminError"] = "Không tìm thấy bài viết #" + id + ".";
                        return RedirectToAction("Index", new { search = search, status = status });
                    }
                }
            }

            switch (newStatus)
            {
                case "Published":
                    TempData["AdminMessage"] = "Đã duyệt và xuất bản bài viết #" + id + " lên trang chủ (Published).";
                    break;
                case "Rejected":
                    TempData["AdminMessage"] = "Đã từ chối bài viết #" + id + " (Lý do: " + rejectionReason + ") — trả về cho phóng viên chỉnh sửa và gửi duyệt lại (Rejected).";
                    break;
                case "Archived":
                    TempData["AdminMessage"] = "Đã chuyển bài viết #" + id + " vào kho lưu trữ (Archived).";
                    break;
                case "Pending":
                    TempData["AdminMessage"] = "Đã chuyển bài viết #" + id + " sang trạng thái Chờ duyệt (Pending).";
                    break;
                default:
                    TempData["AdminMessage"] = "Đã chuyển bài viết #" + id + " về Bản nháp (Draft).";
                    break;
            }

            return RedirectToAction("Index", new { search = search, status = status });
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
                EnsureWorkflowStatusConstraint(conn);

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
                    SELECT UserId, Username, FullName, Email, BirthDate, Gender, Phone, Address, Role, IsActive, CreatedAt AS CreatedDate
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
                                BirthDate = reader["BirthDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BirthDate"]),
                                Gender = reader["Gender"] == DBNull.Value ? "" : Convert.ToString(reader["Gender"]),
                                Phone = reader["Phone"] == DBNull.Value ? "" : Convert.ToString(reader["Phone"]),
                                Address = reader["Address"] == DBNull.Value ? "" : Convert.ToString(reader["Address"]),
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
            role = string.IsNullOrWhiteSpace(role) ? "Reader" : role.Trim();

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
                role = "Reader";
            else if (string.Equals(role, "Reader", StringComparison.OrdinalIgnoreCase))
                role = "Reader";
            else if (string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                role = "Author";
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

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string insertSql = @"
                    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                    OUTPUT INSERTED.UserId
                    VALUES (@u, @p, @fn, @em, @role, 1, GETDATE());";

                        int userId;
                        using (var cmd = new SqlCommand(insertSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@u", username);
                            cmd.Parameters.AddWithValue("@p", ComputeSha256(password.Trim()));
                            cmd.Parameters.AddWithValue("@fn", fullName);
                            cmd.Parameters.AddWithValue("@em", email);
                            cmd.Parameters.AddWithValue("@role", role);
                            userId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        if (role == "Author")
                        {
                            using (var authorCmd = new SqlCommand(@"
                        INSERT INTO dbo.Authors (AuthorName, Email, UserId)
                        VALUES (@name, @email, @userId);", conn, transaction))
                            {
                                authorCmd.Parameters.AddWithValue("@name", fullName);
                                authorCmd.Parameters.AddWithValue("@email", email);
                                authorCmd.Parameters.AddWithValue("@userId", userId);
                                authorCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
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
                role = "Reader";
            else if (string.Equals(role, "Reader", StringComparison.OrdinalIgnoreCase))
                role = "Reader";
            else if (string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                role = "Author";
            else
            {
                TempData["AdminError"] = "Vai trò không hợp lệ.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                const string accountSql = "SELECT Username, FullName, Email, Role, IsActive FROM dbo.Users WHERE UserId = @id;";
                string username;
                string fullName;
                string email;
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
                        fullName = Convert.ToString(reader["FullName"]);
                        email = Convert.ToString(reader["Email"]);
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

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var updateCmd = new SqlCommand(
                            "UPDATE dbo.Users SET Role = @role WHERE UserId = @id;", conn, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@role", role);
                            updateCmd.Parameters.AddWithValue("@id", id);
                            updateCmd.ExecuteNonQuery();
                        }

                        if (role == "Author")
                        {
                            using (var linkCmd = new SqlCommand(@"
                                IF EXISTS (SELECT 1 FROM dbo.Authors WHERE UserId = @userId)
                                    UPDATE dbo.Authors
                                    SET AuthorName = @name, Email = @email
                                    WHERE UserId = @userId;
                                ELSE IF (SELECT COUNT(*) FROM dbo.Authors WHERE Email = @email AND UserId IS NULL) = 1
                                    UPDATE dbo.Authors
                                    SET UserId = @userId, AuthorName = @name
                                    WHERE Email = @email AND UserId IS NULL;
                                ELSE
                                    INSERT INTO dbo.Authors (AuthorName, Email, UserId)
                                    VALUES (@name, @email, @userId);", conn, transaction))
                            {
                                linkCmd.Parameters.AddWithValue("@name", fullName);
                                linkCmd.Parameters.AddWithValue("@email", email);
                                linkCmd.Parameters.AddWithValue("@userId", id);
                                linkCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
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

        // GET: /Admin/Comments
        [AdminAuthorize]
        public ActionResult Comments(string search, string status)
        {
            search = (search ?? string.Empty).Trim();
            status = (status ?? string.Empty).Trim();

            var comments = new List<AdminCommentRow>();
            int totalCount = 0, pendingCount = 0, approvedCount = 0, hiddenCount = 0;

            using (var conn = CreateConnection())
            {
                conn.Open();

                string countSql = @"
                    SELECT
                        COUNT(*) AS TotalCount,
                        SUM(CASE WHEN Status = N'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN Status = N'Approved' THEN 1 ELSE 0 END) AS ApprovedCount,
                        SUM(CASE WHEN Status = N'Hidden' THEN 1 ELSE 0 END) AS HiddenCount
                    FROM dbo.Comments;";

                using (var countCmd = new SqlCommand(countSql, conn))
                using (var r = countCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        totalCount = r["TotalCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["TotalCount"]);
                        pendingCount = r["PendingCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["PendingCount"]);
                        approvedCount = r["ApprovedCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["ApprovedCount"]);
                        hiddenCount = r["HiddenCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["HiddenCount"]);
                    }
                }

                string sql = @"
                    SELECT
                        c.CommentID,
                        c.ArticleID,
                        a.Title AS ArticleTitle,
                        c.UserId,
                        u.Username,
                        c.CommenterName,
                        c.Content,
                        c.CreatedDate,
                        c.Status
                    FROM dbo.Comments c
                    LEFT JOIN dbo.Articles a ON c.ArticleID = a.ArticleID
                    LEFT JOIN dbo.Users u ON c.UserId = u.UserId
                    WHERE (@status = N'' OR c.Status = @status)
                      AND (@search = N''
                           OR c.CommenterName LIKE N'%' + @search + N'%'
                           OR c.Content LIKE N'%' + @search + N'%'
                           OR a.Title LIKE N'%' + @search + N'%'
                           OR u.Username LIKE N'%' + @search + N'%')
                    ORDER BY
                        c.CreatedDate DESC,
                        c.CommentID DESC;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@search", search);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int articleId = Convert.ToInt32(reader["ArticleID"]);
                            comments.Add(new AdminCommentRow
                            {
                                CommentID = Convert.ToInt32(reader["CommentID"]),
                                ArticleID = articleId,
                                ArticleTitle = reader["ArticleTitle"] == DBNull.Value
                                    ? ("Bài viết #" + articleId)
                                    : Convert.ToString(reader["ArticleTitle"]),
                                UserID = reader["UserId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["UserId"]),
                                Username = reader["Username"] == DBNull.Value ? "" : Convert.ToString(reader["Username"]),
                                CommenterName = reader["CommenterName"] == DBNull.Value ? "" : Convert.ToString(reader["CommenterName"]),
                                Content = reader["Content"] == DBNull.Value ? "" : Convert.ToString(reader["Content"]),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                Status = reader["Status"] == DBNull.Value ? "Pending" : Convert.ToString(reader["Status"])
                            });
                        }
                    }
                }
            }

            ViewBag.Search = search;
            ViewBag.StatusFilter = status;
            ViewBag.TotalCount = totalCount;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = approvedCount;
            ViewBag.HiddenCount = hiddenCount;

            return View(comments);
        }

        // POST: /Admin/ApproveComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult ApproveComment(int id, string search, string status)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Comments SET Status = N'Approved' WHERE CommentID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                        TempData["AdminMessage"] = "Đã duyệt bình luận #" + id + " hiển thị công khai.";
                    else
                        TempData["AdminError"] = "Không tìm thấy bình luận cần duyệt.";
                }
            }
            return RedirectToAction("Comments", new { search = search, status = status });
        }

        // POST: /Admin/HideComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult HideComment(int id, string search, string status)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Comments SET Status = N'Hidden' WHERE CommentID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                        TempData["AdminMessage"] = "Đã ẩn bình luận #" + id + " khỏi trang công khai.";
                    else
                        TempData["AdminError"] = "Không tìm thấy bình luận cần ẩn.";
                }
            }
            return RedirectToAction("Comments", new { search = search, status = status });
        }

        // POST: /Admin/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult DeleteComment(int id, string search, string status)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("DELETE FROM dbo.Comments WHERE CommentID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                        TempData["AdminMessage"] = "Đã xóa vĩnh viễn bình luận #" + id + ".";
                    else
                        TempData["AdminError"] = "Không tìm thấy bình luận cần xóa.";
                }
            }
            return RedirectToAction("Comments", new { search = search, status = status });
        }
        // POST: /Admin/MarkCommentSpam
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult MarkCommentSpam(int id, string search, string status)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Comments SET Status = N'Hidden' WHERE CommentID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                        TempData["AdminMessage"] = "Đã đánh dấu spam và ẩn bình luận #" + id + " khỏi trang công khai.";
                    else
                        TempData["AdminError"] = "Không tìm thấy bình luận cần xử lý.";
                }
            }
            return RedirectToAction("Comments", new { search = search, status = status });
        }
    }
}