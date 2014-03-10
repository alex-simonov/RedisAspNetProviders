using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using BookSleeve;

namespace RedisAspNetProviders
{
    public class OutputCacheProvider : System.Web.Caching.OutputCacheProvider
    {
        static readonly object s_oneTimeInitLock = new object();
        static bool s_oneTimeInitCalled;

        static RedisConnectionGateway s_redisConnectionGateway;
        static int s_db;
        static string s_keyPrefix;

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
                throw new ConfigurationErrorsException("OutputCacheProvider configuration error: " + aex.Message, aex);
            }
            try
            {
                s_db = Utils.ParseInt(config["dbNumber"], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, 0);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("OutputCacheProvider configuration error: Can not parse db number.", ex);
            }
        }

        /// <summary>
        /// Get Redis key for entry cached by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected string GetRedisKey(string key)
        {
            return s_keyPrefix + key;
        }

        /// <summary>
        /// Serialize object graph
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        protected virtual byte[] SerializeObject(object entry)
        {
            var bw = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bw.Serialize(ms, entry);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialize object graph from bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>Deserialized object graph</returns>
        protected virtual object DeserializeObject(byte[] bytes)
        {
            var bw = new BinaryFormatter();
            using (var ms = new MemoryStream(bytes))
            {
                return bw.Deserialize(ms);
            }
        }

        /// <summary>
        /// Add the specified entry into the output cache without overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="entryKey">A unique identifier for entry.</param>
        /// <param name="entry">Cache entry.</param>
        /// <param name="utcExpiry">The time and date on which the cached entry expires.</param>
        /// <returns>Inserted cache entry or existing entry.</returns>
        public override object Add(string entryKey, object entry, DateTime utcExpiry)
        {
            string key = GetRedisKey(entryKey);
            RedisConnection redis = s_redisConnectionGateway.GetConnection();

            // Check if any entry already cached by key
            byte[] cachedBytes = redis.Wait(redis.Strings.Get(s_db, key));

            if (cachedBytes == null)
            {
                byte[] newBytes = SerializeObject(entry);

                if (!redis.Features.Scripting)
                {
                    while (cachedBytes == null)
                    {
                        using (var setCacheItemTransaction = redis.CreateTransaction())
                        {
                            setCacheItemTransaction.AddCondition(Condition.KeyNotExists(s_db, key));
                            setCacheItemTransaction.Strings.Set(s_db, key, newBytes, (long)utcExpiry.Subtract(DateTime.UtcNow).TotalSeconds);
                            redis.Wait(setCacheItemTransaction.Execute());
                        }
                        cachedBytes = redis.Wait(redis.Strings.Get(s_db, key));
                    }
                }
                else
                {
                    string scriptBody;
                    if (redis.Features.SetConditional)
                    {
                        scriptBody = @"redis.call('SET', KEYS[1], ARGV[1], 'NX', 'EX', ARGV[2])
                                       return redis.call('GET', KEYS[1])";
                    }
                    else
                    {
                        scriptBody = @"local cached = redis.call('GET', KEYS[1])
                                       if cached == nil then
                                           redis.call('SETEX', KEYS[1], ARGV[1], ARGV[2])
                                           cached = ARGV[1]
                                       end
                                       return cached";
                    }

                    cachedBytes = (byte[])redis.Wait(redis.Scripting.Eval(
                        s_db,
                        scriptBody,
                        new string[] { key },
                        new object[] { newBytes, (long)utcExpiry.Subtract(DateTime.UtcNow).TotalSeconds }));
                }
            }

            return DeserializeObject(cachedBytes);
        }

        /// <summary>
        /// Returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="entryKey">A unique identifier for a cached entry in the output cache.</param>
        /// <returns>The key value that identifies the specified entry in the cache, or null if the specified entry is not in the cache.</returns>
        public override object Get(string entryKey)
        {
            string key = GetRedisKey(entryKey);
            RedisConnection redis = s_redisConnectionGateway.GetConnection();
            byte[] cachedBytes = redis.Wait(redis.Strings.Get(s_db, key));
            return DeserializeObject(cachedBytes);
        }

        /// <summary>
        /// Removes the specified entry from the output cache.
        /// </summary>
        /// <param name="entryKey">The unique identifier for the entry to remove from the output cache.</param>
        public override void Remove(string entryKey)
        {
            string key = GetRedisKey(entryKey);
            RedisConnection redis = s_redisConnectionGateway.GetConnection();
            redis.Wait(redis.Keys.Remove(s_db, key));
        }

        /// <summary>
        /// Inserts the specified entry into the output cache, overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="entryKey">A unique identifier for entry.</param>
        /// <param name="entry">The content to add to the output cache.</param>
        /// <param name="utcExpiry">The time and date on which the cached entry expires.</param>
        public override void Set(string entryKey, object entry, DateTime utcExpiry)
        {
            byte[] newBytes = SerializeObject(entry);
            string key = GetRedisKey(entryKey);
            RedisConnection redis = s_redisConnectionGateway.GetConnection();
            redis.Wait(redis.Strings.Set(s_db, key, newBytes, (long)utcExpiry.Subtract(DateTime.UtcNow).TotalSeconds));
        }

    }

}
