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

            if (string.Equals(Convert.ToString(Session["Role"]), "Editor",
                StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToRoute("AdminArticles", new { action = "Index" });
            }

            return Session["User"] != null
                ? (ActionResult)RedirectToAction("Index", "Authors")
                : RedirectToAction("Login", "Account");
        }

        private ActionResult RedirectToAuthorsIndex()
        {
            return IsAdminManagementRequest()
                ? (ActionResult)RedirectToRoute("AdminAuthors", new { action = "Index" })
                : RedirectToAction("Index");
        }

        // Chuẩn hóa đường dẫn ảnh
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

        // Đọc đường dẫn ảnh từ SQL Server
        private string GetAuthorImage(int authorId)
        {
            var rawPath = db.Database.SqlQuery<string>(
                "SELECT Images FROM dbo.Authors WHERE AuthorID = @p0",
                authorId
            ).FirstOrDefault();

            return NormalizeImagePath(rawPath);
        }

        // Nạp ảnh vào danh sách tác giả
        private void LoadAuthorImages(IEnumerable<Author> authors)
        {
            foreach (var author in authors)
            {
                author.Images = GetAuthorImage(author.AuthorID);
            }
        }

        // Kiểm tra file ảnh
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

            string extension = Path.GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                error = "Chỉ chấp nhận ảnh JPG, JPEG, PNG, GIF hoặc WEBP.";
                return false;
            }

            return true;
        }

        // Lưu file ảnh vào thư mục
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

        // Xóa file ảnh cũ trên ổ đĩa an toàn
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

                string fullPath = Server.MapPath(virtualPath);

                if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch
            {
                // Bỏ qua nếu file không tồn tại hoặc không thuộc thư mục ứng dụng
            }
        }

        // =========================
        // DANH SÁCH TÁC GIẢ
        // =========================
        public ActionResult Index(string search, int? id, string returnUrl = null)
        {
            var denied = CheckAdminManagementAccess();
            if (denied != null) return denied;

            bool isAdminManagement = IsAdminManagementRequest();

            ViewBag.IsAdminManagement = isAdminManagement;
            ViewBag.ReturnUrl = returnUrl;

            var query = db.Authors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();

                query = query.Where(a =>
                    a.AuthorName.Contains(keyword) ||
                    a.Email.Contains(keyword));
            }

            ViewBag.Search = search;

            int? selectedAuthorId = id;

            if (!selectedAuthorId.HasValue &&
                !isAdminManagement &&
                string.IsNullOrWhiteSpace(search))
            {
                var mostPublishedArticles = db.Articles
                    .Where(a => a.Status == "Published")
                    .GroupBy(a => a.AuthorID)
                    .Select(group => new
                    {
                        AuthorID = group.Key,
                        ArticleCount = group.Count()
                    })
                    .OrderByDescending(group => group.ArticleCount)
                    .ThenBy(group => group.AuthorID)
                    .FirstOrDefault();

                selectedAuthorId = mostPublishedArticles != null
                    ? (int?)mostPublishedArticles.AuthorID
                    : db.Authors
                        .OrderBy(a => a.AuthorName)
                        .Select(a => (int?)a.AuthorID)
                        .FirstOrDefault();
            }

            if (selectedAuthorId.HasValue)
            {
                var selectedAuthor = db.Authors.Find(selectedAuthorId.Value);

                if (selectedAuthor == null)
                    return HttpNotFound();

                selectedAuthor.Images = GetAuthorImage(selectedAuthor.AuthorID);

                var articlesQuery = db.Articles.Where(a =>
                    a.AuthorID == selectedAuthorId.Value &&
                    a.Status == "Published");

                var authorArticles = articlesQuery
                    .OrderByDescending(a => a.CreatedDate)
                    .ToList();

                ViewBag.SelectedAuthor = selectedAuthor;
                ViewBag.ShowAuthorProfile = true;
                ViewBag.Articles = authorArticles;
                ViewBag.ArticleCount = authorArticles.Count;

                var suggestedAuthors = db.Authors
                    .Where(a => a.AuthorID != selectedAuthorId.Value)
                    .OrderBy(a => a.AuthorName)
                    .Take(6)
                    .ToList();

                LoadAuthorImages(suggestedAuthors);

                ViewBag.SuggestedAuthors = suggestedAuthors;

                ViewBag.CategoryNames = db.Categories
                    .ToDictionary(c => c.CategoryID, c => c.CategoryName);
            }

            var authors = query.OrderBy(a => a.AuthorName).ToList();

            LoadAuthorImages(authors);

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
                return RedirectToRoute("AdminAuthors",
                    new { action = "Create" });
            }

            ViewBag.IsAdminManagement = true;

            return View();
        }

        // =========================
        // THÊM TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Author author, HttpPostedFileBase imageFile)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if ((imageFile == null || imageFile.ContentLength == 0) && Request.Files != null && Request.Files.Count > 0)
            {
                imageFile = Request.Files[0];
            }

            if (author.AuthorName != null)
                author.AuthorName = author.AuthorName.Trim();

            if (author.Email != null)
                author.Email = author.Email.Trim();

            if (db.Authors.Any(a => a.Email == author.Email))
            {
                ModelState.AddModelError("Email", "Email đã được sử dụng");
            }

            string imageError;

            if (!ValidateImage(imageFile, out imageError))
            {
                ModelState.AddModelError("Images", imageError);
            }

            if (ModelState.IsValid)
            {
                string imagePath = SaveAuthorImage(imageFile);

                db.Authors.Add(author);
                db.SaveChanges();

                if (!string.IsNullOrEmpty(imagePath))
                {
                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Authors SET Images = @p0 WHERE AuthorID = @p1",
                        imagePath,
                        author.AuthorID
                    );
                }

                TempData["Success"] = "Thêm tác giả thành công";

                return RedirectToAuthorsIndex();
            }

            return View(author);
        }

        // =========================
        // SỬA TÁC GIẢ - GET
        // =========================
        [AdminAuthorize]
        public ActionResult Edit(int id)
        {
            if (!IsAdminManagementRequest())
            {
                return RedirectToRoute("AdminAuthors",
                    new { action = "Edit", id = id });
            }

            ViewBag.IsAdminManagement = true;

            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            author.Images = GetAuthorImage(author.AuthorID);

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
            ViewBag.IsAdminManagement = IsAdminManagementRequest();
            if ((imageFile == null || imageFile.ContentLength == 0) && Request.Files != null && Request.Files.Count > 0)
            {
                imageFile = Request.Files[0];
            }

            var existingAuthor = db.Authors.Find(author.AuthorID);

            if (existingAuthor == null)
                return HttpNotFound();

            if (author.AuthorName != null)
                author.AuthorName = author.AuthorName.Trim();

            if (author.Email != null)
                author.Email = author.Email.Trim();

            if (db.Authors.Any(a =>
                a.Email == author.Email &&
                a.AuthorID != author.AuthorID))
            {
                ModelState.AddModelError("Email", "Email đã được sử dụng");
            }

            string imageError;

            if (!ValidateImage(imageFile, out imageError))
            {
                ModelState.AddModelError("Images", imageError);
            }

            if (ModelState.IsValid)
            {
                existingAuthor.AuthorName = author.AuthorName;
                existingAuthor.Email = author.Email;

                db.SaveChanges();

                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string oldImage = GetAuthorImage(existingAuthor.AuthorID);
                    string newImage = SaveAuthorImage(imageFile);

                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Authors SET Images = @p0 WHERE AuthorID = @p1",
                        newImage,
                        existingAuthor.AuthorID
                    );

                    TryDeleteAuthorImage(oldImage);
                }
                else if (removeImage)
                {
                    string oldImage = GetAuthorImage(existingAuthor.AuthorID);

                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Authors SET Images = NULL WHERE AuthorID = @p0",
                        existingAuthor.AuthorID
                    );

                    TryDeleteAuthorImage(oldImage);
                }

                TempData["Success"] = "Cập nhật thành công";

                return RedirectToAuthorsIndex();
            }

            author.Images = GetAuthorImage(author.AuthorID);

            return View(author);
        }

        // =========================
        // XÓA RIÊNG ẢNH TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteImage(int id)
        {
            var author = db.Authors.Find(id);
            if (author == null)
                return HttpNotFound();

            string oldImage = GetAuthorImage(id);

            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.Authors SET Images = NULL WHERE AuthorID = @p0",
                id
            );

            TryDeleteAuthorImage(oldImage);

            TempData["Success"] = "Đã xóa ảnh đại diện tác giả";

            return RedirectToRoute("AdminAuthors", new { action = "Edit", id = id });
        }

        // =========================
        // XÓA TÁC GIẢ - GET
        // =========================
        [AdminAuthorize]
        public ActionResult Delete(int id)
        {
            if (!IsAdminManagementRequest())
            {
                return RedirectToRoute("AdminAuthors",
                    new { action = "Delete", id = id });
            }

            ViewBag.IsAdminManagement = true;

            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            author.Images = GetAuthorImage(author.AuthorID);

            return View(author);
        }

        // =========================
        // XÓA TÁC GIẢ - POST
        // =========================
        [AdminAuthorize]
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ViewBag.IsAdminManagement = IsAdminManagementRequest();

            var author = db.Authors.Find(id);

            if (author == null)
                return HttpNotFound();

            if (db.Articles.Any(a => a.AuthorID == id))
            {
                TempData["Error"] =
                    "Không thể xóa: tác giả này vẫn còn bài viết";

                return RedirectToAuthorsIndex();
            }

            string imagePath = GetAuthorImage(id);

            db.Authors.Remove(author);
            db.SaveChanges();

            TryDeleteAuthorImage(imagePath);

            TempData["Success"] = "Đã xóa tác giả";

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

            if (id == null)
            {
                return new HttpStatusCodeResult(
                    System.Net.HttpStatusCode.BadRequest);
            }

            return IsAdminManagementRequest()
                ? RedirectToRoute("AdminAuthors",
                    new { action = "Index", id = id })
                : RedirectToRoute("PublicAuthor",
                    new { action = "Index", id = id });
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