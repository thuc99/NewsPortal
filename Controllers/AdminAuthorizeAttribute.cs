using System;
using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return httpContext.Session != null && httpContext.Session["Admin"] != null;
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session != null && session["User"] != null)
            {
                string role = Convert.ToString(session["Role"]);
                if (string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Author", StringComparison.OrdinalIgnoreCase))
                {
                    filterContext.Result = new RedirectResult("~/Admin/Article");
                    return;
                }

                filterContext.Result = new RedirectResult("~/Home/Index");
                return;
            }

            filterContext.Result =
                new RedirectResult("~/Account/Login");
        }
    }
}
