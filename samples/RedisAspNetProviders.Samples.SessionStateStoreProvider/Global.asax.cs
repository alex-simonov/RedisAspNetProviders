using System;
using System.Web;
using System.Web.Routing;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider
{
    public class Global : HttpApplication
    {
        private void Application_Start(object sender, EventArgs e)
        {
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}