using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class RefreshUserRoleAttribute : AuthorizeAttribute
    {
        public RefreshUserRoleAttribute()
        {
            Order = -1;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var session = httpContext.Session;
            if (session == null)
                return true;

            string adminUsername = Convert.ToString(session["Admin"]);
            string userUsername = Convert.ToString(session["User"]);
            bool isAdminSession = !string.IsNullOrWhiteSpace(adminUsername);
            string username = isAdminSession ? adminUsername : userUsername;

            if (string.IsNullOrWhiteSpace(username))
                return true;

            using (var db = new NewsPortalDBEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                {
                    if (isAdminSession && string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        session["Role"] = "Admin";
                        return true;
                    }

                    return false;
                }

                if (!user.IsActive)
                    return false;

                string role = (user.Role ?? string.Empty).Trim();
                if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
                    role = "Reader";

                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    role = "Admin";
                    session["Admin"] = user.Username;
                    session.Remove("User");
                }
                else if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Reader", StringComparison.OrdinalIgnoreCase))
                {
                    session.Remove("Admin");
                    session["User"] = user.Username;
                }
                else
                {
                    return false;
                }

                session["Role"] = role;
                session["UserFullName"] = user.FullName;
                return true;
            }
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session != null)
            {
                session.Remove("Admin");
                session.Remove("User");
                session.Remove("UserFullName");
                session.Remove("Role");
                session["LoginMessage"] = "Tài khoản không còn hoạt động hoặc không hợp lệ. Vui lòng đăng nhập lại.";
            }

            filterContext.Result = new RedirectResult("~/Account/Login");
        }
    }

    public class AdminAreaAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session == null)
                return false;

            string role = Convert.ToString(httpContext.Session["Role"]);

            bool isAdmin = httpContext.Session["Admin"] != null;

            bool isEditor = string.Equals(
                role,
                "Editor",
                StringComparison.OrdinalIgnoreCase
            );

            return isAdmin || isEditor;
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session != null && session["User"] != null)
            {
                session["AccessDeniedMessage"] =
                    "B\u1EA1n kh\u00F4ng c\u00F3 quy\u1EC1n truy c\u1EADp khu v\u1EF1c qu\u1EA3n tr\u1ECB.";

                filterContext.Result =
                    new RedirectResult("~/Home/Index");

                return;
            }

            if (session != null)
            {
                session["LoginMessage"] =
                    "Vui l\u00F2ng \u0111\u0103ng nh\u1EADp \u0111\u1EC3 truy c\u1EADp khu v\u1EF1c qu\u1EA3n tr\u1ECB.";
            }

            filterContext.Result =
                new RedirectResult("~/Account/Login");
        }
    }
}
