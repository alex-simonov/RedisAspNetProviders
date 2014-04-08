using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Web.Configuration;
using StackExchange.Redis;

namespace RedisAspNetProviders
{
    internal static class InitializationUtils
    {
        public static int ParseInt(string rawValue, NumberStyles styles, int defaultValue)
        {
            int parsedValue = string.IsNullOrWhiteSpace(rawValue)
                ? defaultValue
                : int.Parse(rawValue, styles, CultureInfo.InvariantCulture);

            return parsedValue;
        }

        public static ConnectionMultiplexer GetConnectionMultiplexer(NameValueCollection config)
        {
            string connectionString;
            if (!string.IsNullOrEmpty(config["connectionStringName"]))
            {
                ConnectionStringSettings connectionStringSettings =
                    WebConfigurationManager.ConnectionStrings[config["connectionStringName"]];
                if (connectionStringSettings == null)
                {
                    throw new ConfigurationErrorsException(string.Format(
                        "Can not find connectionString with name=\"{0}\"",
                        config["connectionStringName"]));
                }
                connectionString = connectionStringSettings.ConnectionString;
            }
            else if (!string.IsNullOrEmpty(config["connectionString"]))
            {
                connectionString = config["connectionString"];
            }
            else
            {
                throw new ConfigurationErrorsException("connectionString is null");
            }
            using (var sw = new StringWriter())
            {
                return ConnectionMultiplexer.Connect(connectionString, sw);
            }
        }
    }
}