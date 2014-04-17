RedisAspNetProviders
====================

Redis backed ASP.NET providers written in C# using [`StackExchange.Redis`](https://github.com/StackExchange/StackExchange.Redis). Includes the following providers:
* `OutputCacheProvider`;
* `SessionStateStoreProvider`.

To use `RedisAspNetProviders` in your project it must meet the following requirements:

1. .NET 4.0 or later.
2. Redis 2.6.0 or later.

To build `RedisAspNetProviders` you will need Visual Studio 2013.


Installation
--------------------
1. `RedisAspNetProviders` can be installed via the nuget UI (as [RedisAspNetProviders](https://www.nuget.org/packages/RedisAspNetProviders)) or via the nuget package manager console:

        PM> Install-Package RedisAspNetProviders
2. Alternatively, you can grab sources from here and build.

Configure connection to Redis:
--------------------
Add a connection string for Redis and specify the `connectionStringName` for provider:

```xml
<connectionStrings>
  <add name="RedisConnectionString" connectionString="192.168.0.120,connectionTimeout=10" />
</connectionStrings>
<system.web>
  ...
  <sessionState mode="Custom" customProvider="RedisSessionStateStoreProvider"> 
    <providers>
      <add name="RedisSessionStateStoreProvider"
           type="RedisAspNetProviders.SessionStateStoreProvider, RedisAspNetProviders"
           connectionStringName="RedisConnectionString" />
    </providers>
  </sessionState>
  ...
  <caching>
    <outputCache defaultProvider="RedisOutputCacheProvider">
      <providers>
        <add name="RedisOutputCacheProvider"
             type="RedisAspNetProviders.OutputCacheProvider, RedisAspNetProviders"
             connectionStringName="RedisConnectionString" />
      </providers>
    </outputCache>
  </caching>
  ...
</system.web>
```        

... or specify the `connectionString` directly:

```xml
<system.web>
  ...
  <sessionState mode="Custom" customProvider="RedisSessionStateStoreProvider"> 
    <providers>
      <add name="RedisSessionStateStoreProvider"
           type="RedisAspNetProviders.SessionStateStoreProvider, RedisAspNetProviders"
           connectionString="192.168.0.120,connectionTimeout=10" />
    </providers>
  </sessionState>
  ...
  <caching>
    <outputCache defaultProvider="RedisOutputCacheProvider">
      <providers>
        <add name="RedisOutputCacheProvider"
             type="RedisAspNetProviders.OutputCacheProvider, RedisAspNetProviders"
             connectionString="192.168.0.121,connectionTimeout=10" />
      </providers>
    </outputCache>
  </caching>
  ...
</system.web>
```

For additional information about configuring connection to Redis you can read [`StackExchange.Redis``s documentation](https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Configuration.md).

Additional configuration parameters
--------------------
All providers support several optional parameters:
* `dbNumber` allows you to specify a number of the database which will store sessions; by default `dbNumber=0`;
* `keyPrefix` allows you to specify a prefix for the `RedisKey` which will be used to store session state in Redis; by default `keyPrefix=""`.

```xml
<system.web>
  ...
  <sessionState mode="Custom" customProvider="RedisSessionStateStoreProvider"> 
    <providers>
      <add name="RedisSessionStateStoreProvider"
           type="RedisAspNetProviders.SessionStateStoreProvider, RedisAspNetProviders"
           connectionString="192.168.0.120,connectionTimeout=10"
           dbNumber="1"
           keyPrefix="MyWebApplication/SessionState/" />
    </providers>
  </sessionState>
  ...
  <caching>
    <outputCache defaultProvider="RedisOutputCacheProvider">
      <providers>
        <add name="RedisOutputCacheProvider"
             type="RedisAspNetProviders.OutputCacheProvider, RedisAspNetProviders"
             connectionString="192.168.0.121,connectionTimeout=10"
             dbNumber="1"
             keyPrefix="MyWebApplication/OutputCache/" />
      </providers>
    </outputCache>
  </caching>
  ...
</system.web>
```

*Note*:
If you want to implement your own rules for choosing a Redis database or formatting a `RedisKey` you can override `SessionStateStoreProvider.GetSessionStateStorageDetails(httpContext, sessionId)` and `OutputCacheProvider.GetCacheEntryStorageDetails(cacheEntryKey)` methods. By default these methods return configured via `dbNumber` Redis database proxy and RedisKey which is generated as `keyPrefix + sessionId` for `SessionStateStoreProvider` and `keyPrefix + cacheEntryKey` for `OutputCacheProvider`.


Custom serialization, compression, etc.
--------------------
All providers have protected virtual methods `Serialize***()` and `Deserialize***()`. Feel free to inherit from necessary provider and implement your own serialization mechanism.
