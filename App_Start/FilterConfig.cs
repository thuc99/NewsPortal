using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NewsPortal.Models;

namespace NewsPortal
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new ActiveUserRoleRefreshAttribute());
        }

        private sealed class ActiveUserRoleRefreshAttribute : ActionFilterAttribute
        {
            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                var session = filterContext.HttpContext.Session;
                if (session == null)
                    return;

                string username = Convert.ToString(session["User"] ?? session["Admin"]);
                if (string.IsNullOrWhiteSpace(username))
                    return;

                using (var db = new NewsPortalDBEntities())
                {
                    var user = db.Users.FirstOrDefault(u => u.Username == username);
                    if (user == null)
                    {
                        ClearIdentity(session);
                        return;
                    }

                    if (!user.IsActive)
                    {
                        ClearIdentity(session);
                        return;
                    }

                    session["Role"] = user.Role;
                    if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        session["Admin"] = user.Username;
                        session.Remove("User");
                        session.Remove("UserFullName");
                    }
                    else
                    {
                        session.Remove("Admin");
                        session["User"] = user.Username;
                        session["UserFullName"] = user.FullName;
                    }
                }
            }

            private static void ClearIdentity(HttpSessionStateBase session)
            {
                session.Remove("Admin");
                session.Remove("User");
                session.Remove("UserFullName");
                session.Remove("Role");
            }
        }
    }
}
