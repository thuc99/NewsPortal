using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NewsPortal.Models
{
    public class ProfileInfoViewModel
    {
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên hiển thị từ 2 đến 100 ký tự")]
        [Display(Name = "Tên hiển thị")]
        public string FullName { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Giới tính")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự")]
        public string Email { get; set; }

        [RegularExpression(@"^(0|\+84)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ 0912345678)")]
        [Display(Name = "Điện thoại")]
        public string Phone { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        public string AvatarUrl { get; set; }
    }

    public class ProfileSidebarViewModel
    {
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public string ActiveTab { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 đến 64 ký tự.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
        public string ConfirmPassword { get; set; }
    }

    public class UserCommentItemViewModel
    {
        public int CommentId { get; set; }
        public int ArticleId { get; set; }
        public string ArticleTitle { get; set; }
        public string Content { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class SavedArticleItemViewModel
    {
        public int SavedId { get; set; }
        public int ArticleId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string ImageUrl { get; set; }
        public DateTime? SavedAt { get; set; }
    }

    public class ViewedArticleItemViewModel
    {
        public int HistoryId { get; set; }
        public int ArticleId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string ImageUrl { get; set; }
        public DateTime? ViewedAt { get; set; }
    }


}