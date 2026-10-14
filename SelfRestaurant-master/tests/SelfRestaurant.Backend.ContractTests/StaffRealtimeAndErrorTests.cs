using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Gateway.Api.Controllers;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class StaffRealtimeAndErrorTests
{
    // ---- Server-wide error handling -------------------------------------------------------------

    [Fact]
    public async Task Unhandled_staff_api_error_returns_vietnamese_json_instead_of_an_empty_body()
    {
        await using var app = await Start(_ => throw new InvalidOperationException("Npgsql: host=db password=secret"));
        var response = await app.GetTestClient().GetAsync("/api/gateway/staff/chef/dashboard?token=abc");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("server_error", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Máy chủ đang gặp sự cố (mã 500). Vui lòng thử lại sau ít phút.", body.RootElement.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("correlationId").GetString()));
        Assert.DoesNotContain("secret", body.RootElement.GetRawText());
    }

    [Fact]
    public async Task Timeouts_become_504_and_the_mobile_api_keeps_its_503_contract()
    {
        await using var app = await Start(_ => throw new TaskCanceledException("HttpClient.Timeout"));
        var web = await app.GetTestClient().GetAsync("/api/gateway/staff/cashier/dashboard");
        Assert.Equal(HttpStatusCode.GatewayTimeout, web.StatusCode);
        Assert.Contains("phản hồi quá lâu", await web.Content.ReadAsStringAsync());

        var mobile = await app.GetTestClient().GetAsync("/api/mobile/v1/orders");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mobile.StatusCode);
        using var body = JsonDocument.Parse(await mobile.Content.ReadAsStringAsync());
        Assert.Equal("service_unavailable", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Error_log_has_type_and_masked_path_but_no_message_or_query()
    {
        var log = new ListLoggerProvider();
        await using var app = await Start(_ => throw new InvalidOperationException("password=secret"), log);
        await app.GetTestClient().GetAsync("/api/gateway/staff/chef/orders/123/start?token=abc");

        var line = Assert.Single(log.Lines, l => l.StartsWith("ApiError "));
        Assert.Contains("type=InvalidOperationException", line);
        Assert.Contains("path=/api/gateway/staff/chef/orders/{id}/start", line);
        Assert.Contains("status=500", line);
        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("token", line);
    }

    [Fact]
    public void Safe_path_masks_ids_and_long_codes()
    {
        Assert.Equal("/api/x/{id}/y/{value}", ApiErrorHandling.SafePath("/api/x/42/y/" + new string('a', 40)));
    }

    private static async Task<WebApplication> Start(RequestDelegate failing, ListLoggerProvider? log = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (log is not null) builder.Logging.AddProvider(log);
        var app = builder.Build();
        app.UseApiErrorHandling();
        app.Run(failing);
        await app.StartAsync();
        return app;
    }

    // ---- Staff branch realtime events ------------------------------------------------------------

    [Fact]
    public async Task Successful_chef_action_nudges_only_its_branch_with_topics_and_no_order_data()
    {
        var hub = new RecordingHub();
        var http = Http(hub, employeeId: 7, branchId: 2);
        await Run(new StaffBranchEventAttribute(StaffEventSource.Staff, StaffBranchEvents.Orders), http, new OkObjectResult(new { success = true }));

        var sent = Assert.Single(hub.Sent);
        Assert.Equal("branch:2", sent.Group);
        Assert.Equal("staffBranchChanged", sent.Method);
        var payload = JsonSerializer.SerializeToElement(sent.Args[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2, payload.GetProperty("branchId").GetInt32());
        Assert.Equal(new[] { "orders" }, payload.GetProperty("topics").EnumerateArray().Select(x => x.GetString() ?? "").ToArray());
        Assert.Equal(2, payload.EnumerateObject().Count());
    }

    [Fact]
    public async Task Failed_actions_missing_identity_and_hub_errors_send_nothing_and_never_throw()
    {
        var hub = new RecordingHub();
        await Run(new StaffBranchEventAttribute(StaffEventSource.Staff, StaffBranchEvents.Orders), Http(hub, 7, 2),
            new ObjectResult(new { message = "x" }) { StatusCode = 409 });
        await Run(new StaffBranchEventAttribute(StaffEventSource.Staff, StaffBranchEvents.Orders), Http(hub, null, null), new OkResult());
        Assert.Empty(hub.Sent);

        var broken = new RecordingHub { Fail = true };
        await Run(new StaffBranchEventAttribute(StaffEventSource.Staff, StaffBranchEvents.ServiceRequests), Http(broken, 7, 2), new OkResult());
    }

    [Theory]
    [InlineData(typeof(MobileDiningOperationsController), nameof(MobileDiningOperationsController.CreateOrder), "orders")]
    [InlineData(typeof(MobileDiningOperationsController), nameof(MobileDiningOperationsController.CreateServiceRequest), "serviceRequests")]
    [InlineData(typeof(MobileDiningOperationsController), nameof(MobileDiningOperationsController.CancelServiceRequest), "serviceRequests")]
    [InlineData(typeof(MobileBillingGatewayController), nameof(MobileBillingGatewayController.RequestPayment), "onsitePayments")]
    [InlineData(typeof(MobileCheckInsGatewayController), nameof(MobileCheckInsGatewayController.CancelPaymentRequest), "onsitePayments")]
    [InlineData(typeof(CustomerGatewayController), nameof(CustomerGatewayController.SubmitMenuOrder), "orders")]
    [InlineData(typeof(StaffChefGatewayController), nameof(StaffChefGatewayController.ReadyItem), "orders")]
    [InlineData(typeof(StaffChefGatewayController), nameof(StaffChefGatewayController.Cancel), "orders")]
    [InlineData(typeof(StaffCashierGatewayController), nameof(StaffCashierGatewayController.ConfirmMobileOnsitePayment), "onsitePayments")]
    [InlineData(typeof(StaffCashierGatewayController), nameof(StaffCashierGatewayController.CompleteServiceRequest), "serviceRequests")]
    [InlineData(typeof(StaffCashierGatewayController), nameof(StaffCashierGatewayController.Checkout), "orders")]
    public void Changes_that_staff_screens_show_publish_a_branch_event(Type controller, string action, string topic)
    {
        var attributes = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == action).SelectMany(m => m.GetCustomAttributes<StaffBranchEventAttribute>()).ToArray();
        Assert.Contains(attributes, a => a.Topics.Contains(topic));
    }

    private static async Task Run(StaffBranchEventAttribute filter, HttpContext http, IActionResult result)
    {
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(action, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        await filter.OnActionExecutionAsync(executing, () =>
            Task.FromResult(new ActionExecutedContext(action, new List<IFilterMetadata>(), new object()) { Result = result }));
    }

    private static HttpContext Http(RecordingHub hub, int? employeeId, int? branchId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHubContext<CustomerNotificationsHub>>(hub);
        services.AddSingleton(new OrdersClient(new HttpClient { BaseAddress = new Uri("http://orders.invalid") }));
        services.AddScoped<StaffBranchEvents>();
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var state = new MemorySession();
        if (employeeId is int e) state.SetInt32(SessionKeys.EmployeeId, e);
        if (branchId is int b) state.SetInt32(SessionKeys.EmployeeBranchId, b);
        http.Items[StaffIdentityState.ItemKey] = state;
        return http;
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }

    private sealed record Sent(string Group, string Method, object?[] Args);

    private sealed class RecordingHub : IHubContext<CustomerNotificationsHub>
    {
        public List<Sent> Sent { get; } = new();
        public bool Fail { get; init; }
        public IHubClients Clients => new HubClients(this);
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class HubClients(RecordingHub owner) : IHubClients
    {
        public IClientProxy Group(string groupName) => new Proxy(owner, groupName);
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class Proxy(RecordingHub owner, string group) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (owner.Fail) throw new InvalidOperationException("broker down");
            owner.Sent.Add(new Sent(group, method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider, ILogger
    {
        public List<string> Lines { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines) Lines.Add(formatter(state, exception) + (exception is null ? "" : " EXCEPTION:" + exception.Message));
        }
    }
}
