using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class AdminAreaAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session == null)
                return false;

            string username = Convert.ToString(httpContext.Session["User"] ?? httpContext.Session["Admin"]);
            if (string.IsNullOrWhiteSpace(username))
                return false;

            using (var db = new NewsPortalDBEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
                if (user != null)
                {
                    httpContext.Session["Role"] = user.Role;
                    bool isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
                    bool isEditor = string.Equals(user.Role, "Editor", StringComparison.OrdinalIgnoreCase);

                    if (isAdmin)
                    {
                        httpContext.Session["Admin"] = user.Username;
                        httpContext.Session.Remove("User");
                        httpContext.Session.Remove("UserFullName");
                    }
                    else
                    {
                        httpContext.Session.Remove("Admin");
                        httpContext.Session["User"] = user.Username;
                        httpContext.Session["UserFullName"] = user.FullName;
                    }

                    return isAdmin || isEditor;
                }
            }

            httpContext.Session.Remove("Admin");
            httpContext.Session.Remove("User");
            httpContext.Session.Remove("UserFullName");
            httpContext.Session.Remove("Role");
            return false;
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
