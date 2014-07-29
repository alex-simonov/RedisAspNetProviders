using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RedisAspNetProviders.Tests
{
    public class ProviderCommonTests<T> where T : ProviderBase, new()
    {
        private static readonly object s_providerLock = new object();

        private static readonly PrivateType s_providerType = new PrivateType(typeof (T));

        protected T CreateProvider()
        {
            var provider = new T();
            provider.Initialize("", new NameValueCollection
            {
                { "connectionString", "192.168.0.195:6379,connectTimeout=10000" }
            });
            return provider;
        }

        [TestInitialize]
        public void ResetInitialization()
        {
            Monitor.Enter(s_providerLock);
            s_providerType.SetStaticField("s_oneTimeInitCalled", false);
        }

        [TestCleanup]
        public void ReleaseLock()
        {
            Monitor.Exit(s_providerLock);
        }

        [TestMethod]
        [ExpectedException(typeof (ConfigurationErrorsException))]
        public void ThrowsConfigurationErrorsExceptionOnRedisHostMissing()
        {
            var config = new NameValueCollection();
            var provider = new T();

            provider.Initialize(null, config);
        }

        [TestMethod]
        [ExpectedException(typeof (ConfigurationErrorsException))]
        public void ThrowsConfigurationErrorsExceptionOnRedisHostEmptyValue()
        {
            var config = new NameValueCollection
            {
                { "connectionString", "" }
            };
            var provider = new T();

            provider.Initialize(null, config);
        }

        [TestMethod]
        public void CanInitialize()
        {
            CreateProvider();
        }
    }
}
