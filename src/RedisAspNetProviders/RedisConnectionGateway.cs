using BookSleeve;
using System;
using System.Diagnostics;
using System.Threading;

namespace RedisAspNetProviders
{
    sealed class RedisConnectionGateway : IDisposable
    {
        readonly object _syncLock;
        readonly RedisConnectionSettings _settings;
        RedisConnection _cachedConnection;

        public RedisConnectionGateway(RedisConnectionSettings settings)
        {
            _settings = settings;
            _syncLock = new object();
        }

        public RedisConnection GetConnection()
        {
            while (true)
            {
                Monitor.Enter(_syncLock);
                try
                {
                    if (_cachedConnection == null)
                    {
                        _cachedConnection = new RedisConnection(
                            host: _settings.Host,
                            port: _settings.Port,
                            password: _settings.Password, 
                            ioTimeout: _settings.IOTimeout,
                            syncTimeout: _settings.SyncTimeout);
                    }

                    if (_cachedConnection.State == RedisConnectionBase.ConnectionState.New)
                    {
                        _cachedConnection.Wait(_cachedConnection.Open());
                    }

                    if (_cachedConnection.State == RedisConnectionBase.ConnectionState.Open)
                    {
                        return _cachedConnection;
                    }

                    if (_cachedConnection.State == RedisConnectionBase.ConnectionState.Closing ||
                        _cachedConnection.State == RedisConnectionBase.ConnectionState.Closed)
                    {
                        _cachedConnection.Dispose();
                        _cachedConnection = null;
                    }
                }
#if DEBUG
                catch
                {
                    if (Debugger.IsAttached) Debugger.Break();
                    throw;
                }
#endif
                finally
                {
                    Monitor.Exit(_syncLock);
                }
            }
        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_cachedConnection != null)
                {
                    _cachedConnection.Dispose();
                    _cachedConnection = null;
                }
            }
        }

    }

}

