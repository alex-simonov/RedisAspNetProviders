using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.Fakes;
using System.Web.SessionState;
using System.Web.SessionState.Fakes;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RedisAspNetProviders.Tests
{
    [TestClass]
    public class SessionStateStoreProviderTests : ProviderCommonTests<SessionStateStoreProvider>
    {
        public int SessionTimeoutInMinutesFromConfig
        {
            get
            {
                return
                    (int)
                        ((SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState"))
                            .Timeout.TotalMinutes;
            }
        }

        private string GenerateKey([CallerMemberName] string testName = null)
        {
            return string.Concat(testName ?? string.Empty, Guid.NewGuid().ToString("N"));
        }

        private static IDisposable ConfigureShimsContrext(out HttpContext httpCtx)
        {
            IDisposable shimsCtx = ShimsContext.Create();
            httpCtx = new ShimHttpContext().Instance;
            HttpStaticObjectsCollection staticObjects = new ShimHttpStaticObjectsCollection().Instance;
            ShimSessionStateUtility.GetSessionStaticObjectsHttpContext = context => staticObjects;
            return shimsCtx;
        }

        private static void AssertSessionNotExists(SessionStateStoreData storeData, bool locked, TimeSpan lockAge,
            object lockId, SessionStateActions actions)
        {
            Assert.IsNull(storeData);
            Assert.IsFalse(locked);
            Assert.AreEqual(TimeSpan.Zero, lockAge);
            Assert.IsNull(lockId);
            Assert.AreEqual(SessionStateActions.None, actions);
        }

        private static void AssertSessionIsLocked(SessionStateStoreData storeData, bool locked, TimeSpan lockAge,
            object lockId,
            object lockId2, SessionStateActions actions)
        {
            Assert.IsNull(storeData);
            Assert.IsTrue(locked);
            Assert.IsTrue(lockAge > TimeSpan.Zero);
            Assert.AreEqual(lockId, lockId2);
            Assert.AreEqual(SessionStateActions.None, actions);
        }

        [TestMethod]
        public void CreateNewStoreDataNotFails()
        {
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                SessionStateStoreData storeData = provider.CreateNewStoreData(httpCtx, SessionTimeoutInMinutesFromConfig);

                Assert.IsNotNull(storeData);
                Assert.IsTrue(storeData.Items.Count == 0);
                Assert.IsFalse(storeData.Items.Dirty);
                Assert.AreEqual(SessionTimeoutInMinutesFromConfig, storeData.Timeout);
            }
        }

        [TestMethod]
        public void GetNonExistingItemReturnsNull()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertSessionNotExists(storeData, locked, lockAge, lockId, actions);
            }
        }

        [TestMethod]
        public void GetNonExistingItemExclusiveReturnsNull()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertSessionNotExists(storeData, locked, lockAge, lockId, actions);
            }
        }

        [TestMethod]
        public void CreateUninitializedItemAndGetReturnsEmptySessionWithInitFlag()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
                Assert.AreEqual(SessionStateActions.InitializeItem, actions);
                Assert.IsTrue(storeData.Items.Count == 0);
                Assert.IsFalse(storeData.Items.Dirty);
            }
        }

        private static void AssertGotSession(SessionStateStoreData storeData, bool locked, TimeSpan lockAge,
            object lockId, bool exclusive = false, object originalLock = null)
        {
            Assert.IsNotNull(storeData);
            Assert.IsFalse(locked);
            Assert.AreEqual(TimeSpan.Zero, lockAge);
            if (exclusive)
            {
                if (originalLock == null)
                {
                    Assert.IsNotNull(lockId);
                }
                else
                {
                    Assert.AreEqual(originalLock, lockId);
                }
            }
            else
            {
                Assert.IsNull(lockId);
            }
        }

        [TestMethod]
        public void CreateUninitializedItemAndGetExclusiveReturnsEmptySessionWithInitFlagAndLock()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId, true);
                Assert.AreEqual(SessionStateActions.InitializeItem, actions);
                Assert.IsTrue(storeData.Items.Count == 0);
                Assert.IsFalse(storeData.Items.Dirty);
            }
        }

        [TestMethod]
        public void GetExclusiveAndGetReturnsLocked()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId, lockId2;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                Thread.Sleep(100);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }

        [TestMethod]
        public void GetExclusiveAndGetExclusiveReturnsLocked()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId, lockId2;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                Thread.Sleep(100);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }

        [TestMethod]
        public void GetItemRemovesInitFlag1()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);

                Assert.AreEqual(SessionStateActions.None, actions);
            }
        }

        [TestMethod]
        public void GetItemRemovesInitFlag2()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId, true);
                Assert.AreEqual(SessionStateActions.None, actions);
            }
        }

        [TestMethod]
        public void ReleaseExclusiveLockedItemWithInvalidLockNotReleasesSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                object lockId2 = (string)lockId + "invalid";
                provider.ReleaseItemExclusive(httpCtx, sessionId, lockId2);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }

        [TestMethod]
        public void ReleaseExclusiveLockedItemReleasesSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                provider.ReleaseItemExclusive(httpCtx, sessionId, lockId);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
                Assert.AreEqual(SessionStateActions.None, actions);
            }
        }

        [TestMethod]
        public void GetItemExclusiveRemovesInitFlag()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);
                provider.ReleaseItemExclusive(httpCtx, sessionId, lockId);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId, true);
                Assert.AreEqual(SessionStateActions.None, actions);
            }
        }

        [TestMethod]
        public void RemoveExclusiveLockedItem()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);
                provider.RemoveItem(httpCtx, sessionId, lockId, storeData);
                storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);

                AssertSessionNotExists(storeData, locked, lockAge, lockId, actions);
            }
        }

        [TestMethod]
        public void RemoveNotLockedItemNotRemovesSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);
                provider.RemoveItem(httpCtx, sessionId, lockId, storeData);
                storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
            }
        }

        [TestMethod]
        public void RemoveLockedItemWithInvalidNotRemovesSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                object lockId2;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);
                provider.RemoveItem(httpCtx, sessionId, lockId + "invalid", storeData);
                Thread.Sleep(100);
                storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }

        [TestMethod]
        public void RemoveLockedItemWithNullLockNotRemovesSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId, lockId2;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData storeData = provider.GetItemExclusive(httpCtx, sessionId, out locked, out lockAge,
                    out lockId,
                    out actions);
                provider.RemoveItem(httpCtx, sessionId, null, storeData);
                Thread.Sleep(100);
                storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge, out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }

        [TestMethod]
        public void SetAndReleaseNewItemSavesSessionAndNotSetsInitFlag()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                SessionStateStoreData newStoreData = provider.CreateNewStoreData(httpCtx,
                    SessionTimeoutInMinutesFromConfig);
                newStoreData.Items[Guid.NewGuid().ToString("N")] = Guid.NewGuid();
                provider.SetAndReleaseItemExclusive(httpCtx, sessionId, newStoreData, null, true);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
                Assert.AreEqual(SessionStateActions.None, actions);
                CollectionAssert.AreEqual(newStoreData.Items, storeData.Items);
            }
        }

        [TestMethod]
        public void SetAndReleaseOldLockedItemSavesAndUnlocksSession()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData newStoreData = provider.GetItemExclusive(httpCtx, sessionId, out locked,
                    out lockAge, out lockId,
                    out actions);
                newStoreData.Items[Guid.NewGuid().ToString("N")] = Guid.NewGuid();
                provider.SetAndReleaseItemExclusive(httpCtx, sessionId, newStoreData, lockId, false);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
                Assert.AreEqual(SessionStateActions.None, actions);
                CollectionAssert.AreEqual(newStoreData.Items, storeData.Items);
            }
        }

        [TestMethod]
        public void SetAndReleaseItemNotModifiesExistingSessionWithoutLock()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData newStoreData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId,
                    out actions);
                newStoreData.Items[Guid.NewGuid().ToString("N")] = Guid.NewGuid();
                provider.SetAndReleaseItemExclusive(httpCtx, sessionId, newStoreData, lockId, false);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId, out actions);

                AssertGotSession(storeData, locked, lockAge, lockId);
                Assert.AreEqual(SessionStateActions.None, actions);
                Assert.IsTrue(storeData.Items.Count == 0);
            }
        }

        [TestMethod]
        public void SetAndReleaseLockedItemNotReleasesSessionWithInvalidLock()
        {
            string sessionId = GenerateKey();
            SessionStateStoreProvider provider = CreateProvider();

            HttpContext httpCtx;
            using (ConfigureShimsContrext(out httpCtx))
            {
                bool locked;
                TimeSpan lockAge;
                object lockId, lockId2;
                SessionStateActions actions;

                provider.CreateUninitializedItem(httpCtx, sessionId, SessionTimeoutInMinutesFromConfig);
                SessionStateStoreData newStoreData = provider.GetItemExclusive(httpCtx, sessionId, out locked,
                    out lockAge, out lockId,
                    out actions);
                newStoreData.Items[Guid.NewGuid().ToString("N")] = Guid.NewGuid();
                provider.SetAndReleaseItemExclusive(httpCtx, sessionId, newStoreData, lockId + "invalid", false);
                SessionStateStoreData storeData = provider.GetItem(httpCtx, sessionId, out locked, out lockAge,
                    out lockId2, out actions);

                AssertSessionIsLocked(storeData, locked, lockAge, lockId, lockId2, actions);
            }
        }
    }
}