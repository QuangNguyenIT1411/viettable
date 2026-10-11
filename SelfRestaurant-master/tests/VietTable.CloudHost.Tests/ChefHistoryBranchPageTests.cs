using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SelfRestaurant.Orders.Api.Controllers;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class ChefHistoryBranchPageTests
{
    [Fact]
    public async Task Other_branch_recent_orders_do_not_consume_chef_history_page()
    {
        await using var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        db.CatalogTableSnapshots.Add(new CatalogTableSnapshots { TableId = 7, BranchId = 3,
            TableNumber = 2, IsActive = true, RefreshedAtUtc = now });
        db.CatalogBranchSnapshots.Add(new CatalogBranchSnapshots { BranchId = 3, Name = "Fixture branch", RefreshedAtUtc = now });
        var status = new OrderStatus { StatusID = 1, StatusCode = "COMPLETED", StatusName = "Completed" };
        db.Orders.AddRange(
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 1, TableID = 7,
                OrderTime = now.AddMinutes(-5), CompletedTime = now.AddMinutes(-4), Status = status },
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 2, TableID = 99,
                OrderTime = now, CompletedTime = now, Status = status });
        await db.SaveChangesAsync();
        using var http = new HttpClient(new NoRemoteReads());
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new OrdersController(db, new CatalogApiClient(http, cache, db),
            null!, null!, null!, null!, null!, null!, null!);

        var result = await controller.GetChefHistory(3, 1, CancellationToken.None);
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(1, payload.GetArrayLength());
        Assert.Equal(1, payload[0].GetProperty("OrderId").GetInt32());
        Assert.Equal("Bàn 2", payload[0].GetProperty("TableName").GetString());
        Assert.False(payload[0].TryGetProperty("BranchName", out _));
        var internalResult = await controller.GetInternalChefHistory(3, null, cancellationToken: CancellationToken.None);
        var internalPayload = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(internalResult.Result).Value);
        Assert.Equal("Fixture branch", internalPayload.GetProperty("items")[0].GetProperty("BranchName").GetString());
    }

    private sealed class NoRemoteReads : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Fresh branch/table snapshots need no remote fetch; history fixture has no items.");
    }
}
