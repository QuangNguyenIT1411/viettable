using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Gateway.Api.Infrastructure;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class CloudSessionTests
{
    [Fact]
    public void Staging_rejects_RAM_fallback_and_missing_Redis_configuration()
    {
        var environment = new TestEnvironment("Staging");
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddVietTableSession(
            new ConfigurationBuilder().Build(), environment));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddVietTableSession(
            Config(new() { ["SessionStore:Provider"] = "Memory" }), environment));
    }

    [Fact]
    public void LOCAL_defaults_are_unchanged_and_Redis_cloud_cookie_is_secure()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddVietTableSession(Config(new()), new TestEnvironment("Production"));
        using var local = services.BuildServiceProvider();
        Assert.Equal(CookieSecurePolicy.SameAsRequest, local.GetRequiredService<IOptions<SessionOptions>>().Value.Cookie.SecurePolicy);
        var cloud = new ServiceCollection().AddLogging();
        cloud.AddVietTableSession(Config(new()
        {
            ["SessionStore:Provider"] = "Redis", ["SessionStore:RedisConfiguration"] = "localhost:6379",
            ["SessionStore:ApplicationName"] = "viettable-staging"
        }), new TestEnvironment("Staging"));
        using var provider = cloud.BuildServiceProvider();
        Assert.Equal(CookieSecurePolicy.Always, provider.GetRequiredService<IOptions<SessionOptions>>().Value.Cookie.SecurePolicy);
    }

    // Opt-in real Redis runtime acceptance, not a mocked distributed cache.
    [RedisRuntimeFact]
    [Trait("Category", "RedisRuntime")]
    public async Task Session_cookie_and_DataProtection_survive_host_restart_and_work_across_instances()
    {
        var connection = Environment.GetEnvironmentVariable("VIETTABLE_REDIS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Redis test connection required.");
        var application = "viettable-session-test-" + Guid.NewGuid().ToString("N");
        var principal = Guid.NewGuid().ToString("N");
        string cookie, protectedValue;
        await using (var first = await Start(connection, application, principal))
        {
            using var result = await first.GetTestClient().GetAsync("https://localhost/establish");
            Assert.True(result.IsSuccessStatusCode);
            var header = result.Headers.GetValues("Set-Cookie").Single();
            Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
            cookie = header.Split(';')[0];
            protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Protect(principal);
            await using var replica = await Start(connection, application, principal);
            await AssertSession(replica, cookie, principal);
        } // All process-level DI/cache/keys are disposed before constructing the replacement.
        await using var restarted = await Start(connection, application, principal);
        await AssertSession(restarted, cookie, principal);
        Assert.Equal(principal, restarted.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("restart-test").Unprotect(protectedValue));
        await using var otherEnvironment = await Start(connection, application + "-other", principal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/read");
        request.Headers.Add("Cookie", cookie);
        using var response = await otherEnvironment.GetTestClient().SendAsync(request);
        Assert.Equal("missing", await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertSession(WebApplication app, string cookie, string expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/read");
        request.Headers.Add("Cookie", cookie);
        using var response = await app.GetTestClient().SendAsync(request);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }

    private static async Task<WebApplication> Start(string redis, string application, string principal)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddVietTableSession(Config(new()
        {
            ["SessionStore:Provider"] = "Redis", ["SessionStore:RedisConfiguration"] = redis,
            ["SessionStore:ApplicationName"] = application
        }), builder.Environment);
        var app = builder.Build();
        app.UseSession();
        // Test-only endpoints, never compiled into the production Gateway's routes.
        app.MapGet("/establish", (HttpContext context) =>
        {
            context.Session.SetString("test-principal", principal);
            return Results.Ok();
        });
        app.MapGet("/read", (HttpContext context) => Results.Text(context.Session.GetString("test-principal") ?? "missing"));
        await app.StartAsync();
        return app;
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CloudSessionTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public sealed class RedisRuntimeFactAttribute : FactAttribute
{
    public RedisRuntimeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIETTABLE_REDIS_TEST_CONNECTION")))
            Skip = "Requires real disposable Redis; run scripts/test-cloud-session.ps1.";
    }
}
