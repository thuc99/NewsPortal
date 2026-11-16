
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
                    "B?n không có quy?n truy c?p khu v?c qu?n tr?.";

                filterContext.Result =
                    new RedirectResult("~/Home/Index");

                return;
            }

            if (session != null)
            {
                session["LoginMessage"] =
                    "Vui lòng ??ng nh?p ?? truy c?p khu v?c qu?n tr?.";
            }

            filterContext.Result =
                new RedirectResult("~/Account/Login");
        }
    }
}
