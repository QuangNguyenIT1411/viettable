using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SelfRestaurant.Gateway.Api.Infrastructure;
using VietTable.CloudHost.Auth;
using VietTable.CloudHost.Composition;
using Xunit;

public sealed class StaffLogoutTests
{
    [Fact]
    public async Task Logout_revokes_replayed_ticket_without_revoking_other_sessions_and_relogin_works()
    {
        var fixture = new Validator();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        var server = new ModuleServer();
        builder.Services.AddSingleton<IServer>(server);
        builder.Services.AddSingleton<IStaffCookieValidator>(fixture);
        builder.Services.AddAuthentication().AddCookie(StaffCookieState.Scheme, options => options.Cookie.Name = "staff-test");
        await using var app = builder.Build();
        app.UseMiddleware<StaffCookieMiddleware>();
        app.MapGet("/login", (HttpContext c) => {
            c.StaffIdentity().SetInt32(SessionKeys.EmployeeId, 1);
            c.StaffIdentity().SetString(SessionKeys.EmployeeRoleCode, "CHEF");
            c.StaffIdentity().SetInt32(SessionKeys.EmployeeBranchId, 3);
            return Results.Ok();
        });
        app.MapPost("/logout", (HttpContext c) => { c.StaffIdentity().Clear(); return Results.Ok(); });
        app.MapGet("/protected", (HttpContext c) => c.StaffIdentity().GetInt32(SessionKeys.EmployeeId) is > 0
            ? Results.Ok() : Results.Unauthorized());
        await app.StartAsync();
        var router = new ModuleRouter(); router.Register("gateway", server);
        using var client = new HttpClient(new ModuleDispatchHandler(router) { InnerHandler = new HttpClientHandler() });
        async Task<string> Login()
        {
            using var r = await client.GetAsync("http://gateway.module.invalid/login");
            r.EnsureSuccessStatusCode();
            return r.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("staff-test=")).Split(';')[0];
        }
        async Task<int> Send(string cookie, string path, bool post = false)
        {
            using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, "http://gateway.module.invalid" + path);
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request);
            return (int)response.StatusCode;
        }
        var first = await Login();
        var independent = await Login();
        Assert.Equal(200, await Send(first, "/protected"));
        Assert.Equal(200, await Send(first, "/logout", true));
        Assert.Equal(401, await Send(first, "/protected"));
        Assert.Equal(200, await Send(independent, "/protected"));
        Assert.Equal(200, await Send(await Login(), "/protected"));
        Assert.All(fixture.Revoked, hash => Assert.Matches("^[A-F0-9]{64}$", hash));
        await app.StopAsync();
    }

    private sealed class Validator : IStaffCookieValidator
    {
        public HashSet<string> Revoked { get; } = [];
        public Task<string?> VersionAsync(int employeeId, string role, int branch, CancellationToken ct) => Task.FromResult<string?>("fixture-version");
        public Task<bool> IsRevokedAsync(string hash, CancellationToken ct) => Task.FromResult(Revoked.Contains(hash));
        public Task RevokeAsync(string hash, DateTimeOffset expiry, CancellationToken ct) { Revoked.Add(hash); return Task.CompletedTask; }
    }
}
