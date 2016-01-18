using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using StackExchange.Redis;

namespace RedisAspNetProviders
{
    public class SessionStateStoreProvider : SessionStateStoreProviderBase
    {
        private const string LockStartDateTimeFormat = "dd'-'MM'-'yyyy'T'HH':'mm':'ss'.'fffffff";
        private static readonly object s_oneTimeInitLock = new object();
        private static bool s_oneTimeInitCalled;

        protected static ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected static int DbNumber { get; private set; }
        protected static string KeyPrefix { get; private set; }
        protected static TimeSpan SessionTimeout { get; private set; }

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

        private static void OneTimeInit(NameValueCollection config)
        {
            ConnectionMultiplexer = InitializationUtils.GetConnectionMultiplexer(config);
            try
            {
                DbNumber = InitializationUtils.ParseInt(config["dbNumber"],
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, 0);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("Can not parse db number.", ex);
            }
            KeyPrefix = config["keyPrefix"] ?? string.Empty;

            var sessionStateConfig = (SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState");
            SessionTimeout = sessionStateConfig.Timeout;
        }

        protected virtual Tuple<IDatabase, RedisKey> GetSessionStateStorageDetails(HttpContext context, string id)
        {
            return new Tuple<IDatabase, RedisKey>(
                ConnectionMultiplexer.GetDatabase(DbNumber),
                KeyPrefix + id);
        }

        protected static string GenerateNewLockId()
        {
            return string.Format(
                "{0}|{1}",
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                DateTime.UtcNow.ToString(LockStartDateTimeFormat, CultureInfo.InvariantCulture));
        }

        protected static TimeSpan GetLockAge(string lockIdString)
        {
            int lockDateTimeStartIndex = lockIdString.IndexOf('|');
            DateTime lockDateTime = DateTime.ParseExact(
                lockIdString.Substring(lockDateTimeStartIndex + 1),
                LockStartDateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return DateTime.UtcNow.Subtract(lockDateTime);
        }

#if DOTNET45
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        protected static HttpStaticObjectsCollection GetSessionStaticObjects(HttpContext context)
        {
            return context == null ? new HttpStaticObjectsCollection() : SessionStateUtility.GetSessionStaticObjects(context);
        }

        protected virtual byte[] SerializeSessionState(SessionStateItemCollection sessionStateItems)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                sessionStateItems.Serialize(bw);
                return ms.ToArray();
            }
        }

        protected virtual SessionStateItemCollection DeserializeSessionState(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                SessionStateItemCollection result = SessionStateItemCollection.Deserialize(br);
                return result;
            }
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(
                new SessionStateItemCollection(),
                GetSessionStaticObjects(context),
                timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            byte[] sessionStateBytes = SerializeSessionState(new SessionStateItemCollection());

            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

			// HMSET <key>, data, <session data>, init, 1, timeout, <timeout>
			// EXPIRE <key>, <timeout>
            redis.ScriptEvaluate(
                @"redis.call('DEL', KEYS[1])
                  redis.call('HMSET', KEYS[1], ARGV[2], ARGV[3], ARGV[4], ARGV[5], ARGV[6], ARGV[1])
                  redis.call('EXPIRE', KEYS[1], ARGV[1])",
                new RedisKey[] { key },
                new RedisValue[]
                {
                    (long)TimeSpan.FromMinutes(timeout).TotalSeconds,
                    HashFieldsEnum.SessionStateData,
                    sessionStateBytes,
                    HashFieldsEnum.InitializeItemFlag,
                    1,
					HashFieldsEnum.Timeout
                });
        }

        public override void Dispose()
        {}

