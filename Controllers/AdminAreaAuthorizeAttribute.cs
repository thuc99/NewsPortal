using System;
using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AdminAreaAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return httpContext.Session["Admin"] != null ||
                string.Equals(Convert.ToString(httpContext.Session["Role"]), "Editor", StringComparison.OrdinalIgnoreCase);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Session["User"] != null)
            {
                filterContext.Result = new RedirectResult("/Articles/Index");
                return;
            }

            filterContext.Result = new RedirectResult("/Account/Login");
        }
    }
}
