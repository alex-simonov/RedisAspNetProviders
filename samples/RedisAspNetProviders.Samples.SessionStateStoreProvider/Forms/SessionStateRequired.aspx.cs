using System;
using System.Web.UI;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms
{
    public partial class SessionStateRequired : Page
    {
        public static string RouteName = "WebFormsSessionRequired";

        protected void Page_Load(object sender, EventArgs e)
        {
            ((ModelControl)Master.FindControl("ModelView")).RedirectRouteName = RouteName;
        }
    }
}