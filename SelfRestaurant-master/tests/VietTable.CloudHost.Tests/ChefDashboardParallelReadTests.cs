using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SelfRestaurant.Gateway.Api.Controllers;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class ChefDashboardParallelReadTests
{
    [Fact]
    public async Task Independent_dashboard_reads_all_start_before_any_completes()
    {
        using var handler = new HeldReads();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://fixture.invalid") };
        var controller = new StaffChefGatewayController(new OrdersClient(client), new CatalogClient(client),
            null!, null!, null!, NullLogger<StaffChefGatewayController>.Instance, null!);
        var staff = new StaffSessionUserDto(1, "fixture", "Fixture", null, null, 1, "CHEF", "Chef", 7, "Branch");
        var method = typeof(StaffChefGatewayController).GetMethod("BuildDashboardAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task<ChefDashboardDto>)method.Invoke(controller, [staff, 20, CancellationToken.None])!;
        try
        {
            await handler.AllStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(task.IsCompleted);
            Assert.Equal(4, handler.Paths.Count);
            Assert.Contains(handler.Paths, path => path.StartsWith("/api/branches/7/chef/orders"));
            Assert.Contains(handler.Paths, path => path.StartsWith("/api/branches/7/chef/history"));
        }
        finally { handler.Release.TrySetResult(); }
        var result = await task;
        Assert.Equal(7, result.Staff.BranchId);
        Assert.Empty(result.PendingOrders);
        Assert.Empty(result.History);
        Assert.Empty(result.Menu.Dishes);
        Assert.Empty(result.Ingredients);
    }

    private sealed class HeldReads : HttpMessageHandler
    {
        public ConcurrentBag<string> Paths { get; } = new();
        public TaskCompletionSource AllStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            if (Paths.Count == 4) AllStarted.TrySetResult();
            await Release.Task.WaitAsync(ct);
            var body = request.RequestUri.AbsolutePath.Contains("/chef/") ? "[]" : "null";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
