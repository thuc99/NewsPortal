using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NewsPortal.Models
{
    [MetadataType(typeof(AuthorMetadata))]
    public partial class Author { }

    public class AuthorMetadata
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tác giả")]
        [StringLength(300, ErrorMessage = "Tên tối đa 300 ký tự")]
        [Display(Name = "Tên tác giả")]
        public string AuthorName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(300, ErrorMessage = "Email tối đa 300 ký tự")]
        public string Email { get; set; }
    }
}