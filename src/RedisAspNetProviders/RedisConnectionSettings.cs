using System;
using System.Collections.Specialized;
using System.Globalization;

namespace RedisAspNetProviders
{
    sealed class RedisConnectionSettings
    {
        public string Host { get; private set; }

        public int Port { get; private set; }

        public string Password { get; private set; }

        public int SyncTimeout { get; private set; }

        public int IOTimeout { get; private set; }

        /// <summary>
        /// Creates a RedisAspNetProviders.RedisConnectionSettings from a System.Collections.Specialized.NameValueCollection.
        /// Required keys are:
        ///  - "host" - host address for Redis.
        /// Optional keys are:
        ///  - "port" - port for Redis, default is 6379,
        ///  - "password" - password for Redis, default is null,
        ///  - "db" - database number for operations, default is 0,
        ///  - "syncTimeout" - syncTimout in miliseconds, defauylt is 5000,
        ///  - "ioTimeout" - syncTimout in miliseconds, defauylt is 5000.
        /// </summary>
        /// <param name="config">A System.Collections.Specialized.NameValueCollection which contains redis connection settings.</param>
        /// <returns>Parsed RedisAspNetProviders.RedisConnectionSettings.</returns>
        public static RedisConnectionSettings Parse(NameValueCollection config)
        {
            var connectionSettings = new RedisConnectionSettings();

            connectionSettings.Host = config["host"];
            if (string.IsNullOrEmpty(connectionSettings.Host))
            {
                throw new ArgumentException("host is null or an empty string.");
            }

            try
            {
                connectionSettings.Port = Utils.ParseInt(config["port"], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, 6379);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Can not parse port.", ex);
            }

            connectionSettings.Password = config["password"];
            if (connectionSettings.Password == string.Empty)
            {
                connectionSettings.Password = null;
            }

            try
            {
                connectionSettings.SyncTimeout = Utils.ParseInt(config["syncTimeout"], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, 10000);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Can not parse syncTimeout.", ex);
            }

            try
            {
                connectionSettings.IOTimeout = Utils.ParseInt(config["ioTimeout"], NumberStyles.Integer, -1);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Can not parse ioTimeout.", ex);
            }

            return connectionSettings;
        }

    }

}