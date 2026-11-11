using System;
using System.Linq;
using System.Web;
using System.Web.Security;
using NewsPortal.Models;

namespace NewsPortal.Helpers
{
    public static class AdminSessionHelper
    {
        public const string KeyUserId = "AdminUserId";
        public const string KeyUsername = "AdminUsername";
        public const string KeyFullName = "AdminFullName";
        public const string KeyRole = "AdminRole";

        public static void SetAdminSession(HttpSessionStateBase session, User user)
        {
            if (session == null || user == null) return;

            session[KeyUserId] = user.UserID;
            session[KeyUsername] = user.Username;
            session[KeyFullName] = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
            session[KeyRole] = user.Role;
        }

        public static void ClearAdminSession(HttpSessionStateBase session)
        {
            if (session != null)
            {
                session.Remove(KeyUserId);
                session.Remove(KeyUsername);
                session.Remove(KeyFullName);
                session.Remove(KeyRole);
                session.Clear();
                session.Abandon();
            }
            FormsAuthentication.SignOut();
        }

        public static bool IsAdminLoggedIn(HttpContextBase httpContext)
        {
            if (httpContext == null) return false;

            var session = httpContext.Session;
            if (session != null && session[KeyUserId] != null)
            {
                string role = session[KeyRole] as string;
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Khôi phục phiên từ FormsAuthentication cookie nếu người dùng chọn "Ghi nhớ đăng nhập"
            if (httpContext.User != null && httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated)
            {
                string username = httpContext.User.Identity.Name;
                if (!string.IsNullOrWhiteSpace(username) && session != null)
                {
                    try
                    {
                        using (var db = new NewsPortalDBEntities())
                        {
                            var user = db.Users.FirstOrDefault(u =>
                                u.Username == username &&
                                u.IsActive &&
                                u.Role == "Admin");

                            if (user != null)
                            {
                                SetAdminSession(session, user);
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Bỏ qua nếu CSDL chưa sẵn sàng
                    }
                }
            }

            return false;
        }

        public static int? GetCurrentAdminId(HttpSessionStateBase session)
        {
            if (session == null || session[KeyUserId] == null) return null;
            int id;
            if (int.TryParse(session[KeyUserId].ToString(), out id))
            {
                return id;
            }
            return null;
        }

        public static string GetCurrentAdminName(HttpSessionStateBase session)
        {
            if (session == null) return null;
            return (session[KeyFullName] as string) ?? (session[KeyUsername] as string);
        }
    }
}
