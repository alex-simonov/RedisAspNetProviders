using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using BookSleeve;

namespace RedisAspNetProviders
{
    public class SessionStateStoreProvider : SessionStateStoreProviderBase
    {
        static readonly object s_oneTimeInitLock = new object();
        static bool s_oneTimeInitCalled;

        static RedisConnectionGateway s_redisConnectionGateway;
        static int s_db;
        static string s_keyPrefix;
        static int s_sessionTimeoutInSeconds;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = GetType().FullName;
            }

            base.Initialize(name, config);

            if (!s_oneTimeInitCalled)
            {
                lock (s_oneTimeInitLock)
                {
                    if (!s_oneTimeInitCalled)
                    {
                        OneTimeInit(config);
                        s_oneTimeInitCalled = true;
                    }
                }
            }
        }

        protected virtual void OneTimeInit(NameValueCollection config)
        {
            s_keyPrefix = config["keyPrefix"] ?? string.Empty;
            try
            {
                var redisConnectionSettings = RedisConnectionSettings.Parse(config);
                s_redisConnectionGateway = new RedisConnectionGateway(redisConnectionSettings);
            }
            catch (ArgumentException aex)
            {
                throw new ConfigurationErrorsException("SessionStateStoreProvider configuration error: " + aex.Message, aex);
            }
            try
            {
                s_db = Utils.ParseInt(config["dbNumber"], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, 0);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("SessionStateStoreProvider configuration error: Can not parse db number.", ex);
            }

            var sessionStateConfigSection = (SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState");
            s_sessionTimeoutInSeconds = (int)sessionStateConfigSection.Timeout.TotalSeconds;
        }


        /// <summary>
        /// Get Redis key for session state data stored by key
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        protected virtual string GetRedisKey(HttpContext context, string id)
        {
            return s_keyPrefix + id;
        }

        /// <summary>
        /// Serialize a content of SessionStateItemCollection to array of bytes
        /// </summary>
        /// <param name="sessionStateItems"></param>
        /// <returns></returns>
        protected virtual byte[] SerializeSessionStateItemCollection(SessionStateItemCollection sessionStateItems)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                sessionStateItems.Serialize(bw);
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialize a content of SessionStateItemCollection from array of bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        protected virtual SessionStateItemCollection DeserializeSessionStateItemCollection(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                var result = SessionStateItemCollection.Deserialize(br);
                return result;
            }
        }

        /// <summary>
        /// Create a brand new SessionStateStoreData. The created SessionStateStoreData must have
        /// a non-null ISessionStateItemCollection.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(
                new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context),
                timeout);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="timeout"></param>
        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            string key = GetRedisKey(context, id);
            RedisConnection redis = s_redisConnectionGateway.GetConnection();
            redis.Keys.Remove(s_db, key);
            redis.Hashes.Set(s_db, key, new Dictionary<string, byte[]>(2)
            {
                { "init", new byte[1] { 0x00 } },
                { "data", SerializeSessionStateItemCollection(new SessionStateItemCollection()) }
            });
            redis.Keys.Expire(s_db, key, (int)TimeSpan.FromMinutes(timeout).TotalSeconds);
        }

        public override void Dispose()
        { }

        public override void EndRequest(HttpContext context)
        { }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked,
            out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetItem(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked,
            out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        protected virtual SessionStateStoreData GetItem(bool exclusive, HttpContext context, string id, out bool locked,
            out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            throw new NotImplementedException();
        }

        public override void InitializeRequest(HttpContext context)
        { }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            throw new NotImplementedException();
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            throw new NotImplementedException();
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            string key = GetRedisKey(context, id);
            var redis = s_redisConnectionGateway.GetConnection();
            redis.Wait(redis.Keys.Expire(s_db, key, s_sessionTimeoutInSeconds));
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            throw new NotImplementedException();
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

    }
}
