using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NewsPortal.Helpers;

namespace NewsPortal.Filters
{
    /// <summary>
    /// Bộ lọc kiểm tra xác thực và phân quyền Quản trị viên (Admin).
    /// Chặn người dùng chưa đăng nhập hoặc không phải Admin truy cập các trang CRUD quản trị.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return AdminSessionHelper.IsAdminLoggedIn(httpContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.Controller != null && filterContext.Controller.TempData != null)
            {
                filterContext.Controller.TempData["Error"] = "Vui lòng đăng nhập bằng tài khoản Quản trị viên (Admin) để thực hiện chức năng này.";
            }

            string returnUrl = filterContext.HttpContext.Request != null
                ? filterContext.HttpContext.Request.RawUrl
                : null;

            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary(new
                {
                    controller = "Admin",
                    action = "Login",
                    returnUrl = returnUrl
                })
            );
        }
    }
}
