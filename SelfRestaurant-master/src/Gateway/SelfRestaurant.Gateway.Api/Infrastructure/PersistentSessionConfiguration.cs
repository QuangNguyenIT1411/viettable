using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

public static class PersistentSessionConfiguration
{
    public static IServiceCollection AddVietTableSession(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // Preserve existing LOCAL configuration, including the Production-named local Compose.
        // Staging must explicitly provide a durable cache instead of silently falling back to RAM.
        var provider = configuration["SessionStore:Provider"] ?? (environment.IsStaging() ? "Redis" : "Memory");
        var redis = provider.Equals("Redis", StringComparison.OrdinalIgnoreCase);
        if (!redis && !provider.Equals("Memory", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported SessionStore provider.");
        if (environment.IsStaging() && !redis)
            throw new InvalidOperationException("Staging requires Redis-backed sessions and DataProtection.");

        if (redis)
        {
            var text = configuration["SessionStore:RedisConfiguration"];
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("SessionStore:RedisConfiguration is required.");
            ConfigurationOptions options;
            try { options = ConfigurationOptions.Parse(text); }
            catch { throw new InvalidOperationException("Invalid Redis session configuration."); }
            options.AbortOnConnectFail = true;
            // Stable, environment-specific namespace prevents sharing auth cookies across environments.
            var applicationName = configuration["SessionStore:ApplicationName"];
            if (string.IsNullOrWhiteSpace(applicationName))
                throw new InvalidOperationException("SessionStore:ApplicationName is required for Redis.");
            var prefix = applicationName + ":";
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                try { return ConnectionMultiplexer.Connect(options); }
                catch { throw new InvalidOperationException("Redis session store is unavailable."); }
            });
            services.AddStackExchangeRedisCache(_ => { });
            services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>((cache, connection) =>
            {
                cache.InstanceName = prefix + "session:";
                cache.ConnectionMultiplexerFactory = () => Task.FromResult(connection);
            });
            services.AddDataProtection().SetApplicationName(applicationName);
            services.AddOptions<KeyManagementOptions>().Configure<IConnectionMultiplexer>((keys, connection) =>
                keys.XmlRepository = new RedisXmlRepository(() => connection.GetDatabase(), prefix + "data-protection"));
        }
        else services.AddDistributedMemoryCache();

        services.AddSession(session =>
        {
            session.Cookie.HttpOnly = true;
            session.Cookie.IsEssential = true;
            session.Cookie.SameSite = SameSiteMode.Lax;
            session.Cookie.SecurePolicy = redis && !environment.IsDevelopment()
                ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            session.IdleTimeout = TimeSpan.FromHours(8);
        });
        return services;
    }
}
