using System.Net;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using SelfRestaurant.Gateway.Api;
using VietTable.CloudHost.Auth;
using VietTable.CloudHost.Composition;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class StaffPreviewRoutingTests
{
    [Fact]
    public async Task Existing_gateway_serves_three_staff_deep_links_and_keeps_customer_web_absent()
    {
        var root = Path.Combine(Path.GetTempPath(), "viettable-staff-routing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var role in new[] { "chef", "cashier", "admin" })
            {
                var folder = Path.Combine(root, "wwwroot", role + "-app"); Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, "index.html"), "staff-routing-test:" + role);
            }
            var server = new ModuleServer();
            await using var app = await GatewayModule.BuildAsync([], new WebApplicationOptions {
                ContentRootPath = root, EnvironmentName = "Production",
                ApplicationName = typeof(GatewayModule).Assembly.FullName }, builder => {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                        ["CloudHost:StatelessStaff"] = "true", ["CloudHost:ExposeStaffWeb"] = "true",
                        ["MobileJwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                        ["Runtime:LegacyPersistentWorkers"] = "false", ["RabbitMq:Enabled"] = "false"
                    });
                    builder.Services.AddSingleton<IServer>(server);
                }, builder => {
                    builder.Services.AddAuthentication().AddCookie(StaffCookieState.Scheme);
                    builder.Services.AddSingleton<IStaffCookieValidator>(new NoStaffFixtureValidator());
                }, gateway => gateway.UseMiddleware<StaffCookieMiddleware>());
            await app.StartAsync();
            var router = new ModuleRouter(); router.Register("gateway", server);
            using var client = new HttpClient(new ModuleDispatchHandler(router) { InnerHandler = new HttpClientHandler() });
            foreach (var (role, path) in new[] {
                ("chef", "/app/chef/Staff/Account/Login"),
                ("cashier", "/app/cashier/Staff/Cashier/Index"),
                ("admin", "/app/admin/Admin/Dashboard/Index") })
            {
                using var result = await client.GetAsync("http://gateway.module.invalid" + path);
                Assert.Equal(HttpStatusCode.OK, result.StatusCode);
                Assert.Equal("staff-routing-test:" + role, await result.Content.ReadAsStringAsync());
                Assert.Contains("no-cache", result.Headers.CacheControl!.ToString());
            }
            using (var home = await client.GetAsync("http://gateway.module.invalid/"))
            {
                Assert.Equal(HttpStatusCode.OK, home.StatusCode);
                Assert.Equal("text/html", home.Content.Headers.ContentType!.MediaType);
                Assert.Contains("no-cache", home.Headers.CacheControl!.ToString());
                var html = await home.Content.ReadAsStringAsync();
                foreach (var link in new[] { "href=\"/app/admin/\">Admin<", "href=\"/app/chef/\">Bếp<", "href=\"/app/cashier/\">Thu ngân<" })
                    Assert.Contains(link, html);
            }
            using (var qr = await client.GetAsync("http://gateway.module.invalid/Menu/FromQr?code=QR-Q1-B01"))
            {
                Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
                Assert.Equal("text/html", qr.Content.Headers.ContentType!.MediaType);
                Assert.Contains("no-cache", qr.Headers.CacheControl!.ToString());
                var html = await qr.Content.ReadAsStringAsync();
                Assert.Contains("Mở app VietTable và quét lại mã này", html);
                Assert.Contains("<strong>QR-Q1-B01</strong>", html);
                Assert.DoesNotContain("staff-routing-test", html);
            }
            foreach (var hostile in new[] { "<script>alert(1)</script>", "QR\"><img src=x onerror=alert(1)>", new string('A', 65) })
            {
                using var qr = await client.GetAsync("http://gateway.module.invalid/menu/fromqr?code=" + Uri.EscapeDataString(hostile));
                Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
                var html = await qr.Content.ReadAsStringAsync();
                Assert.Contains("Mở app VietTable và quét lại mã này", html);
                Assert.DoesNotContain("Mã bàn:", html);
                Assert.DoesNotContain("<script", html);
                Assert.DoesNotContain("onerror", html);
            }
            using (var noCode = await client.GetAsync("http://gateway.module.invalid/Menu/FromQr"))
            {
                Assert.Equal(HttpStatusCode.OK, noCode.StatusCode);
                Assert.DoesNotContain("Mã bàn:", await noCode.Content.ReadAsStringAsync());
            }
            foreach (var path in new[] { "/api/gateway/staff/chef/dashboard", "/api/mobile/v1/orders", "/hubs/mobile-notifications/negotiate" })
            {
                using var protectedResult = await client.GetAsync("http://gateway.module.invalid" + path);
                Assert.True(protectedResult.Headers.CacheControl?.NoStore == true);
                Assert.DoesNotContain("staff-routing-test", await protectedResult.Content.ReadAsStringAsync());
            }
            foreach (var (origin, expected) in new[] {
                ("https://gateway.module.invalid", HttpStatusCode.BadRequest),
                ("https://foreign.example", HttpStatusCode.Forbidden),
                ("http://gateway.module.invalid", HttpStatusCode.Forbidden) })
            {
                using var login = new HttpRequestMessage(HttpMethod.Post, "http://gateway.module.invalid/api/gateway/staff/auth/login") {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json") };
                login.Headers.Add("Origin", origin);
                using var result = await client.SendAsync(login);
                Assert.Equal(expected, result.StatusCode);
            }
            await app.StopAsync();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class NoStaffFixtureValidator : IStaffCookieValidator
    {
        public Task<bool> IsRevokedAsync(string ticketHash, CancellationToken ct) => throw new InvalidOperationException("No staff cookie expected.");
        public Task RevokeAsync(string ticketHash, DateTimeOffset expiry, CancellationToken ct) => throw new InvalidOperationException("No staff cookie expected.");
        public Task<string?> VersionAsync(int employeeId, string role, int branch, CancellationToken ct) =>
            throw new InvalidOperationException("This routing/origin test must not access staff identity or database.");
    }
}
