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
    public async Task Active_queue_projection_preserves_empty_orders_item_order_and_branch_scope()
    {
        await using var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        db.CatalogTableSnapshots.Add(new CatalogTableSnapshots { TableId = 7, BranchId = 3,
            TableNumber = 2, IsActive = true, RefreshedAtUtc = now });
        db.CatalogDishSnapshots.Add(new CatalogDishSnapshots { DishId = 4, Name = "Fixture dish", RefreshedAtUtc = now });
        var status = new OrderStatus { StatusID = 1, StatusCode = "CONFIRMED", StatusName = "Confirmed" };
        db.Orders.AddRange(
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 1, TableID = 7, OrderTime = now, Status = status, IsActive = true },
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 2, TableID = 7, OrderTime = now.AddMinutes(1), Status = status, IsActive = true },
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 3, TableID = 99, OrderTime = now, Status = status, IsActive = true });
        db.OrderItems.AddRange(
            new OrderItems { ItemID = 9, OrderID = 1, DishID = 4, Quantity = 2, UnitPrice = 20, LineTotal = 40, StatusCode = "READY" },
            new OrderItems { ItemID = 2, OrderID = 1, DishID = 4, Quantity = 1, UnitPrice = 30, LineTotal = 30, StatusCode = "PREPARING" });
        await db.SaveChangesAsync();
        using var http = new HttpClient(new NoRemoteReads()) { BaseAddress = new Uri("http://fixture.invalid") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new OrdersController(db, new CatalogApiClient(http, cache, db),
            null!, null!, null!, null!, null!, null!, null!);
        var result = await controller.GetChefOrders(3, null, CancellationToken.None);
        var rows = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal(1, rows[0].GetProperty("orderId").GetInt32());
        Assert.Equal("Bàn 2", rows[0].GetProperty("tableName").GetString());
        var items = rows[0].GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(2, items[0].GetProperty("itemId").GetInt32());
        Assert.Equal("PREPARING", items[0].GetProperty("statusCode").GetString());
        Assert.Equal(9, items[1].GetProperty("itemId").GetInt32());
        Assert.Equal("READY", items[1].GetProperty("statusCode").GetString());
        Assert.Equal(0, rows[1].GetProperty("items").GetArrayLength());
        var cashier = await controller.GetCashierOrdersInternal(3, CancellationToken.None);
        var queue = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(cashier.Result).Value);
        Assert.Equal(2, queue.GetArrayLength());
        var paidItems = queue[0].GetProperty("items");
        Assert.Equal(2, paidItems.GetArrayLength());
        Assert.Equal(2, paidItems[0].GetProperty("itemId").GetInt32());
        Assert.Equal(70, queue[0].GetProperty("subtotal").GetDecimal());
        Assert.Equal(3, queue[0].GetProperty("itemCount").GetInt32());
        Assert.Equal(0, queue[1].GetProperty("items").GetArrayLength());
    }

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

    [Fact]
    public async Task History_projection_keeps_empty_orders_and_filters_chef_items_after_order_page()
    {
        await using var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        db.CatalogTableSnapshots.Add(new CatalogTableSnapshots { TableId = 7, BranchId = 3,
            TableNumber = 2, IsActive = true, RefreshedAtUtc = now });
        db.CatalogBranchSnapshots.Add(new CatalogBranchSnapshots { BranchId = 3, Name = "Fixture", RefreshedAtUtc = now });
        db.CatalogDishSnapshots.Add(new CatalogDishSnapshots { DishId = 4, Name = "Fixture dish", RefreshedAtUtc = now });
        var status = new OrderStatus { StatusID = 1, StatusCode = "COMPLETED", StatusName = "Completed" };
        db.Orders.AddRange(
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 1, TableID = 7, OrderTime = now, Status = status },
            new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 2, TableID = 7, OrderTime = now.AddMinutes(-1), Status = status });
        db.OrderItems.AddRange(
            new OrderItems { ItemID = 1, OrderID = 1, DishID = 4, Quantity = 2, ChefId = 11, Note = "fixture note" },
            new OrderItems { ItemID = 2, OrderID = 1, DishID = 4, Quantity = 1, ChefId = 12 });
        await db.SaveChangesAsync();
        using var http = new HttpClient(new NoRemoteReads()) { BaseAddress = new Uri("http://fixture.invalid") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new OrdersController(db, new CatalogApiClient(http, cache, db),
            null!, null!, null!, null!, null!, null!, null!);
        var result = await controller.GetChefHistory(3, 2, CancellationToken.None);
        var rows = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Contains("2x Fixture dish", rows[0].GetProperty("DishesSummary").GetString());
        Assert.Contains("1x Fixture dish", rows[0].GetProperty("DishesSummary").GetString());
        Assert.Equal("", rows[1].GetProperty("DishesSummary").GetString());
        var filtered = await controller.GetInternalChefHistory(3, 11, cancellationToken: CancellationToken.None);
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(filtered.Result).Value);
        Assert.Equal(1, payload.GetProperty("items").GetArrayLength());
        Assert.Equal("2x Fixture dish", payload.GetProperty("items")[0].GetProperty("DishesSummary").GetString());
    }

    private sealed class NoRemoteReads : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Fresh branch/table snapshots need no remote fetch; history fixture has no items.");
    }
}
