using System;
using System.Web;

namespace RedisAspNetProviders.Samples.SessionStateStoreProvider.Models
{
    [Serializable]
    public class Model
    {
        public DateTime DateTime { get; set; }
        public bool FromSession { get; set; }

        internal static Model GetModel(HttpSessionStateBase session)
        {
            Model model = null;
            if (session != null)
            {
                if (session["DateTime"] == null)
                {
                    model = new Model
                    {
                        DateTime = DateTime.Now,
                        FromSession = false
                    };
                }
                else
                {
                    model = new Model
                    {
                        DateTime = (DateTime)session["DateTime"],
                        FromSession = true
                    };
                }
            }
            return model;
        }

        internal static void UpdateModel(HttpSessionStateBase session)
        {
            session["DateTime"] = DateTime.Now;
        }
    }
}