
using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class HomeController : Controller
    {
        private NewsPortalDBEntities db = new NewsPortalDBEntities();

        public ActionResult Index(string searchString, int? categoryId, int? authorId,
            DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            const int pageSize = 6;
            page = Math.Max(page, 1);

            // Lấy danh sách bài viết đã xuất bản
            var publishedArticles = db.Articles
                .Include(a => a.Categories)
                .Include(a => a.Authors)
                .Where(a => a.Status == "Published");

            // ==========================================
            // THANH TIN NÓNG - 10 BÀI VIẾT MỚI NHẤT
            // ==========================================
            ViewBag.BreakingNews = publishedArticles
                .OrderByDescending(a => a.CreatedDate)
                .Take(10)
                .ToList();

            // Lấy 3 bài viết mới nhất
            var latestArticles = publishedArticles
                .OrderByDescending(a => a.CreatedDate)
                .Take(3)
                .ToList();

            // Chọn bài viết nổi bật trong 7 ngày gần nhất
            var featuredSince = DateTime.Now.AddDays(-7);

            var featured = publishedArticles
                .Where(a => a.CreatedDate >= featuredSince)
                .OrderByDescending(a => a.ViewCount)
                .ThenByDescending(a => a.CreatedDate)
                .FirstOrDefault();

            if (featured == null)
            {
                featured = latestArticles.FirstOrDefault();
            }

            // ==========================================
            // TOP 5 BÀI VIẾT ĐỌC NHIỀU TRONG 7 NGÀY
            // ==========================================
            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            var weeklyViews = db.ArticleViewHistory
                .Where(h => h.ViewedDate >= sevenDaysAgo)
                .GroupBy(h => h.ArticleId)
                .Select(g => new
                {
                    ArticleId = g.Key,
                    WeeklyViews = g.Count()
                })
                .ToList();

            var topViewedArticles = weeklyViews
                .Join(
                    publishedArticles.ToList(),
                    view => view.ArticleId,
                    article => article.ArticleID,
                    (view, article) => Tuple.Create(
                        article,
                        view.WeeklyViews
                    ))
                .OrderByDescending(x => x.Item2)
                .Take(5)
                .ToList();

            // ==========================================
            // TÌM KIẾM VÀ LỌC BÀI VIẾT
            // ==========================================
            var query = publishedArticles;

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                query = query.Where(a =>
                    a.Title.Contains(searchString) ||
                    a.Summary.Contains(searchString) ||
                    a.Content.Contains(searchString));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.CategoryID == categoryId.Value);
            }

            if (authorId.HasValue)
            {
                query = query.Where(a => a.AuthorID == authorId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(a => a.CreatedDate < endDate);
            }

            // Không hiển thị bài viết nổi bật trong danh sách bài viết khác
            if (featured != null)
            {
                var featuredId = featured.ArticleID;
                query = query.Where(a => a.ArticleID != featuredId);
            }

            // ==========================================
            // PHÂN TRANG
            // ==========================================
            var totalArticles = query.Count();

            var totalPages = Math.Max(
                (int)Math.Ceiling((double)totalArticles / pageSize), 1);

            page = Math.Min(page, totalPages);

            var result = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ==========================================
            // TRUYỀN DỮ LIỆU SANG VIEW
            // ==========================================
            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.AuthorId = authorId;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            ViewBag.Categories = db.Categories
                .OrderBy(c => c.CategoryName)
                .ToList();

            ViewBag.Authors = db.Authors
                .OrderBy(a => a.AuthorName)
                .ToList();

            ViewBag.FeaturedArticle = featured;
            ViewBag.LatestArticles = latestArticles;

            // Dữ liệu Top 5 bài viết đọc nhiều
            ViewBag.TopViewedArticles = topViewedArticles;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalArticles = totalArticles;

            return View(result);
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
