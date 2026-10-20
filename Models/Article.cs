using System;
using System.ComponentModel.DataAnnotations;

namespace NewsPortal.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Display(Name = "Nội dung")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }

        [Display(Name = "Hình ảnh")]
        public string Image { get; set; } // Thêm thuộc tính lưu đường dẫn ảnh

        [Display(Name = "Ngày đăng")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}