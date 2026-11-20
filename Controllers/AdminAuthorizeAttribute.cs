using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal.Controllers
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
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
                    if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Session["Admin"] = user.Username;
                        httpContext.Session.Remove("User");
                        httpContext.Session.Remove("UserFullName");
                        return true;
                    }

                    httpContext.Session.Remove("Admin");
                    httpContext.Session["User"] = user.Username;
                    httpContext.Session["UserFullName"] = user.FullName;
                    return false;
                }
            }

            return httpContext.Session["Admin"] != null &&
                   string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            filterContext.Result =
                new RedirectResult("/Account/Login");
        }
    }
}