using System;
using System.ComponentModel.DataAnnotations;

namespace NewsPortal.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết")]
        [StringLength(250, MinimumLength = 10, ErrorMessage = "Tiêu đề từ 10 đến 250 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung bài viết")]
        [StringLength(20000, MinimumLength = 50, ErrorMessage = "Nội dung từ 50 đến 20000 ký tự")]
        [Display(Name = "Nội dung")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }

        [Display(Name = "Hình ảnh")]
        public string Image { get; set; } // Thêm thuộc tính lưu đường dẫn ảnh

        public string ImageUrl { get; set; }

        public string Status { get; set; }

        public int ViewCount { get; set; }

        [Display(Name = "Ngày đăng")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}