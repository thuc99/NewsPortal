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
        private static readonly string[] ValidStatuses = new[]
        {
            "Draft", "Pending", "Published", "Rejected", "Archived"
        };

        private static bool _statusConstraintChecked = false;
        private static bool _roleConstraintChecked = false;
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
    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Articles DROP CONSTRAINT [' + REPLACE(cc.name COLLATE DATABASE_DEFAULT, N']', N']]') + N'];'
    FROM sys.check_constraints cc
    LEFT JOIN sys.columns col
        ON col.object_id = cc.parent_object_id
       AND col.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Articles')
      AND (col.name COLLATE DATABASE_DEFAULT = N'Status'
           OR cc.definition COLLATE DATABASE_DEFAULT LIKE N'%Status%'
           OR cc.name COLLATE DATABASE_DEFAULT IN (N'CK_Articles_Status', N'CK_Articles_Status_Workflow'));

    IF LEN(@dropSql) > 0
        EXEC sp_executesql @dropSql;

    UPDATE dbo.Articles
    SET Status = N'Draft'
    WHERE Status IS NULL
       OR Status NOT IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived');

    ALTER TABLE dbo.Articles WITH CHECK
    ADD CONSTRAINT [CK_Articles_Status_Workflow]
    CHECK ([Status] IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived'));
END";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                _statusConstraintChecked = true;
            }
        }

        private void EnsureUserRoleConstraintAndSyncAuthors(SqlConnection conn)
        {
            if (!_roleConstraintChecked)
            {
                lock (_constraintLock)
                {
                    if (!_roleConstraintChecked)
                    {
                        const string roleConstraintSql = @"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    DECLARE @dropRoleSql NVARCHAR(MAX) = N'';
    SELECT @dropRoleSql = @dropRoleSql + N'ALTER TABLE dbo.Users DROP CONSTRAINT [' + REPLACE(cc.name COLLATE DATABASE_DEFAULT, N']', N']]') + N'];'
    FROM sys.check_constraints cc
    LEFT JOIN sys.columns col
        ON col.object_id = cc.parent_object_id
       AND col.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Users')
      AND (col.name COLLATE DATABASE_DEFAULT = N'Role'
           OR cc.definition COLLATE DATABASE_DEFAULT LIKE N'%Role%'
           OR cc.name COLLATE DATABASE_DEFAULT LIKE N'%Role%');

    IF LEN(@dropRoleSql) > 0
        EXEC sp_executesql @dropRoleSql;

    UPDATE dbo.Users
    SET Role = N'Reader'
    WHERE Role IS NULL
       OR LTRIM(RTRIM(Role)) = N''
       OR Role = N'User'
       OR Role NOT IN (N'Admin', N'Editor', N'Author', N'Reader');

    ALTER TABLE dbo.Users WITH CHECK
    ADD CONSTRAINT [CK_Users_Role_Workflow]
    CHECK ([Role] IN (N'Admin', N'Editor', N'Author', N'Reader', N'User'));
END";
                        using (var cmd = new SqlCommand(roleConstraintSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        _roleConstraintChecked = true;
                    }
                }
            }

            const string syncAuthorsSql = @"
IF OBJECT_ID(N'dbo.Authors', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    UPDATE au
    SET au.UserId = u.UserId
    FROM dbo.Authors au
    INNER JOIN dbo.Users u
        ON au.UserId IS NULL
       AND u.Role = N'Author'
       AND u.Email IS NOT NULL
       AND au.Email IS NOT NULL
       AND LOWER(LTRIM(RTRIM(au.Email))) = LOWER(LTRIM(RTRIM(u.Email)))
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Authors ex WHERE ex.UserId = u.UserId);

    INSERT INTO dbo.Authors (AuthorName, Email, Images, UserId)
    SELECT
        LEFT(ISNULL(NULLIF(LTRIM(RTRIM(u.FullName)), N''), u.Username), 150),
        LEFT(ISNULL(NULLIF(LTRIM(RTRIM(u.Email)), N''), u.Username + N'@newsportal.local'), 150),
        CASE WHEN u.AvatarUrl IS NOT NULL AND LEN(u.AvatarUrl) <= 500 THEN u.AvatarUrl ELSE NULL END,
        u.UserId
    FROM dbo.Users u
    WHERE u.Role = N'Author'
      AND NOT EXISTS (SELECT 1 FROM dbo.Authors au WHERE au.UserId = u.UserId);
END";
            using (var syncCmd = new SqlCommand(syncAuthorsSql, conn))
            {
                syncCmd.ExecuteNonQuery();
            }
        }

        private void SyncSingleAuthorProfile(SqlConnection conn, int userId)
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.Authors', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE UserId = @uid)
    BEGIN
        DECLARE @uName NVARCHAR(50), @fName NVARCHAR(100), @uEmail NVARCHAR(256), @uAvatar NVARCHAR(MAX);
        SELECT @uName = Username, @fName = FullName, @uEmail = Email, @uAvatar = AvatarUrl
        FROM dbo.Users WHERE UserId = @uid;

        IF @uName IS NOT NULL
        BEGIN
            DECLARE @existingAuthorId INT = NULL;
            IF @uEmail IS NOT NULL AND LTRIM(RTRIM(@uEmail)) <> N''
            BEGIN
                SELECT TOP 1 @existingAuthorId = AuthorID
                FROM dbo.Authors
                WHERE UserId IS NULL
                  AND Email IS NOT NULL
                  AND LOWER(LTRIM(RTRIM(Email))) = LOWER(LTRIM(RTRIM(@uEmail)));
            END

            IF @existingAuthorId IS NOT NULL
            BEGIN
                UPDATE dbo.Authors SET UserId = @uid WHERE AuthorID = @existingAuthorId;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.Authors (AuthorName, Email, Images, UserId)
                VALUES (
                    LEFT(ISNULL(NULLIF(LTRIM(RTRIM(@fName)), N''), @uName), 150),
                    LEFT(ISNULL(NULLIF(LTRIM(RTRIM(@uEmail)), N''), @uName + N'@newsportal.local'), 150),
                    CASE WHEN @uAvatar IS NOT NULL AND LEN(@uAvatar) <= 500 THEN @uAvatar ELSE NULL END,
                    @uid
                );
            END
        END
    END
END";
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();
            }
        }

        private static string NormalizeValidRole(string rawRole)
        {
            rawRole = (rawRole ?? string.Empty).Trim();
            if (string.Equals(rawRole, "Admin", StringComparison.OrdinalIgnoreCase))
                return "Admin";
            if (string.Equals(rawRole, "Editor", StringComparison.OrdinalIgnoreCase))
                return "Editor";
            if (string.Equals(rawRole, "Author", StringComparison.OrdinalIgnoreCase))
                return "Author";
            if (string.Equals(rawRole, "Reader", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawRole, "User", StringComparison.OrdinalIgnoreCase))
                return "Reader";
            return null;
        }

        private static string GetVietnameseRoleLabel(string role)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return "Quản trị viên (Admin)";
            if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
                return "Biên tập viên (Editor)";
            if (string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                return "Tác giả (Author)";
            return "Độc giả (Reader)";
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

        // Điều hướng nhanh các đường dẫn phụ trong thanh điều hướng Admin
        public ActionResult Articles()
        {
            return RedirectToRoute("AdminArticles", new { action = "Index" });
        }

        public ActionResult Categories()
        {
            return RedirectToRoute("AdminCategories", new { action = "Index" });
        }

        public ActionResult Authors()
        {
            return RedirectToRoute("AdminAuthors", new { action = "Index" });
        }

        // GET: /Admin
        public ActionResult Index(string search = "", string status = "", int page = 1)
        {
            if (Session["Admin"] == null)
                return RedirectToRoute("AdminArticles", new { action = "Index" });

            var model = new AdminDashboardViewModel
            {
                SearchQuery = search ?? "",
                StatusFilter = status ?? "",
                ArticlesPageSize = 10
            };

            int pendingArticles = 0;
            int rejectedArticles = 0;
            int archivedArticles = 0;
            var rejectionReasons = new Dictionary<int, string>();
            var categoryStats = new List<AdminCategoryStatusStat>();
            var articleRatingStats = new List<AdminArticleRatingStat>();
            var starCounts = new int[6];
            int totalRatings = 0;
            double overallAverageRating = 0;

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureWorkflowStatusConstraint(conn);
                EnsureUserRoleConstraintAndSyncAuthors(conn);

                string statsSql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM dbo.Articles) AS TotalArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Published') AS PublishedArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Draft') AS DraftArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Pending') AS PendingArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Rejected') AS RejectedArticles,
                        (SELECT COUNT(*) FROM dbo.Articles WHERE Status = N'Archived') AS ArchivedArticles,
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
                        pendingArticles = Convert.ToInt32(reader["PendingArticles"]);
                        rejectedArticles = Convert.ToInt32(reader["RejectedArticles"]);
                        archivedArticles = Convert.ToInt32(reader["ArchivedArticles"]);
                        model.TotalViews = Convert.ToInt32(reader["TotalViews"]);
                        model.TotalCategories = Convert.ToInt32(reader["TotalCategories"]);
                        model.TotalAuthors = Convert.ToInt32(reader["TotalAuthors"]);
                        model.TotalUsers = Convert.ToInt32(reader["TotalUsers"]);
                    }
                }

                const string articleCountSql = @"
                    SELECT COUNT(*)
                    FROM dbo.Articles a
                    WHERE (@search = N'' OR a.Title LIKE N'%' + @search + N'%')
                      AND (@status = N'' OR a.Status = @status);";

                using (var countCmd = new SqlCommand(articleCountSql, conn))
                {
                    countCmd.Parameters.AddWithValue("@search", model.SearchQuery.Trim());
                    countCmd.Parameters.AddWithValue("@status", model.StatusFilter.Trim());
                    model.FilteredArticleCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                model.TotalArticlePages = Math.Max(1,
                    (int)Math.Ceiling(model.FilteredArticleCount / (double)model.ArticlesPageSize));
                model.CurrentArticlePage = Math.Min(Math.Max(page, 1), model.TotalArticlePages);
                int articleOffset = (model.CurrentArticlePage - 1) * model.ArticlesPageSize;

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
                    WHERE (@search = N'' OR a.Title LIKE N'%' + @search + N'%')
                      AND (@status = N'' OR a.Status = @status)
                    ORDER BY CASE WHEN a.Status = N'Pending' THEN 0 ELSE 1 END,
                             a.CreatedDate DESC,
                             a.ArticleID DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                using (var cmd = new SqlCommand(listSql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", model.SearchQuery.Trim());
                    cmd.Parameters.AddWithValue("@status", model.StatusFilter.Trim());
                    cmd.Parameters.AddWithValue("@offset", articleOffset);
                    cmd.Parameters.AddWithValue("@pageSize", model.ArticlesPageSize);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int artId = Convert.ToInt32(reader["ArticleID"]);
                            string rejReason = reader["RejectionReason"] == DBNull.Value
                                ? null
                                : Convert.ToString(reader["RejectionReason"]);

                            if (!string.IsNullOrWhiteSpace(rejReason))
                            {
                                rejectionReasons[artId] = rejReason;
                            }

                            model.Articles.Add(new AdminArticleRow
                            {
                                ArticleID = artId,
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

                const string categoryStatsSql = @"
                    SELECT c.CategoryID, c.CategoryName,
                        COUNT(a.ArticleID) AS TotalArticles,
                        SUM(CASE WHEN a.Status = N'Published' THEN 1 ELSE 0 END) AS PublishedCount,
                        SUM(CASE WHEN a.Status = N'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN a.Status = N'Draft' THEN 1 ELSE 0 END) AS DraftCount,
                        SUM(CASE WHEN a.Status = N'Rejected' THEN 1 ELSE 0 END) AS RejectedCount,
                        SUM(CASE WHEN a.Status = N'Archived' THEN 1 ELSE 0 END) AS ArchivedCount,
                        ISNULL(SUM(a.ViewCount), 0) AS TotalViews
                    FROM dbo.Categories c
                    LEFT JOIN dbo.Articles a ON a.CategoryID = c.CategoryID
                    GROUP BY c.CategoryID, c.CategoryName
                    ORDER BY c.CategoryName;";

                using (var cmd = new SqlCommand(categoryStatsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categoryStats.Add(new AdminCategoryStatusStat
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

                const string ratingSummarySql = @"
                    SELECT COUNT(*) AS TotalRatings,
                        ISNULL(AVG(CAST(Rating AS decimal(10, 2))), 0) AS AverageRating,
                        ISNULL(SUM(CASE WHEN Rating = 1 THEN 1 ELSE 0 END), 0) AS OneStarCount,
                        ISNULL(SUM(CASE WHEN Rating = 2 THEN 1 ELSE 0 END), 0) AS TwoStarCount,
                        ISNULL(SUM(CASE WHEN Rating = 3 THEN 1 ELSE 0 END), 0) AS ThreeStarCount,
                        ISNULL(SUM(CASE WHEN Rating = 4 THEN 1 ELSE 0 END), 0) AS FourStarCount,
                        ISNULL(SUM(CASE WHEN Rating = 5 THEN 1 ELSE 0 END), 0) AS FiveStarCount
                    FROM dbo.ArticleRatings;";

                using (var cmd = new SqlCommand(ratingSummarySql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalRatings = Convert.ToInt32(reader["TotalRatings"]);
                        overallAverageRating = Convert.ToDouble(reader["AverageRating"]);
                        starCounts[1] = Convert.ToInt32(reader["OneStarCount"]);
                        starCounts[2] = Convert.ToInt32(reader["TwoStarCount"]);
                        starCounts[3] = Convert.ToInt32(reader["ThreeStarCount"]);
                        starCounts[4] = Convert.ToInt32(reader["FourStarCount"]);
                        starCounts[5] = Convert.ToInt32(reader["FiveStarCount"]);
                    }
                }

                const string articleRatingStatsSql = @"
                    SELECT a.ArticleID, a.Title,
                        ISNULL(c.CategoryName, N'Chưa phân loại') AS CategoryName,
                        AVG(CAST(r.Rating AS decimal(10, 2))) AS AverageRating,
                        COUNT(*) AS RatingCount,
                        SUM(CASE WHEN r.Rating = 5 THEN 1 ELSE 0 END) AS FiveStarCount,
                        SUM(CASE WHEN r.Rating <= 2 THEN 1 ELSE 0 END) AS LowStarCount
                    FROM dbo.ArticleRatings r
                    INNER JOIN dbo.Articles a ON a.ArticleID = r.ArticleID
                    LEFT JOIN dbo.Categories c ON c.CategoryID = a.CategoryID
                    GROUP BY a.ArticleID, a.Title, c.CategoryName
                    ORDER BY RatingCount DESC, a.Title;";

                using (var cmd = new SqlCommand(articleRatingStatsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        articleRatingStats.Add(new AdminArticleRatingStat
                        {
                            ArticleID = Convert.ToInt32(reader["ArticleID"]),
                            Title = Convert.ToString(reader["Title"]),
                            CategoryName = Convert.ToString(reader["CategoryName"]),
                            AverageRating = Convert.ToDouble(reader["AverageRating"]),
                            RatingCount = Convert.ToInt32(reader["RatingCount"]),
                            FiveStarCount = Convert.ToInt32(reader["FiveStarCount"]),
                            LowStarCount = Convert.ToInt32(reader["LowStarCount"])
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
        public ActionResult UpdateArticleStatus(int id, string newStatus, string rejectionReason = "", string search = "", string status = "")
        {
            newStatus = (newStatus ?? "").Trim();
            rejectionReason = (rejectionReason ?? "").Trim();
            if (rejectionReason.Length > 500)
                rejectionReason = rejectionReason.Substring(0, 500);

            if (!ValidStatuses.Contains(newStatus))
            {
                TempData["AdminError"] = "Trạng thái bài viết không hợp lệ.";
                return RedirectToAction("Index", new { search = search, status = status });
            }

            if (newStatus == "Rejected" && string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["AdminError"] = "Vui lòng nhập lý do từ chối bài viết.";
                return RedirectToAction("Index", new { search = search, status = status });
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureWorkflowStatusConstraint(conn);

                const string sql = @"
                    UPDATE dbo.Articles
                    SET Status = @newStatus,
                        RejectionReason = CASE
                            WHEN @newStatus = N'Rejected' THEN @reason
                            WHEN @newStatus = N'Published' THEN NULL
                            ELSE RejectionReason
                        END,
                        UpdatedDate = GETDATE()
                    WHERE ArticleID = @id;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@newStatus", newStatus);
                    cmd.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(rejectionReason) ? (object)DBNull.Value : rejectionReason);
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
                    TempData["AdminMessage"] = "Đã từ chối bài viết #" + id + " — trả về cho tác giả chỉnh sửa (Rejected).";
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
        public ActionResult Users(string search = "", string roleFilter = "", string activeFilter = "", int page = 1)
        {
            search = (search ?? string.Empty).Trim();
            roleFilter = (roleFilter ?? string.Empty).Trim();
            activeFilter = (activeFilter ?? string.Empty).Trim();
            const int pageSize = 8;

            var users = new List<AdminUserRow>();
            int activeAdminCount = 0;
            int currentPage = 1;
            int totalPages = 1;
            int filteredUserCount = 0;
            int totalAll = 0, countAdmin = 0, countEditor = 0, countAuthor = 0, countReader = 0, countLocked = 0;

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureUserRoleConstraintAndSyncAuthors(conn);

                const string summarySql = @"
                    SELECT
                        COUNT(*) AS TotalAll,
                        ISNULL(SUM(CASE WHEN Role = N'Admin' THEN 1 ELSE 0 END), 0) AS CountAdmin,
                        ISNULL(SUM(CASE WHEN Role = N'Editor' THEN 1 ELSE 0 END), 0) AS CountEditor,
                        ISNULL(SUM(CASE WHEN Role = N'Author' THEN 1 ELSE 0 END), 0) AS CountAuthor,
                        ISNULL(SUM(CASE WHEN Role IN (N'Reader', N'User') THEN 1 ELSE 0 END), 0) AS CountReader,
                        ISNULL(SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END), 0) AS CountLocked,
                        ISNULL(SUM(CASE WHEN IsActive = 1 AND Role = N'Admin' THEN 1 ELSE 0 END), 0) AS ActiveAdminCount
                    FROM dbo.Users;";

                using (var sumCmd = new SqlCommand(summarySql, conn))
                using (var sr = sumCmd.ExecuteReader())
                {
                    if (sr.Read())
                    {
                        totalAll = Convert.ToInt32(sr["TotalAll"]);
                        countAdmin = Convert.ToInt32(sr["CountAdmin"]);
                        countEditor = Convert.ToInt32(sr["CountEditor"]);
                        countAuthor = Convert.ToInt32(sr["CountAuthor"]);
                        countReader = Convert.ToInt32(sr["CountReader"]);
                        countLocked = Convert.ToInt32(sr["CountLocked"]);
                        activeAdminCount = Convert.ToInt32(sr["ActiveAdminCount"]);
                    }
                }

                const string matchingUsersCountSql = @"
                    SELECT COUNT(*)
                    FROM dbo.Users
                    WHERE (@search = N''
                        OR Username LIKE N'%' + @search + N'%'
                        OR FullName LIKE N'%' + @search + N'%'
                        OR Email LIKE N'%' + @search + N'%'
                        OR Phone LIKE N'%' + @search + N'%'
                        OR Role LIKE N'%' + @search + N'%')
                      AND (@roleFilter = N''
                        OR (@roleFilter = N'Reader' AND Role IN (N'Reader', N'User'))
                        OR Role = @roleFilter)
                      AND (@activeFilter = N''
                        OR (@activeFilter = N'active' AND IsActive = 1)
                        OR (@activeFilter = N'locked' AND IsActive = 0));";

                using (var countCmd = new SqlCommand(matchingUsersCountSql, conn))
                {
                    countCmd.Parameters.AddWithValue("@search", search);
                    countCmd.Parameters.AddWithValue("@roleFilter", roleFilter);
                    countCmd.Parameters.AddWithValue("@activeFilter", activeFilter);
                    filteredUserCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                totalPages = Math.Max(1, (int)Math.Ceiling(filteredUserCount / (double)pageSize));
                currentPage = Math.Min(Math.Max(page, 1), totalPages);
                int offset = (currentPage - 1) * pageSize;

                string sql = @"
                    SELECT UserId, Username, FullName, Email, BirthDate, Gender, Phone, Address, Role, IsActive, CreatedAt AS CreatedDate
                    FROM dbo.Users
                    WHERE (@search = N''
                        OR Username LIKE N'%' + @search + N'%'
                        OR FullName LIKE N'%' + @search + N'%'
                        OR Email LIKE N'%' + @search + N'%'
                        OR Phone LIKE N'%' + @search + N'%'
                        OR Role LIKE N'%' + @search + N'%')
                      AND (@roleFilter = N''
                        OR (@roleFilter = N'Reader' AND Role IN (N'Reader', N'User'))
                        OR Role = @roleFilter)
                      AND (@activeFilter = N''
                        OR (@activeFilter = N'active' AND IsActive = 1)
                        OR (@activeFilter = N'locked' AND IsActive = 0))
                    ORDER BY UserId ASC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@search", search);
                    cmd.Parameters.AddWithValue("@roleFilter", roleFilter);
                    cmd.Parameters.AddWithValue("@activeFilter", activeFilter);
                    cmd.Parameters.AddWithValue("@offset", offset);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string rawRole = Convert.ToString(reader["Role"]);
                            string displayRole = NormalizeValidRole(rawRole) ?? "Reader";

                            users.Add(new AdminUserRow
                            {
                                UserID = Convert.ToInt32(reader["UserId"]),
                                Username = Convert.ToString(reader["Username"]),
                                FullName = reader["FullName"] == DBNull.Value ? "" : Convert.ToString(reader["FullName"]),
                                Email = reader["Email"] == DBNull.Value ? "" : Convert.ToString(reader["Email"]),
                                BirthDate = reader["BirthDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BirthDate"]),
                                Gender = reader["Gender"] == DBNull.Value ? "" : Convert.ToString(reader["Gender"]),
                                Phone = reader["Phone"] == DBNull.Value ? "" : Convert.ToString(reader["Phone"]),
                                Address = reader["Address"] == DBNull.Value ? "" : Convert.ToString(reader["Address"]),
                                Role = displayRole,
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                            });
                        }
                    }
                }
            }

            string currentAdminUsername = Convert.ToString(Session["Admin"]);

            foreach (var user in users)
            {
                bool isRootAdmin = string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase);
                bool isSelf = !string.IsNullOrWhiteSpace(currentAdminUsername) &&
                    string.Equals(currentAdminUsername, user.Username, StringComparison.OrdinalIgnoreCase);
                bool isLastActiveAdmin = user.IsActive &&
                    string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                    activeAdminCount <= 1;

                user.CanToggleStatus = !isLastActiveAdmin && !(isRootAdmin && user.IsActive) && !(isSelf && user.IsActive);
                user.CanChangeRole = !isRootAdmin && !isSelf && !isLastActiveAdmin;
            }

            ViewBag.Search = search;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.ActiveFilter = activeFilter;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilteredCount = filteredUserCount;
            ViewBag.TotalAllUsers = totalAll;
            ViewBag.CountAdmin = countAdmin;
            ViewBag.CountEditor = countEditor;
            ViewBag.CountAuthor = countAuthor;
            ViewBag.CountReader = countReader;
            ViewBag.CountLocked = countLocked;

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
            string normalizedRole = NormalizeValidRole(string.IsNullOrWhiteSpace(role) ? "Reader" : role);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["AdminError"] = "Vui lòng nhập đầy đủ Tên đăng nhập, Mật khẩu, Họ tên và Email.";
                return RedirectToAction("Users");
            }

            if (username.Length < 3 || username.Length > 30 || Regex.IsMatch(username, @"\s"))
            {
                TempData["AdminError"] = "Tên đăng nhập phải từ 3 đến 30 ký tự và không chứa khoảng trắng.";
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

            if (normalizedRole == null)
            {
                TempData["AdminError"] = "Cấp bậc vai trò không hợp lệ. Vui lòng chọn Quản trị viên, Biên tập viên, Tác giả hoặc Độc giả.";
                return RedirectToAction("Users");
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureUserRoleConstraintAndSyncAuthors(conn);

                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Username = @u;", conn))
                {
                    checkCmd.Parameters.AddWithValue("@u", username);
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        TempData["AdminError"] = "Tên đăng nhập '" + username + "' đã tồn tại.";
                        return RedirectToAction("Users");
                    }
                }

                using (var checkEmailCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users WHERE Email = @email;", conn))
                {
                    checkEmailCmd.Parameters.AddWithValue("@email", email);
                    if (Convert.ToInt32(checkEmailCmd.ExecuteScalar()) > 0)
                    {
                        TempData["AdminError"] = "Email này đã được sử dụng cho tài khoản khác.";
                        return RedirectToAction("Users");
                    }
                }

                const string insertSql = @"
                    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                    OUTPUT INSERTED.UserId
                    VALUES (@u, @p, @fn, @em, @role, 1, GETDATE());";

                int createdUserId;
                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", ComputeSha256(password.Trim()));
                    cmd.Parameters.AddWithValue("@fn", fullName);
                    cmd.Parameters.AddWithValue("@em", email);
                    cmd.Parameters.AddWithValue("@role", normalizedRole);
                    createdUserId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (normalizedRole == "Author")
                {
                    SyncSingleAuthorProfile(conn, createdUserId);
                }
            }

            TempData["AdminMessage"] = "Đã tạo tài khoản '" + username + "' với cấp bậc " + GetVietnameseRoleLabel(normalizedRole) + ".";
            return RedirectToAction("Users");
        }

        // POST: /Admin/UpdateUserRole (Hỗ trợ lưu đầy đủ 4 cấp: Admin, Editor, Author, Reader + đồng bộ bảng Authors)
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateUserRole(int id, string role, string search = "", string roleFilter = "", string activeFilter = "")
        {
            string normalizedRole = NormalizeValidRole(role);
            if (normalizedRole == null)
            {
                TempData["AdminError"] = "Cấp bậc vai trò không hợp lệ. Chỉ chấp nhận Admin, Editor, Author hoặc Reader.";
                return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
            }

            string targetUsername = "";

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureUserRoleConstraintAndSyncAuthors(conn);

                const string accountSql = "SELECT Username, Role, IsActive FROM dbo.Users WHERE UserId = @id;";
                string currentRole;
                bool isActive;

                using (var cmd = new SqlCommand(accountSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            TempData["AdminError"] = "Không tìm thấy tài khoản cần đổi cấp bậc.";
                            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                        }

                        targetUsername = Convert.ToString(reader["Username"]);
                        currentRole = NormalizeValidRole(Convert.ToString(reader["Role"])) ?? "Reader";
                        isActive = Convert.ToBoolean(reader["IsActive"]);
                    }
                }

                bool isRootAdmin = string.Equals(targetUsername, "admin", StringComparison.OrdinalIgnoreCase);
                bool isSelf = string.Equals(Convert.ToString(Session["Admin"]), targetUsername, StringComparison.OrdinalIgnoreCase);

                if (normalizedRole != "Admin" && (isRootAdmin || isSelf))
                {
                    TempData["AdminError"] = "Không thể hạ cấp tài khoản Quản trị viên gốc hoặc tài khoản đang đăng nhập.";
                    return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                }

                if (isActive && string.Equals(currentRole, "Admin", StringComparison.OrdinalIgnoreCase) && normalizedRole != "Admin")
                {
                    const string countSql = "SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin' AND IsActive = 1;";
                    using (var countCmd = new SqlCommand(countSql, conn))
                    {
                        if (Convert.ToInt32(countCmd.ExecuteScalar()) <= 1)
                        {
                            TempData["AdminError"] = "Không thể hạ cấp Quản trị viên đang hoạt động cuối cùng.";
                            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                        }
                    }
                }

                const string updateSql = "UPDATE dbo.Users SET Role = @role WHERE UserId = @id;";
                using (var updateCmd = new SqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@role", normalizedRole);
                    updateCmd.Parameters.AddWithValue("@id", id);
                    updateCmd.ExecuteNonQuery();
                }

                if (normalizedRole == "Author")
                {
                    SyncSingleAuthorProfile(conn, id);
                }
            }

            TempData["AdminMessage"] = "Đã lưu cấp bậc của tài khoản '" + targetUsername + "' thành " + GetVietnameseRoleLabel(normalizedRole) + ".";
            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
        }

        // POST: /Admin/ToggleUserActive
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserActive(int id, string search = "", string roleFilter = "", string activeFilter = "")
        {
            string username = "";
            bool wasActive = false;

            using (var conn = CreateConnection())
            {
                conn.Open();
                EnsureUserRoleConstraintAndSyncAuthors(conn);

                const string accountSql = "SELECT Username, Role, IsActive FROM dbo.Users WHERE UserId = @id;";
                string role;

                using (var accountCmd = new SqlCommand(accountSql, conn))
                {
                    accountCmd.Parameters.AddWithValue("@id", id);
                    using (var reader = accountCmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            TempData["AdminError"] = "Không tìm thấy tài khoản.";
                            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                        }

                        username = Convert.ToString(reader["Username"]);
                        role = Convert.ToString(reader["Role"]);
                        wasActive = Convert.ToBoolean(reader["IsActive"]);
                    }
                }

                bool isSelf = string.Equals(Convert.ToString(Session["Admin"]), username, StringComparison.OrdinalIgnoreCase);

                if (wasActive && (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) || isSelf))
                {
                    TempData["AdminError"] = "Không thể khóa tài khoản Quản trị viên gốc hoặc tài khoản bạn đang đăng nhập.";
                    return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                }

                if (wasActive && string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    const string countSql = "SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin' AND IsActive = 1;";
                    using (var countCmd = new SqlCommand(countSql, conn))
                    {
                        if (Convert.ToInt32(countCmd.ExecuteScalar()) <= 1)
                        {
                            TempData["AdminError"] = "Không thể khóa Quản trị viên đang hoạt động cuối cùng.";
                            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                        }
                    }
                }

                const string sql = "UPDATE dbo.Users SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE UserId = @id;";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        TempData["AdminError"] = "Không tìm thấy tài khoản.";
                        return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
                    }
                }
            }

            TempData["AdminMessage"] = (wasActive ? "Đã khóa tài khoản '" : "Đã mở khóa tài khoản '") + username + "'.";
            return RedirectToAction("Users", new { search = search, roleFilter = roleFilter, activeFilter = activeFilter });
        }

        // GET: /Admin/Comments (Cho phép cả Admin và Editor kiểm duyệt bình luận)
        public ActionResult Comments(string search, string status, int page = 1)
        {
            search = (search ?? string.Empty).Trim();
            status = (status ?? string.Empty).Trim();
            const int pageSize = 8;

            var comments = new List<AdminCommentRow>();
            int currentPage = 1;
            int totalPages = 1;
            int filteredCommentCount = 0;
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

                const string matchingCommentsCountSql = @"
                    SELECT COUNT(*)
                    FROM dbo.Comments c
                    LEFT JOIN dbo.Articles a ON c.ArticleID = a.ArticleID
                    LEFT JOIN dbo.Users u ON c.UserId = u.UserId
                    WHERE (@status = N'' OR c.Status = @status)
                      AND (@search = N''
                           OR c.CommenterName LIKE N'%' + @search + N'%'
                           OR c.Content LIKE N'%' + @search + N'%'
                           OR a.Title LIKE N'%' + @search + N'%'
                           OR u.Username LIKE N'%' + @search + N'%');";

                using (var matchingCountCmd = new SqlCommand(matchingCommentsCountSql, conn))
                {
                    matchingCountCmd.Parameters.AddWithValue("@status", status);
                    matchingCountCmd.Parameters.AddWithValue("@search", search);
                    filteredCommentCount = Convert.ToInt32(matchingCountCmd.ExecuteScalar());
                }

                totalPages = Math.Max(1, (int)Math.Ceiling(filteredCommentCount / (double)pageSize));
                currentPage = Math.Min(Math.Max(page, 1), totalPages);
                int offset = (currentPage - 1) * pageSize;

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
                        CASE WHEN c.Status = N'Pending' THEN 0
                             WHEN c.Status = N'Approved' THEN 1
                             ELSE 2 END,
                         c.CreatedDate DESC,
                         c.CommentID DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@search", search);
                    cmd.Parameters.AddWithValue("@offset", offset);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);

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
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilteredCount = filteredCommentCount;
            ViewBag.TotalCount = totalCount;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = approvedCount;
            ViewBag.HiddenCount = hiddenCount;

            return View(comments);
        }

        // POST: /Admin/ApproveComment
        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}
