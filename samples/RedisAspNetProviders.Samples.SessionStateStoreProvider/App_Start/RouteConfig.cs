using System.Web.Mvc;
using System.Web.Routing;
using RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("MVC", "mvc/{controller}/{action}"
                );

            routes.MapPageRoute(
                SessionStateRequired.RouteName,
                "webforms/sessionrequired",
                "~/Forms/SessionStateRequired.aspx"
                );

            routes.MapPageRoute(
                SessionStateReadOnly.RouteName,
                "webforms/sessionreadonly",
                "~/Forms/SessionStateReadOnly.aspx"
                );

            routes.MapPageRoute(
                SessionStateDisabled.RouteName,
                "webforms/sessiondisabled",
                "~/Forms/SessionStateDisabled.aspx"
                );

            routes.MapRoute(null, "", new { controller = "RequiredSession", action = "Index" });
        }
    }
}