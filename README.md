RedisAspNetProviders
====================

Redis backed ASP.NET providers written in C# using [`StackExchange.Redis`](https://github.com/StackExchange/StackExchange.Redis). Currently includes the following providers:
* `OutputCacheProvider`;
* `SessionStateStoreProvider`.

Common requirements:
--------------------
1. .NET 4.5 or later (`StackExchange.Redis` is compiled for .NET 4.5).
2. Redis 2.6.0 or later.

Custom serialization:
--------------------
All providers have protected virtual methods `Serialize***()` and `Deserialize***()`. Feel free to inherit from necessary provider and implement your own serialization mechanism.



