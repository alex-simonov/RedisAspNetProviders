using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using StackExchange.Redis;

namespace RedisAspNetProviders
{
    public class OutputCacheProvider : System.Web.Caching.OutputCacheProvider
    {
        private static readonly object s_oneTimeInitLock = new object();
        private static bool s_oneTimeInitCalled;

        protected static ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected static int DbNumber { get; private set; }
        protected static string KeyPrefix { get; private set; }

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

        private void OneTimeInit(NameValueCollection config)
        {
            ConnectionMultiplexer = InitializationUtils.GetConnectionMultiplexer(config);
            try
            {
                DbNumber = InitializationUtils.ParseInt(
                    config["dbNumber"],
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    0);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("Can not parse db number.", ex);
            }
            KeyPrefix = config["keyPrefix"] ?? string.Empty;
        }

        protected virtual Tuple<IDatabase, RedisKey> GetCacheEntryStorageDetails(string entryKey)
        {
            return new Tuple<IDatabase, RedisKey>(
                ConnectionMultiplexer.GetDatabase(DbNumber),
                KeyPrefix + entryKey);
        }

        protected virtual byte[] SerializeObject(object entry)
        {
            var bw = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bw.Serialize(ms, entry);
                return ms.ToArray();
            }
        }

        protected virtual object DeserializeObject(byte[] bytes)
        {
            var bw = new BinaryFormatter();
            using (var ms = new MemoryStream(bytes))
            {
                return bw.Deserialize(ms);
            }
        }

        public override object Add(string entryKey, object entry, DateTime utcExpiry)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetCacheEntryStorageDetails(entryKey);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            RedisValue cachedBytes = redis.StringGet(key);
            if (!cachedBytes.IsNull) return DeserializeObject(cachedBytes);

            return AddOrGetExisting(entry, utcExpiry, redis, key);
        }

        protected internal virtual object AddOrGetExisting(object entry, DateTime utcExpiry, IDatabase redis, RedisKey key)
        {
            byte[] newBytes = SerializeObject(entry);
            RedisResult cachedBytes = redis.ScriptEvaluate(
                @"local cached = redis.call('GET', KEYS[1])
                  if not cached then
                      redis.call('SETEX', KEYS[1], ARGV[1], ARGV[2])
                  end
                  return cached",
                new RedisKey[] { key },
                new RedisValue[]
                {
                    (long)utcExpiry.Subtract(DateTime.UtcNow).TotalSeconds,
                    newBytes
                });

            return cachedBytes.IsNull ? entry : DeserializeObject((byte[])cachedBytes);
        }

        public override object Get(string entryKey)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetCacheEntryStorageDetails(entryKey);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            RedisValue cachedBytes = redis.StringGet(key);
            return cachedBytes.IsNull ? null : DeserializeObject(cachedBytes);
        }

        public override void Remove(string entryKey)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetCacheEntryStorageDetails(entryKey);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            redis.KeyDelete(key);
        }

        public override void Set(string entryKey, object entry, DateTime utcExpiry)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetCacheEntryStorageDetails(entryKey);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            byte[] newBytes = SerializeObject(entry);
            redis.StringSet(key, newBytes, utcExpiry.Subtract(DateTime.UtcNow));
        }
    }
}