        public override void EndRequest(HttpContext context)
        {}

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
            locked = false;
            lockAge = TimeSpan.Zero;
            lockId = null;
            actions = SessionStateActions.None;

            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            var arguments = new List<RedisValue>(4)
            {
                HashFieldsEnum.SessionStateData,//1
                HashFieldsEnum.Lock,//2
                HashFieldsEnum.InitializeItemFlag,//3
				HashFieldsEnum.Timeout//4
            };
            if (exclusive)
            {
                arguments.Add(GenerateNewLockId());//6
            }
			// session[1] = sessionstate
			// session[2] = lock
			// session[3] = init
			// session[4] = timeout
            var result = (RedisValue[])redis.ScriptEvaluate(
				@"local session = redis.call('HMGET', KEYS[1], ARGV[1], ARGV[2], ARGV[3], ARGV[4])
                  if not session[1] then 
                      return { false }
                  elseif session[2] then
                      return { false, session[2] }
                  end
                  redis.call('EXPIRE', KEYS[1], session[4])
                  if ARGV[5] ~= nil then
                      redis.call('HSET', KEYS[1], ARGV[2], ARGV[5])
                      session[2] = ARGV[5]
                  end
                  local initFlagSet = not not session[3]
                  if initFlagSet then
                      redis.call('HDEL', KEYS[1], ARGV[3])
                  end
                  return { session[1], session[2], initFlagSet, session[4] }",
                new RedisKey[] { key },
                arguments.ToArray());
			//results[0] = sessionstate
			//results[1] = lock
			//results[2] = init flag set
			//results[3] = timeout
            switch (result.Length)
            {
                case 1: // session is not found
                    return null;

                case 2: // session is locked by someone else
                    locked = true;
                    lockId = (string)result[1];
                    lockAge = GetLockAge((string)lockId);
                    return null;

				//case 3: // got session. 
				//	if (exclusive)
				//	{
				//		lockId = (string)result[1];
				//	}
				//	if ((bool)result[2])
				//	{
				//		actions = SessionStateActions.InitializeItem;
				//	}
				//	return new SessionStateStoreData(
				//		DeserializeSessionState(result[0]),
				//		GetSessionStaticObjects(context),
				//		(int)SessionTimeout.TotalMinutes);
				case 4: // got session with a timeout
					if (exclusive)
                    {
                        lockId = (string)result[1];
                    }
                    if ((bool)result[2])
                    {
                        actions = SessionStateActions.InitializeItem;
                    }
					int timeout = 0;
					if(!result[3].TryParse(out timeout))
					{
						timeout = (int)SessionTimeout.TotalMinutes;
					}
                    return new SessionStateStoreData(
                        DeserializeSessionState(result[0]),
                        GetSessionStaticObjects(context),
                        timeout/60);

                default:
                    throw new ProviderException("Invalid count of items in result array.");
            }
        }

        public override void InitializeRequest(HttpContext context)
        {}

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            redis.ScriptEvaluate(
                @"redis.call('EXPIRE', KEYS[1], ARGV[1])
                  if redis.call('HGET', KEYS[1], ARGV[2]) == ARGV[3] then
                      redis.call('HDEL', KEYS[1], ARGV[2])
                  end",
                new RedisKey[] { key },
                new RedisValue[]
                {
                    (long)SessionTimeout.TotalSeconds,
                    HashFieldsEnum.Lock,
                    (string)lockId
                });
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            if (lockId == null) return;
            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            redis.ScriptEvaluate(
                @"local lockId = redis.call('HGET', KEYS[1], ARGV[1])
                  if (ARGV[2] == nil and not lockId) or (ARGV[2] ~= nil and lockId == ARGV[2]) then
                      redis.call('DEL', KEYS[1])
                  end",
                new RedisKey[] { key },
                new RedisValue[]
                {
                    HashFieldsEnum.Lock,
                    (string)lockId
                });
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            //redis.KeyExpire(key, SessionTimeout);
			redis.ScriptEvaluate(
				@"local timeout = redis.call('HGET', KEYS[1], ARGV[1])
                  redis.call('EXPIRE', KEYS[1], timeout)",
				new RedisKey[] { key },
				new RedisValue[] { HashFieldsEnum.Timeout });
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item,
            object lockId, bool newItem)
        {
            if (lockId == null && !newItem) return;

            Tuple<IDatabase, RedisKey> storageDetails = GetSessionStateStorageDetails(context, id);
            IDatabase redis = storageDetails.Item1;
            RedisKey key = storageDetails.Item2;

            byte[] sessionStateData = SerializeSessionState((SessionStateItemCollection)item.Items);

			var timeout = TimeSpan.FromMinutes(item.Timeout);

            var arguments = new List<RedisValue>(6)
            {
                HashFieldsEnum.SessionStateData, //1
                sessionStateData, //2
				HashFieldsEnum.Timeout, //3
                (long)timeout.TotalSeconds, //4
                newItem, //5
                HashFieldsEnum.Lock, //6
            };
            if (lockId != null)
            {
                arguments.Add((string)lockId); //7
            }
            redis.ScriptEvaluate(
                @"local canUpdateSession = true
                  if tonumber(ARGV[5]) == 1 then
                      redis.call('DEL', KEYS[1])
                  elseif ARGV[7] ~= nil and ARGV[7] == redis.call('HGET', KEYS[1], ARGV[6]) then
                      redis.call('HDEL', KEYS[1], ARGV[6])
                  else
                      canUpdateSession = false
                  end
                  if canUpdateSession then
                      redis.call('HMSET', KEYS[1], ARGV[1], ARGV[2], ARGV[3], ARGV[4])
                      redis.call('EXPIRE', KEYS[1], ARGV[4])
                  end",
                new RedisKey[] { key },
                arguments.ToArray());
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        protected static class HashFieldsEnum
        {
            /// <summary>"init"</summary>
            public const string InitializeItemFlag = "init";

            /// <summary>"data"</summary>
            public const string SessionStateData = "data";

            /// <summary>"lock"</summary>
            public const string Lock = "lock";

			/// <summary>
			/// "timeout"
			/// </summary>
			public const string Timeout = "timeout";
        }
    }
}