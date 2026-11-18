
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NewsPortal
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Trang tác giả công khai
            routes.MapRoute(
                name: "PublicAuthor",
                url: "Author/{action}/{id}",
                defaults: new
                {
                    controller = "Authors",
                    action = "Index",
                    id = UrlParameter.Optional
                }
            );

            // Trang chủ
            routes.MapRoute(
                name: "HomeDetailsWithoutId",
                url: "Home/Details",
                defaults: new
                {
                    controller = "Home",
                    action = "Index"
                }
            );

            // Quản lý bài viết
            routes.MapRoute(
                name: "AdminArticles",
                url: "Admin/Article/{action}/{id}",
                defaults: new
                {
                    controller = "Articles",
                    action = "Index",
                    id = UrlParameter.Optional
                },
                namespaces: new[] { "NewsPortal.Controllers" }
            );

            // Quản lý tác giả
            routes.MapRoute(
                name: "AdminAuthors",
                url: "Admin/Author/{action}/{id}",
                defaults: new
                {
                    controller = "Authors",
                    action = "Index",
                    id = UrlParameter.Optional
                },
                namespaces: new[] { "NewsPortal.Controllers" }
            );

            // Quản lý danh mục
            routes.MapRoute(
                name: "AdminCategories",
                url: "Admin/Category/{action}/{id}",
                defaults: new
                {
                    controller = "Categories",
                    action = "Index",
                    id = UrlParameter.Optional
                },
                namespaces: new[] { "NewsPortal.Controllers" }
            );

            // Các trang Admin khác
            routes.MapRoute(
                name: "Admin",
                url: "Admin/{action}/{id}",
                defaults: new
                {
                    controller = "Admin",
                    action = "Index",
                    id = UrlParameter.Optional
                },
                namespaces: new[] { "NewsPortal.Controllers" }
            );

            // Route mặc định
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new
                {
                    controller = "Home",
                    action = "Index",
                    id = UrlParameter.Optional
                },
                constraints: new
                {
                    controller = @"^(?!Admin$).*$"
                }
            );
        }
    }
}