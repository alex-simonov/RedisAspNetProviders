using System.Web.Mvc;
using System.Web.SessionState;
using RedisAspNetProviders.Samples.SessionStateStoreProvider.Models;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Controllers
{
    public abstract class SessionControllerBase : Controller
    {
        public virtual ActionResult Index()
        {
            Model model = Model.GetModel(Session);
            return View(model);
        }

        public RedirectToRouteResult SetDateTimeNowToSession()
        {
            Model.UpdateModel(Session);
            return RedirectToAction("Index");
        }

        public RedirectToRouteResult ClearSession()
        {
            Session.Clear();
            return RedirectToAction("Index");
        }

        public RedirectToRouteResult AbandonSession()
        {
            Session.Abandon();
            return RedirectToAction("Index");
        }
    }

    [SessionState(SessionStateBehavior.Required)]
    public class RequiredSessionController : SessionControllerBase
    {
        [OutputCache(VaryByParam = "*", Duration = 0, NoStore = true)]
        public override ActionResult Index()
        {
            ViewBag.Title = "MVC - SessionStateBehavior.Required";
            return base.Index();
        }
    }

    [SessionState(SessionStateBehavior.ReadOnly)]
    public class ReadOnlySessionController : SessionControllerBase
    {
        [OutputCache(VaryByParam = "*", Duration = 0, NoStore = true)]
        public override ActionResult Index()
        {
            ViewBag.Title = "MVC - SessionStateBehavior.ReadOnly";
            return base.Index();
        }
    }

    [SessionState(SessionStateBehavior.Disabled)]
    public class DisabledSessionController : SessionControllerBase
    {
        [OutputCache(VaryByParam = "*", Duration = 0, NoStore = true)]
        public override ActionResult Index()
        {
            ViewBag.Title = "MVC - SessionStateBehavior.Disabled";
            return base.Index();
        }
    }
}