using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NewsPortal.Models
{
    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
        [StringLength(150, ErrorMessage = "Thông tin đăng nhập tối đa 150 ký tự")]
        [Display(Name = "Tên đăng nhập hoặc Email")]
        public string UsernameOrEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 đến 100 ký tự")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp với mật khẩu mới")]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmPassword { get; set; }
    }

    public class CategoryArticleCountItem
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int TotalCount { get; set; }
        public int PublishedCount { get; set; }
        public int DraftCount { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public string AdminFullName { get; set; }
        public string AdminUsername { get; set; }
        public int TotalArticles { get; set; }
        public int PublishedArticles { get; set; }
        public int DraftArticles { get; set; }
        public int TotalCategories { get; set; }
        public int TotalAuthors { get; set; }
        public int TotalAdmins { get; set; }
        public List<CategoryArticleCountItem> CategoryStats { get; set; }
        public List<Articles> RecentArticles { get; set; }
    }
}
