using System;
using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AdminAreaAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session == null)
                return false;

            string role = Convert.ToString(httpContext.Session["Role"]);

            bool isAdmin = httpContext.Session["Admin"] != null ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

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
                string role = Convert.ToString(session["Role"]);
                if (string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                {
                    filterContext.Result = new RedirectResult("~/Admin/Article");
                    return;
                }

                session["AccessDeniedMessage"] =
                    "Bạn không có quyền truy cập khu vực quản trị.";

                filterContext.Result =
                    new RedirectResult("~/Home/Index");

                return;
            }

            if (session != null)
            {
                session["LoginMessage"] =
                    "Vui lòng đăng nhập để truy cập khu vực quản trị.";
            }

            filterContext.Result =
                new RedirectResult("~/Account/Login");
        }
    }
}
