
using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

            // THANH TIN NÓNG - 10 BÀI VIẾT MỚI NHẤT
            ViewBag.BreakingNews = publishedArticles
                .OrderByDescending(a => a.CreatedDate)
                .Take(10)
                .ToList();

            // TỪ KHÓA PHỔ BIẾN - THỐNG KÊ CỤM TỪ 2-3 TỪ
            ViewBag.PopularKeywords = GetPopularKeywords(publishedArticles);

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

            // TOP 5 BÀI VIẾT ĐỌC NHIỀU TRONG 7 NGÀY
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

            // TÌM KIẾM VÀ LỌC BÀI VIẾT
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

            // PHÂN TRANG
            var totalArticles = query.Count();

            var totalPages = Math.Max(
                (int)Math.Ceiling((double)totalArticles / pageSize), 1);

            page = Math.Min(page, totalPages);

            var result = query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // TRUYỀN DỮ LIỆU SANG VIEW
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
            ViewBag.TopViewedArticles = topViewedArticles;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalArticles = totalArticles;

            return View(result);
        }

        // THỐNG KÊ CÁC CỤM TỪ KHÓA 2-3 TỪ
        private List<string> GetPopularKeywords(IQueryable<Articles> articles)
        {
            // Các từ không có nhiều ý nghĩa khi làm từ khóa
            var stopWords = new HashSet<string>(
                new[]
                {
                    "những", "được", "trong", "ngoài", "đang",
                    "các", "một", "này", "đó", "cho", "với",
                    "của", "theo", "từ", "tại", "vào", "khi",
                    "sau", "trước", "trên", "dưới", "đến",
                    "như", "là", "và", "có", "không", "đã",
                    "sẽ", "bị", "bởi", "nếu", "thì", "mà",
                    "về", "ra", "để", "nhiều", "đây", "việc",
                    "người", "bài", "viết", "tin", "the", "and",
                    "for", "with", "from", "this", "that",
                    "theo đó", "cho biết", "được biết",
                    "hiện nay", "mới đây", "thời gian",
                    "liên quan", "tiếp tục", "đồng thời"
                },
                StringComparer.OrdinalIgnoreCase
            );

            // Chỉ lấy tiêu đề và tóm tắt, không lấy toàn bộ nội dung
            var articleTexts = articles
                .Select(a => new
                {
                    a.Title,
                    a.Summary
                })
                .ToList();

            // Điểm của từng cụm từ khóa
            var keywordScores = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );

            // Số bài viết có chứa từng cụm từ
            var keywordArticleCounts = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var article in articleTexts)
            {
                var articleKeywords = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
                );

                // Từ khóa trong tiêu đề được ưu tiên cao hơn
                var titleKeywords = ExtractPhrases(
                    article.Title ?? "",
                    stopWords
                );

                foreach (var keyword in titleKeywords)
                {
                    articleKeywords[keyword] = 3;
                }

                // Từ khóa trong phần tóm tắt có điểm thấp hơn
                var summaryKeywords = ExtractPhrases(
                    article.Summary ?? "",
                    stopWords
                );

                foreach (var keyword in summaryKeywords)
                {
                    if (!articleKeywords.ContainsKey(keyword))
                    {
                        articleKeywords[keyword] = 1;
                    }
                }

                // Mỗi bài viết chỉ tính một lần cho mỗi cụm từ
                foreach (var item in articleKeywords)
                {
                    if (keywordScores.ContainsKey(item.Key))
                    {
                        keywordScores[item.Key] += item.Value;
                        keywordArticleCounts[item.Key]++;
                    }
                    else
                    {
                        keywordScores[item.Key] = item.Value;
                        keywordArticleCounts[item.Key] = 1;
                    }
                }
            }

            // Ưu tiên cụm từ xuất hiện trong nhiều bài viết
            return keywordArticleCounts
                .Where(x => x.Value >= 1)
                .OrderByDescending(x => x.Value)
                .ThenByDescending(x => keywordScores[x.Key])
                .Take(10)
                .Select(x => FormatKeyword(x.Key))
                .ToList();
        }

        // TÁCH CỤM TỪ GỒM 2-3 TỪ LIỀN NHAU
        private List<string> ExtractPhrases(
            string text,
            HashSet<string> stopWords)
        {
            var words = Regex.Matches(
                text.ToLowerInvariant(),
                @"[\p{L}\p{N}]{2,}"
            )
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

            var phrases = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

            // Tạo cụm từ gồm 2 từ và 3 từ
            for (int length = 2; length <= 3; length++)
            {
                for (int i = 0; i <= words.Count - length; i++)
                {
                    var phraseWords = words
                        .Skip(i)
                        .Take(length)
                        .ToList();

                    // Không cho cụm từ bắt đầu hoặc kết thúc bằng từ vô nghĩa
                    if (stopWords.Contains(phraseWords.First()) ||
                        stopWords.Contains(phraseWords.Last()))
                    {
                        continue;
                    }

                    // Loại cụm từ có quá nhiều từ nối
                    int stopWordCount = phraseWords
                        .Count(w => stopWords.Contains(w));

                    if (stopWordCount > 1)
                    {
                        continue;
                    }

                    // Ghép thành cụm từ hoàn chỉnh
                    var phrase = string.Join(" ", phraseWords);

                    // Không lấy cụm từ chỉ chứa số
                    if (!phrase.Any(char.IsLetter))
                    {
                        continue;
                    }

                    phrases.Add(phrase);
                }
            }

            return phrases.ToList();
        }

        // VIẾT HOA CHỮ CÁI ĐẦU CỦA CỤM TỪ
        private string FormatKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return keyword;
            }

            return char.ToUpperInvariant(keyword[0]) +
                keyword.Substring(1);
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
