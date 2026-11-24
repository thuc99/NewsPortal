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
        [StringLength(150, ErrorMessage = "Tên tác giả tối đa 150 ký tự")]
        [Display(Name = "Tên tác giả")]
        public string AuthorName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự")]
        [Display(Name = "Địa chỉ email")]
        public string Email { get; set; }
    }
}
