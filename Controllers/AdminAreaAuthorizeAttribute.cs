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
