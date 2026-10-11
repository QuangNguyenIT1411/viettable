using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using SelfRestaurant.Gateway.Api.Infrastructure;
using VietTable.CloudHost.Auth;
using VietTable.CloudHost.Composition;
using Xunit;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Identity.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using SelfRestaurant.Gateway.Api.Hubs;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class CloudHostCompositionTests
{
    [Fact]
    public async Task Module_dispatch_decodes_route_keys_once_and_preserves_encoded_slashes()
    {
        var builder = WebApplication.CreateBuilder();
        var server = new ModuleServer();
        builder.Services.AddSingleton<IServer>(server);
        await using var app = builder.Build();
        app.MapGet("/path/{**path}", (HttpContext context) => Results.Json(context.Request.Path.Value));
        await app.StartAsync();
        using var client = new HttpClient(new ModuleDispatchHandler(CreateRouter(server)) { InnerHandler = new HttpClientHandler() });
        foreach (var (encoded, expected) in new[] {
            ("/path/c8%3Acreate%3Akey", "/path/c8:create:key"),
            ("/path/b%C3%A0n", "/path/bàn"),
            ("/path/a%2Fb", "/path/a%2Fb") })
            Assert.Equal(expected, await client.GetFromJsonAsync<string>("http://test.module.invalid" + encoded));
        await app.StopAsync();
    }
    private static ModuleRouter CreateRouter(ModuleServer server)
    {
        var router = new ModuleRouter(); router.Register("test", server); return router;
    }

    [Fact]
    public async Task Cloud_staff_hub_rejects_foreign_branch_and_legacy_table_subscription()
    {
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { ["CloudHost:StatelessStaff"]="true" }).Build())
            .BuildServiceProvider();
        var http=new DefaultHttpContext { RequestServices=services };
        var state=new StaffCookieState(null);
        state.SetInt32(SessionKeys.EmployeeId,27); state.SetInt32(SessionKeys.EmployeeBranchId,4);
        http.Items[StaffIdentityState.ItemKey]=state;
        var caller=new TestHubCaller(http);
        var groups=new TestGroups();
        var hub=new CustomerNotificationsHub { Context=caller,Groups=groups };
        await hub.SubscribeBranch(4);
        Assert.Equal("branch:4",groups.Joined.Single());
        await Assert.ThrowsAsync<HubException>(()=>hub.SubscribeBranch(5));
        await Assert.ThrowsAsync<HubException>(()=>hub.SubscribeTable(4));
        Assert.Single(groups.Joined);
    }
    [Fact]
    public void Employee_nullable_timestamp_sort_generates_Postgres_SQL_and_reset_state_is_Identity_owned()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=design_only").Options);
        var sql = db.Employees.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt ?? e.UpdatedAt ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc))
            .Select(e => e.EmployeeID).ToQueryString();
        Assert.Contains("ORDER BY", sql);
        var token = db.Model.FindEntityType(typeof(SelfRestaurant.Identity.Api.Persistence.Entities.CloudStaffResetToken))!;
        Assert.Equal("identity", token.GetSchema());
        Assert.Equal("TokenHash", token.FindPrimaryKey()!.Properties.Single().Name);
        Assert.DoesNotContain(token.GetProperties(), x => x.Name == "Token" || x.Name == "Password");
    }
    [Fact]
    public async Task Direct_module_dispatch_preserves_body_status_headers_and_request_scope_without_sockets()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName="Production" });
        var server = new ModuleServer();
        builder.Services.AddSingleton<IServer>(server);
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddHttpContextAccessor();
        await using var app = builder.Build();
        app.MapPost("/echo", async (HttpContext context, ScopeMarker scope) => {
            using var timing = VietTable.CloudHost.Diagnostics.CloudPerformanceDiagnostics.Begin("Test", "action");
            timing.RecordQuery(TimeSpan.FromMilliseconds(2));
            var payload = await context.Request.ReadFromJsonAsync<Payload>();
            context.Response.OnStarting(() => { context.Response.Headers["X-Module"]="private"; return Task.CompletedTask; });
            return Results.Json(new {payload!.Value, scope.Id},statusCode:201);
        });
        await app.StartAsync();
        var router = new ModuleRouter(); router.Register("test",server);
        using var client = new HttpClient(new ModuleDispatchHandler(router) { InnerHandler=new HttpClientHandler() })
            { BaseAddress=new Uri("http://test.module.invalid") };
        var accessor = new HttpContextAccessor();
        var outer = new DefaultHttpContext(); accessor.HttpContext=outer;
        using var requestTiming = VietTable.CloudHost.Diagnostics.CloudPerformanceDiagnostics.Begin("Gateway", "request");
        using var first = await client.PostAsJsonAsync("/echo",new Payload("real pipeline"));
        using var second = await client.PostAsJsonAsync("/echo",new Payload("second request"));
        Assert.Equal(HttpStatusCode.Created,first.StatusCode);
        Assert.Same(outer,accessor.HttpContext);
        Assert.Contains("sql-sum;dur=4.0", requestTiming.ServerTiming);
        Assert.Contains("sql-count;desc=\"2\"", requestTiming.ServerTiming);
        Assert.Equal("private",first.Headers.GetValues("X-Module").Single());
        var one=await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var two=await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("real pipeline",one.GetProperty("value").GetString());
        Assert.NotEqual(one.GetProperty("id").GetString(),two.GetProperty("id").GetString());
        using var missing=await client.GetAsync("/absent"); Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
        accessor.HttpContext=null;
    }
    [Fact]
    public void Staff_state_round_trip_uses_cookie_claims_without_server_session()
    {
        var original = new StaffCookieState(null);
        original.SetInt32(SessionKeys.EmployeeId,27);
        original.SetString(SessionKeys.EmployeeRoleCode,"CASHIER");
        original.SetInt32(SessionKeys.EmployeeBranchId,4);
        var restored = new StaffCookieState(original.Principal("version"));
        Assert.Equal(27,restored.GetInt32(SessionKeys.EmployeeId));
        Assert.Equal("CASHIER",restored.GetString(SessionKeys.EmployeeRoleCode));
        Assert.Equal(4,restored.GetInt32(SessionKeys.EmployeeBranchId));
        Assert.False(restored.Changed);
        restored.Clear(); Assert.True(restored.Changed); Assert.Empty(restored.Keys);
    }
    public sealed record Payload(string Value);
    public sealed class ScopeMarker { public string Id { get; }=Guid.NewGuid().ToString("N"); }
    private sealed class TestHubCaller : HubCallerContext
    {
        public TestHubCaller(HttpContext http) => Features.Set<IHttpContextFeature>(new TestHttpFeature { HttpContext=http });
        public override string ConnectionId=>"test";
        public override string? UserIdentifier=>null;
        public override ClaimsPrincipal? User=>null;
        public override IDictionary<object,object?> Items { get; }=new Dictionary<object,object?>();
        public override IFeatureCollection Features { get; }=new FeatureCollection();
        public override CancellationToken ConnectionAborted=>CancellationToken.None;
        public override void Abort() { }
    }
    private sealed class TestHttpFeature : IHttpContextFeature { public HttpContext? HttpContext { get; set; } }
    private sealed class TestGroups : IGroupManager
    {
        public List<string> Joined { get; }=[];
        public Task AddToGroupAsync(string id,string name,CancellationToken ct=default) { Joined.Add(name);return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string id,string name,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
