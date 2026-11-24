using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace NewsPortal.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết")]
        [StringLength(250, ErrorMessage = "Tiêu đề bài viết không được vượt quá 250 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        [NotMapped]
        [Display(Name = "Tác giả")]
        public int? AuthorId { get; set; }

        [NotMapped]
        public string AuthorName { get; set; }

        [NotMapped]
        public string CategoryName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung bài viết")]
        [StringLength(20000, MinimumLength = 50, ErrorMessage = "Nội dung từ 50 đến 20000 ký tự")]
        [Display(Name = "Nội dung")]
        [DataType(DataType.MultilineText)]
        [AllowHtml]
        public string Content { get; set; }

        [Display(Name = "Hình ảnh")]
        public string Image { get; set; }

        public string ImageUrl { get; set; }

        public string Status { get; set; }

        public int ViewCount { get; set; }

        [Display(Name = "Ngày đăng")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [NotMapped]
        public string PlainSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content)) return "";

                var noTags = Regex.Replace(Content, "<.*?>", " ");
                var cleanText = Regex.Replace(WebUtility.HtmlDecode(noTags), @"\s+", " ").Trim();
                return cleanText.Length > 120 ? cleanText.Substring(0, 120) + "..." : cleanText;
            }
        }
    }
}
