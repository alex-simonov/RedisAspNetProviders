using System;
using System.Web.UI;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms
{
    public partial class SessionStateReadOnly : Page
    {
        public static string RouteName = "WebFormsSessionReadOnly";

        protected void Page_Load(object sender, EventArgs e)
        {
            ((ModelControl)Master.FindControl("ModelView")).RedirectRouteName = RouteName;
        }
    }
}