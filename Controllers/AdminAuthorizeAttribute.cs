using System.Web;
using System.Web.Mvc;

namespace NewsPortal.Controllers
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return httpContext.Session["Admin"] != null;
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            filterContext.Result =
                new RedirectResult("/Account/Login");
        }
    }
}