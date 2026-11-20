using System;
using System.Collections.Generic;

namespace NewsPortal.Models
{
    public class AdminArticleRow
    {
        public int ArticleID { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public string AuthorName { get; set; }
        public string Status { get; set; }
        public int ViewCount { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AdminUserRow
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool CanToggleStatus { get; set; }
        public bool CanChangeRole { get; set; }
    }

    public class AdminCommentRow
    {
        public int CommentID { get; set; }
        public int ArticleID { get; set; }
        public string ArticleTitle { get; set; }
        public int? UserID { get; set; }
        public string Username { get; set; }
        public string CommenterName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalArticles { get; set; }
        public int PublishedArticles { get; set; }
        public int DraftArticles { get; set; }
        public int TotalViews { get; set; }
        public int TotalCategories { get; set; }
        public int TotalAuthors { get; set; }
        public int TotalUsers { get; set; }

        public string SearchQuery { get; set; }
        public string StatusFilter { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int FilteredArticleCount { get; set; }

        public List<AdminArticleRow> Articles { get; set; }

        public AdminDashboardViewModel()
        {
            Articles = new List<AdminArticleRow>();
        }
    }
}