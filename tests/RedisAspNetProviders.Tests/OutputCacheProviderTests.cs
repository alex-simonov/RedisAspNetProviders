using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RedisAspNetProviders.Fakes;

namespace RedisAspNetProviders.Tests
{
    [TestClass]
    public class OutputCacheProviderTests : ProviderCommonTests<OutputCacheProvider>
    {
        private const int Timeout = 2;
        private const string OldData = "old Data";
        private const string NewData = "new data";

        private string GenerateKey([CallerMemberName] string testName = null)
        {
            return string.Concat(testName ?? string.Empty, Guid.NewGuid().ToString("N"));
        }

        [TestMethod]
        public void GetNonExistingReturnsNull()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            object entry = provider.Get(entryKey);

            Assert.IsNull(entry);
        }

        [TestMethod]
        public void RemoveNonExistingNotThrows()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Remove(entryKey);
        }

        [TestMethod]
        public void RemoveExistingSuccess()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Set(entryKey, NewData, DateTime.UtcNow.AddMinutes(10));
            provider.Remove(entryKey);

            Assert.IsNull(provider.Get(entryKey));
        }

        [TestMethod]
        public void SetAndGetReturnsCachedEntry()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Set(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            object entry = provider.Get(entryKey);

            Assert.AreEqual(NewData, entry);
        }

        [TestMethod]
        public void SetExpires()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Set(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            Thread.Sleep(TimeSpan.FromSeconds(Timeout + 0.05));
            object entry = provider.Get(entryKey);

            Assert.IsNull(entry);
        }

        [TestMethod]
        public void SetReplacesExistingData()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Set(entryKey, OldData, DateTime.UtcNow.AddSeconds(Timeout));
            provider.Set(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            object entry = provider.Get(entryKey);

            Assert.AreEqual(NewData, entry);
        }

        [TestMethod]
        public void AddAndGetReturnsCachedEntry()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Add(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            object entry = provider.Get(entryKey);

            Assert.AreEqual(NewData, entry);
        }

        [TestMethod]
        public void AddExpires()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Add(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            Thread.Sleep(TimeSpan.FromSeconds(Timeout + 0.05));
            object entry = provider.Get(entryKey);

            Assert.IsNull(entry);
        }

        [TestMethod]
        public void AddNotReplacesExistingData()
        {
            string entryKey = GenerateKey();
            OutputCacheProvider provider = CreateProvider();

            provider.Add(entryKey, OldData, DateTime.UtcNow.AddSeconds(Timeout));
            provider.Add(entryKey, NewData, DateTime.UtcNow.AddSeconds(Timeout));
            object entry = provider.Get(entryKey);

            Assert.AreEqual(OldData, entry);
        }

        [TestMethod]
        public void AddContention()
        {
            const int timeout = 5;
            string entryKey = GenerateKey();

            OutputCacheProvider provider = CreateProvider();
            using (ShimsContext.Create())
            using (var canSetEvent = new AutoResetEvent(false))
            using (var canResumeAddEvent = new AutoResetEvent(false))
            {
                var adder = new ShimOutputCacheProvider(CreateProvider());
                adder.AddOrGetExistingObjectDateTimeIDatabaseRedisKey = (entry, expiresAt, redis, key) =>
                {
                    canSetEvent.Set();
                    canResumeAddEvent.WaitOne();
                    return provider.AddOrGetExisting(entry, expiresAt, redis, key);
                };

                Task<object> addTask = Task.Factory.StartNew(
                    () => adder.Instance.Add(entryKey, OldData, DateTime.UtcNow.AddSeconds(timeout)),
                    TaskCreationOptions.LongRunning);

                canSetEvent.WaitOne();
                provider.Set(entryKey, NewData, DateTime.UtcNow.AddSeconds(timeout));
                canResumeAddEvent.Set();

                object cachedData = addTask.Result;

                Assert.AreEqual(NewData, cachedData);
            }
        }
    }
}