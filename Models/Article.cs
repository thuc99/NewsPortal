using System;
using System.ComponentModel.DataAnnotations;

namespace NewsPortal.Models
{
    [MetadataType(typeof(ArticleMetadata))]
    public partial class Articles
    {
    }

    public class ArticleMetadata
    {
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Display(Name = "Danh mục")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tác giả")]
        [Display(Name = "Tác giả")]
        public int AuthorID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết")]
        [StringLength(250, ErrorMessage = "Tiêu đề tối đa 250 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [StringLength(500, ErrorMessage = "Tóm tắt tối đa 500 ký tự")]
        [Display(Name = "Tóm tắt")]
        [DataType(DataType.MultilineText)]
        public string Summary { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung bài viết")]
        [Display(Name = "Nội dung")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Ngày cập nhật")]
        public DateTime? UpdatedDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
        [RegularExpression("^(Draft|Published)$", ErrorMessage = "Trạng thái chỉ được là Draft (Bản nháp) hoặc Published (Đã xuất bản)")]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; }
    }
}
