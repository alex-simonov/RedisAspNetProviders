using System;
using System.Web;
using System.Web.UI;
using RedisAspNetProviders.Samples.SessionStateStoreProvider.Models;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms
{
    public partial class ModelControl : UserControl
    {
        protected HttpSessionStateBase SessionStateBase
        {
            get { return Context.Session == null ? null : new HttpSessionStateWrapper(Context.Session); }
        }

        internal string RedirectRouteName { get; set; }

        public Model GetModel()
        {
            return Model.GetModel(SessionStateBase);
        }

        protected void UpdateSession(object sender, EventArgs e)
        {
            Model.UpdateModel(SessionStateBase);
            Response.RedirectToRoute(RedirectRouteName);
        }

        protected void ClearSession(object sender, EventArgs e)
        {
            SessionStateBase.Clear();
            Response.RedirectToRoute(RedirectRouteName);
        }

        protected void AbandonSession(object sender, EventArgs e)
        {
            SessionStateBase.Abandon();
            Response.RedirectToRoute(RedirectRouteName);
        }
    }
}