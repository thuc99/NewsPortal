using System.Web;
using System.Web.Mvc;
using System.Linq;
using NewsPortal.Models;

namespace NewsPortal
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new UserRoleRefreshFilter(), int.MinValue);
            filters.Add(new HandleErrorAttribute());
        }
    }

    public class UserRoleRefreshFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session == null)
                return;

            string username = System.Convert.ToString(session["User"] ?? session["Admin"]);
            if (string.IsNullOrWhiteSpace(username))
                return;

            string role;
            using (var db = new NewsPortalDBEntities())
            {
                role = db.Users
                    .Where(user => user.Username == username)
                    .Select(user => user.Role)
                    .FirstOrDefault();
            }

            role = NormalizeRole(role);
            if (role == null)
                return;

            session["Role"] = role;
            if (role == "Admin")
            {
                session["Admin"] = username;
                session.Remove("User");
            }
            else
            {
                session["User"] = username;
                session.Remove("Admin");
            }
        }

        private static string NormalizeRole(string role)
        {
            if (string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase))
                return "Admin";
            if (string.Equals(role, "Editor", System.StringComparison.OrdinalIgnoreCase))
                return "Editor";
            if (string.Equals(role, "Author", System.StringComparison.OrdinalIgnoreCase))
                return "Author";
            if (string.Equals(role, "Reader", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "User", System.StringComparison.OrdinalIgnoreCase))
                return "Reader";
            return null;
        }
    }
}
