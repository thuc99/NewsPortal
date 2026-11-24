using NewsPortal.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AuthorsController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        private bool IsAdminManagementRequest()
        {
            return (Request.AppRelativeCurrentExecutionFilePath ?? string.Empty)
                .StartsWith("~/Admin/Author", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult CheckAdminManagementAccess()
        {
            if (!IsAdminManagementRequest() || Session["Admin"] != null)
                return null;

            string role = Convert.ToString(Session["Role"]);
            if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToRoute("AdminArticles", new { action = "Index" });
            }

            return Session["User"] != null
                ? (ActionResult)RedirectToRoute("PublicAuthor", new { action = "Index" })
                : RedirectToAction("Login", "Account");
        }

        private ActionResult RedirectToAuthorsIndex()
        {
            return IsAdminManagementRequest()
                ? (ActionResult)RedirectToRoute("AdminAuthors", new { action = "Index" })
                : RedirectToRoute("PublicAuthor", new { action = "Index" });
        }

        // Đồng bộ tất cả tài khoản có Role = 'Author' sang bảng dbo.Authors (an toàn với UX_Authors_UserId)
        private void SyncAuthorAccounts()
        {
            const string sql = @"
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
            try
            {
                db.Database.ExecuteSqlCommand(sql);
            }
            catch
            {
                // Bỏ qua nếu CSDL đang khởi tạo
            }
        }

        // Chuẩn hóa đường dẫn ảnh tác giả (hỗ trợ cả ~/Content/Avatars/..., /Content/Images/Authors/..., URL ngoài và tên file)
        private string NormalizeImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim();

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            if (path.StartsWith("~"))
            {
                return VirtualPathUtility.ToAbsolute(path);
            }

            return "/Content/Images/Authors/" + path.TrimStart('/');
        }

        // Đọc đường dẫn ảnh tác giả (ưu tiên ảnh riêng trong dbo.Authors, dự phòng sang AvatarUrl của tài khoản liên kết)
        private string GetAuthorImage(Author author)
        {
            if (author == null) return null;

            string rawPath = db.Database.SqlQuery<string>(
                "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                author.AuthorID
            ).FirstOrDefault();

            string normalized = NormalizeImagePath(rawPath);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;

            if (author.UserId.HasValue)
            {
                string userAvatar = db.Users
                    .Where(u => u.UserId == author.UserId.Value)
                    .Select(u => u.AvatarUrl)
                    .FirstOrDefault();

                return NormalizeImagePath(userAvatar);
            }

            return null;
        }

        // Nạp ảnh hàng loạt cho danh sách tác giả
        private void LoadAuthorImages(IList<Author> authors)
        {
            if (authors == null || authors.Count == 0) return;

            var linkedUserIds = authors
                .Where(a => a.UserId.HasValue)
                .Select(a => a.UserId.Value)
                .Distinct()
                .ToList();

            var userAvatars = linkedUserIds.Count > 0
                ? db.Users
                    .Where(u => linkedUserIds.Contains(u.UserId))
                    .ToDictionary(u => u.UserId, u => u.AvatarUrl)
                : new Dictionary<int, string>();

            foreach (var author in authors)
            {
                string normalized = NormalizeImagePath(author.Images);
                if (string.IsNullOrEmpty(normalized) && author.UserId.HasValue)
                {
                    string uAvatar;
                    if (userAvatars.TryGetValue(author.UserId.Value, out uAvatar))
                    {
                        normalized = NormalizeImagePath(uAvatar);
                    }
                }
                author.Images = normalized;
            }
        }

        // Kiểm tra file ảnh tải lên
        private bool ValidateImage(HttpPostedFileBase imageFile, out string error)
        {
            error = null;

            if (imageFile == null || imageFile.ContentLength == 0)
                return true;

            if (imageFile.ContentLength > 5 * 1024 * 1024)
            {
                error = "Ảnh không được vượt quá 5 MB.";
                return false;
            }

            string extension = (Path.GetExtension(imageFile.FileName) ?? string.Empty)
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                error = "Chỉ chấp nhận định dạng ảnh JPG, JPEG, PNG, GIF hoặc WEBP.";
                return false;
            }

            return true;
        }

        // Lưu file ảnh vào thư mục ~/Content/Images/Authors/
        private string SaveAuthorImage(HttpPostedFileBase imageFile)
        {
            if (imageFile == null || imageFile.ContentLength == 0)
                return null;

            string extension = Path.GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            string folder = Server.MapPath("~/Content/Images/Authors/");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid().ToString("N") + extension;
            string fullPath = Path.Combine(folder, fileName);

            imageFile.SaveAs(fullPath);

            return "/Content/Images/Authors/" + fileName;
        }

        // Xóa file ảnh cũ thuộc thư mục ~/Content/Images/Authors/ (không xóa nhầm ảnh Avatar tài khoản)
        private void TryDeleteAuthorImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string virtualPath = imagePath.StartsWith("~")
                    ? imagePath
                    : (imagePath.StartsWith("/") ? "~" + imagePath : "~/Content/Images/Authors/" + imagePath);

                if (virtualPath.IndexOf("/Content/Images/Authors/", StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                string fullPath = Server.MapPath(virtualPath);

                if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch
            {
                // Bỏ qua nếu file không tồn tại hoặc đang được sử dụng
            }
        }

        // =========================
        // DANH SÁCH TÁC GIẢ & HỒ SƠ TÁC GIẢ
        // =========================
        public ActionResult Index(string search, int? id, string returnUrl = null, int page = 1)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            SyncAuthorAccounts();

            bool isAdminManagement = IsAdminManagementRequest();

            ViewBag.IsAdminManagement = isAdminManagement;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Search = search;

            // Admin xem toàn bộ tác giả; trang công khai ẩn các tác giả có tài khoản đang bị khóa
            var baseAuthorsQuery = isAdminManagement
                ? db.Authors.AsQueryable()
                : db.Authors.Where(a => !a.UserId.HasValue ||
                    db.Users.Any(u => u.UserId == a.UserId.Value && u.IsActive));

            if (id.HasValue)
            {
                var selectedAuthor = baseAuthorsQuery.FirstOrDefault(a => a.AuthorID == id.Value);

                if (selectedAuthor == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin tác giả.";
                    return RedirectToAuthorsIndex();
                }

                selectedAuthor.Images = GetAuthorImage(selectedAuthor);

                var articlesQuery = db.Articles.Where(a => a.AuthorID == id.Value);
                if (!isAdminManagement)
                {
                    articlesQuery = articlesQuery.Where(a => a.Status == "Published");
                }

                var authorArticles = articlesQuery
                    .OrderByDescending(a => a.CreatedDate)
                    .ToList();

                ViewBag.SelectedAuthor = selectedAuthor;
                ViewBag.ShowAuthorProfile = true;
                ViewBag.Articles = authorArticles;
                ViewBag.ArticleCount = authorArticles.Count;

                var suggestedAuthors = baseAuthorsQuery
                    .Where(a => a.AuthorID != id.Value)
                    .OrderBy(a => a.AuthorName)
                    .Take(6)
                    .ToList();

                LoadAuthorImages(suggestedAuthors);
                ViewBag.SuggestedAuthors = suggestedAuthors;

                ViewBag.CategoryNames = db.Categories
                    .ToDictionary(c => c.CategoryID, c => c.CategoryName);

                return View(new List<Author> { selectedAuthor });
            }

            var filteredQuery = baseAuthorsQuery;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                filteredQuery = filteredQuery.Where(a =>
                    a.AuthorName.Contains(keyword) ||
                    (a.Email != null && a.Email.Contains(keyword)));
            }

            int totalAuthors = filteredQuery.Count();
            const int pageSize = 8;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalAuthors / (double)pageSize));
            int currentPage = isAdminManagement ? Math.Min(Math.Max(page, 1), totalPages) : 1;

            var orderedAuthors = filteredQuery.OrderBy(a => a.AuthorName).ThenBy(a => a.AuthorID);
            var authors = isAdminManagement
                ? orderedAuthors.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList()
                : orderedAuthors.ToList();
            LoadAuthorImages(authors);

            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = isAdminManagement ? totalPages : 1;

            // Đếm số bài viết cho từng tác giả để hiển thị trực quan trên bảng/thẻ
            var articleCounts = (isAdminManagement
                    ? db.Articles
                    : db.Articles.Where(a => a.Status == "Published"))
                .GroupBy(a => a.AuthorID)
                .Select(g => new { AuthorID = g.Key, Count = g.Count() })
                .ToDictionary(x => x.AuthorID, x => x.Count);

            var linkedUserIds = authors
                .Where(a => a.UserId.HasValue)
                .Select(a => a.UserId.Value)
                .Distinct()
                .ToList();

            var linkedUsernames = linkedUserIds.Count > 0
                ? db.Users
                    .Where(u => linkedUserIds.Contains(u.UserId))
                    .ToDictionary(u => u.UserId, u => u.Username + " (" + u.Role + ")")
                : new Dictionary<int, string>();

            ViewBag.ArticleCounts = articleCounts;
            ViewBag.LinkedUsernames = linkedUsernames;

            return View(authors);
        }

        // =========================
        // THÊM TÁC GIẢ - GET
        // =========================
        [AdminAuthorize]
        public ActionResult Create()
        {
            if (!IsAdminManagementRequest())
            {
                return RedirectToRoute("AdminAuthors", new { action = "Create" });
            }

            ViewBag.IsAdminManagement = true;
            return View(new Author());
        }

        // =========================
        // THÊM TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Author author, HttpPostedFileBase imageFile)
        {
            ViewBag.IsAdminManagement = true;

            if ((imageFile == null || imageFile.ContentLength == 0) && Request.Files != null && Request.Files.Count > 0)
            {
                imageFile = Request.Files[0];
            }

            if (author == null)
                author = new Author();

            author.AuthorName = (author.AuthorName ?? string.Empty).Trim();
            author.Email = (author.Email ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(author.Email) &&
                db.Authors.Any(a => a.Email != null && a.Email.ToLower() == author.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng cho tác giả khác.");
            }

            string imageError;
            if (!ValidateImage(imageFile, out imageError))
            {
                ModelState.AddModelError("Images", imageError);
            }

            if (ModelState.IsValid)
            {
                string imagePath = SaveAuthorImage(imageFile);

                // Tự động liên kết nếu email khớp với một tài khoản Users chưa có hồ sơ tác giả
                var matchedUser = db.Users.FirstOrDefault(u =>
                    u.Email != null && u.Email.ToLower() == author.Email.ToLower());

                if (matchedUser != null && !db.Authors.Any(a => a.UserId == matchedUser.UserId))
                {
                    author.UserId = matchedUser.UserId;
                }

                author.Images = imagePath;
                db.Authors.Add(author);
                db.SaveChanges();

                TempData["Success"] = "Đã thêm tác giả '" + author.AuthorName + "' thành công.";
                return RedirectToAuthorsIndex();
            }

            return View(author);
        }

        // =========================
        // SỬA TÁC GIẢ - GET
        // =========================
        [AdminAuthorize]
        public ActionResult Edit(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã tác giả không hợp lệ.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            if (!IsAdminManagementRequest())
            {
                return RedirectToRoute("AdminAuthors", new { action = "Edit", id = id.Value });
            }

            ViewBag.IsAdminManagement = true;

            var author = db.Authors.Find(id.Value);
            if (author == null)
            {
                TempData["Error"] = "Không tìm thấy tác giả cần chỉnh sửa.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            string directImage = db.Database.SqlQuery<string>(
                "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                author.AuthorID
            ).FirstOrDefault();

            ViewBag.HasCustomAuthorImage = !string.IsNullOrWhiteSpace(directImage);
            author.Images = GetAuthorImage(author);

            if (author.UserId.HasValue)
            {
                var linkedUser = db.Users.FirstOrDefault(u => u.UserId == author.UserId.Value);
                if (linkedUser != null)
                {
                    ViewBag.LinkedUsername = linkedUser.Username;
                    ViewBag.LinkedRole = linkedUser.Role;
                }
            }

            return View(author);
        }

        // =========================
        // SỬA TÁC GIẢ - POST (HỖ TRỢ THAY ẢNH HOẶC XÓA ẢNH)
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Author author, HttpPostedFileBase imageFile, bool removeImage = false)
        {
            ViewBag.IsAdminManagement = true;

            if ((imageFile == null || imageFile.ContentLength == 0) && Request.Files != null && Request.Files.Count > 0)
            {
                imageFile = Request.Files[0];
            }

            if (author == null || author.AuthorID <= 0)
            {
                TempData["Error"] = "Dữ liệu tác giả không hợp lệ.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            var existingAuthor = db.Authors.Find(author.AuthorID);
            if (existingAuthor == null)
            {
                TempData["Error"] = "Không tìm thấy tác giả cần cập nhật.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            author.AuthorName = (author.AuthorName ?? string.Empty).Trim();
            author.Email = (author.Email ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(author.Email) &&
                db.Authors.Any(a =>
                    a.AuthorID != author.AuthorID &&
                    a.Email != null &&
                    a.Email.ToLower() == author.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng cho tác giả khác.");
            }

            if (existingAuthor.UserId.HasValue && !string.IsNullOrEmpty(author.Email))
            {
                int linkedUid = existingAuthor.UserId.Value;
                if (db.Users.Any(u => u.UserId != linkedUid && u.Email != null && u.Email.ToLower() == author.Email.ToLower()))
                {
                    ModelState.AddModelError("Email", "Email này đang thuộc về một tài khoản người dùng khác.");
                }
            }

            string imageError;
            if (!ValidateImage(imageFile, out imageError))
            {
                ModelState.AddModelError("Images", imageError);
            }

            if (ModelState.IsValid)
            {
                string oldCustomImage = db.Database.SqlQuery<string>(
                    "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                    existingAuthor.AuthorID
                ).FirstOrDefault();

                existingAuthor.AuthorName = author.AuthorName;
                existingAuthor.Email = author.Email;

                // Đồng bộ họ tên & email sang tài khoản Users liên kết (nếu có)
                if (existingAuthor.UserId.HasValue)
                {
                    var linkedUser = db.Users.FirstOrDefault(u => u.UserId == existingAuthor.UserId.Value);
                    if (linkedUser != null)
                    {
                        linkedUser.FullName = author.AuthorName.Length > 100
                            ? author.AuthorName.Substring(0, 100)
                            : author.AuthorName;
                        linkedUser.Email = author.Email;
                    }
                }

                db.SaveChanges();

                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string newImage = SaveAuthorImage(imageFile);

                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Authors SET Images = @p0 WHERE AuthorID = @p1",
                        newImage,
                        existingAuthor.AuthorID
                    );

                    TryDeleteAuthorImage(oldCustomImage);
                }
                else if (removeImage)
                {
                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Authors SET Images = NULL WHERE AuthorID = @p0",
                        existingAuthor.AuthorID
                    );

                    TryDeleteAuthorImage(oldCustomImage);
                }

                TempData["Success"] = "Đã cập nhật thông tin tác giả '" + existingAuthor.AuthorName + "'.";
                return RedirectToAuthorsIndex();
            }

            author.Images = GetAuthorImage(existingAuthor);
            if (existingAuthor.UserId.HasValue)
            {
                var linkedUser = db.Users.FirstOrDefault(u => u.UserId == existingAuthor.UserId.Value);
                if (linkedUser != null)
                {
                    ViewBag.LinkedUsername = linkedUser.Username;
                    ViewBag.LinkedRole = linkedUser.Role;
                }
            }

            return View(author);
        }

        // =========================
        // XÓA RIÊNG ẢNH TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteImage(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
                return RedirectToRoute("AdminAuthors", new { action = "Index" });

            var author = db.Authors.Find(id.Value);
            if (author == null)
            {
                TempData["Error"] = "Không tìm thấy tác giả.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            string oldImage = db.Database.SqlQuery<string>(
                "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                id.Value
            ).FirstOrDefault();

            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.Authors SET Images = NULL WHERE AuthorID = @p0",
                id.Value
            );

            TryDeleteAuthorImage(oldImage);

            TempData["Success"] = "Đã xóa ảnh đại diện của tác giả.";
            return RedirectToRoute("AdminAuthors", new { action = "Edit", id = id.Value });
        }

        // =========================
        // XÓA TÁC GIẢ - GET
        // =========================
        [AdminAuthorize]
        public ActionResult Delete(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã tác giả không hợp lệ.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            if (!IsAdminManagementRequest())
            {
                return RedirectToRoute("AdminAuthors", new { action = "Delete", id = id.Value });
            }

            ViewBag.IsAdminManagement = true;

            var author = db.Authors.Find(id.Value);
            if (author == null)
            {
                TempData["Error"] = "Không tìm thấy tác giả cần xóa.";
                return RedirectToRoute("AdminAuthors", new { action = "Index" });
            }

            author.Images = GetAuthorImage(author);
            ViewBag.ArticleCount = db.Articles.Count(a => a.AuthorID == author.AuthorID);

            if (author.UserId.HasValue)
            {
                var linkedUser = db.Users.FirstOrDefault(u => u.UserId == author.UserId.Value);
                if (linkedUser != null)
                {
                    ViewBag.LinkedUsername = linkedUser.Username;
                }
            }

            return View(author);
        }

        // =========================
        // XÓA TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int? id)
        {
            ViewBag.IsAdminManagement = true;

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "Mã tác giả không hợp lệ.";
                return RedirectToAuthorsIndex();
            }

            var author = db.Authors.Find(id.Value);
            if (author == null)
            {
                TempData["Error"] = "Tác giả không tồn tại hoặc đã được xóa trước đó.";
                return RedirectToAuthorsIndex();
            }

            int articleCount = db.Articles.Count(a => a.AuthorID == id.Value);
            if (articleCount > 0)
            {
                TempData["Error"] = "Không thể xóa tác giả '" + author.AuthorName + "' vì vẫn còn " + articleCount + " bài viết thuộc tác giả này.";
                return RedirectToAuthorsIndex();
            }

            string oldCustomImage = db.Database.SqlQuery<string>(
                "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                id.Value
            ).FirstOrDefault();

            int? linkedUserId = author.UserId;
            string deletedName = author.AuthorName;

            db.Authors.Remove(author);
            db.SaveChanges();

            // Nếu tác giả gắn với tài khoản đang có Role = 'Author', chuyển tài khoản về 'Reader' để không tự sinh lại
            if (linkedUserId.HasValue)
            {
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Users SET Role = N'Reader' WHERE UserId = @p0 AND Role = N'Author'",
                    linkedUserId.Value
                );
            }

            TryDeleteAuthorImage(oldCustomImage);

            TempData["Success"] = "Đã xóa tác giả '" + deletedName + "' thành công.";
            return RedirectToAuthorsIndex();
        }

        // =========================
        // DETAILS
        // =========================
        public ActionResult Details(int? id)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null)
                return denied;

            if (!id.HasValue || id.Value <= 0)
            {
                return RedirectToAuthorsIndex();
            }

            return IsAdminManagementRequest()
                ? (ActionResult)RedirectToRoute("AdminAuthors", new { action = "Index", id = id.Value })
                : RedirectToRoute("PublicAuthor", new { action = "Index", id = id.Value });
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
