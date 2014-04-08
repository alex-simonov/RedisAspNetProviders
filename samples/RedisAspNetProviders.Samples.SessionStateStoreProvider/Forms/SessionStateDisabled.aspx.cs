using System;
using System.Web.UI;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms
{
    public partial class SessionStateDisabled : Page
    {
        public static readonly string RouteName = "WebFormsSessionDisabled";

        protected void Page_Load(object sender, EventArgs e)
        {
            ((ModelControl)Master.FindControl("ModelView")).RedirectRouteName = RouteName;
        }
    }
}