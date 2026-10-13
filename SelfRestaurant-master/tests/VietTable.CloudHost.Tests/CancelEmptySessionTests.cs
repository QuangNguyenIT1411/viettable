using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Orders.Api.Controllers;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class CancelEmptySessionTests
{
    private const int Owner = 11;
    private const int TableId = 37;

    [Fact]
    public async Task Owner_cancels_empty_session_closes_it_as_cancelled_and_releases_table()
    {
        await using var db = NewDb();
        db.DiningSessions.Add(Session(1));
        db.ServiceRequests.Add(new ServiceRequest { DiningSessionId = 1, CustomerId = Owner, BranchId = 3, TableId = TableId,
            RequestType = "WATER", Status = "OPEN", IdempotencyKey = "sr-1", RequestHash = new string('a', 64),
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();

        var result = await Controller(db, catalog).CancelEmptySession(1, new(Owner), CancellationToken.None);

        var body = Assert.IsType<DiningSessionsController.DiningSessionResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("CANCELLED", body.Status);
        var stored = await db.DiningSessions.SingleAsync();
        Assert.Equal("CANCELLED", stored.Status);
        Assert.NotNull(stored.EndedAtUtc);
        Assert.Null(stored.PaymentId);
        Assert.Equal("CANCELLED", (await db.ServiceRequests.SingleAsync()).Status);
        Assert.Equal(new[] { TableId }, catalog.Released);

        // A retry is idempotent and re-releases only a table that no newer session uses.
        var retry = await Controller(db, catalog).CancelEmptySession(1, new(Owner), CancellationToken.None);
        Assert.IsType<OkObjectResult>(retry.Result);
        Assert.Equal(new[] { TableId, TableId }, catalog.Released);
    }

    [Fact]
    public async Task Rejects_non_owner_without_changes()
    {
        await using var db = NewDb();
        db.DiningSessions.Add(Session(1));
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();

        var result = await Controller(db, catalog).CancelEmptySession(1, new(Owner + 1), CancellationToken.None);

        AssertRejected(result, 403, "forbidden");
        await AssertUnchanged(db, catalog);
    }

    [Fact]
    public async Task Rejects_session_with_any_order_round()
    {
        await using var db = NewDb();
        var session = Session(1);
        db.DiningSessions.Add(session);
        db.Orders.Add(new SelfRestaurant.Orders.Api.Persistence.Entities.Orders { OrderID = 5, TableID = TableId,
            DiningSessionCode = session.SessionCode, OrderTime = DateTime.UtcNow, IsActive = true,
            Status = new OrderStatus { StatusID = 1, StatusCode = "CONFIRMED", StatusName = "Confirmed" } });
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();

        var result = await Controller(db, catalog).CancelEmptySession(1, new(Owner), CancellationToken.None);

        AssertRejected(result, 409, "session_has_orders");
        await AssertUnchanged(db, catalog);
    }

    [Fact]
    public async Task Rejects_session_with_converted_preorder()
    {
        await using var db = NewDb();
        var session = Session(1);
        session.ConvertedOrderId = 9;
        db.DiningSessions.Add(session);
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();

        AssertRejected(await Controller(db, catalog).CancelEmptySession(1, new(Owner), CancellationToken.None), 409, "session_has_orders");
        await AssertUnchanged(db, catalog);
    }

    [Fact]
    public async Task Rejects_session_waiting_for_payment()
    {
        await using var db = NewDb();
        var session = Session(1);
        session.Status = "PAYMENT_REQUESTED";
        db.DiningSessions.Add(session);
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();

        var result = await Controller(db, catalog).CancelEmptySession(1, new(Owner), CancellationToken.None);

        AssertRejected(result, 409, "session_payment_locked");
        var stored = await db.DiningSessions.SingleAsync();
        Assert.Equal("PAYMENT_REQUESTED", stored.Status);
        Assert.Null(stored.EndedAtUtc);
        Assert.Empty(catalog.Released);
    }

    [Fact]
    public async Task Rejects_reservation_session_and_closed_or_missing_sessions()
    {
        await using var db = NewDb();
        var reserved = Session(1);
        reserved.ReservationId = 4;
        var paid = Session(2);
        paid.Status = "CLOSED";
        paid.EndedAtUtc = DateTime.UtcNow;
        db.DiningSessions.AddRange(reserved, paid);
        await db.SaveChangesAsync();
        var catalog = new ReleaseRecorder();
        var controller = Controller(db, catalog);

        AssertRejected(await controller.CancelEmptySession(1, new(Owner), CancellationToken.None), 409, "session_from_reservation");
        AssertRejected(await controller.CancelEmptySession(2, new(Owner), CancellationToken.None), 409, "session_closed");
        AssertRejected(await controller.CancelEmptySession(99, new(Owner), CancellationToken.None), 404, "session_not_found");
        Assert.Equal("ACTIVE", (await db.DiningSessions.SingleAsync(x => x.DiningSessionId == 1)).Status);
        Assert.Equal("CLOSED", (await db.DiningSessions.SingleAsync(x => x.DiningSessionId == 2)).Status);
        Assert.Empty(catalog.Released);
    }

    private static OrdersDbContext NewDb() => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static DiningSessionsController Controller(OrdersDbContext db, ICatalogReadModel catalog) => new(db, catalog, null!, null!);

    private static DiningSession Session(int id) => new()
    {
        DiningSessionId = id, SessionCode = "DS-TEST-" + id, CustomerId = Owner, BranchId = 3, TableId = TableId,
        Status = "ACTIVE", CheckInIdempotencyKey = "checkin-" + id, RequestHash = new string('b', 64),
        StartedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static void AssertRejected(ActionResult<DiningSessionsController.DiningSessionResponse> result, int status, string code)
    {
        var error = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(status, error.StatusCode);
        var body = JsonSerializer.SerializeToElement(error.Value);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    private static async Task AssertUnchanged(OrdersDbContext db, ReleaseRecorder catalog)
    {
        var stored = await db.DiningSessions.SingleAsync();
        Assert.Equal("ACTIVE", stored.Status);
        Assert.Null(stored.EndedAtUtc);
        Assert.Empty(catalog.Released);
    }

    private sealed class ReleaseRecorder : ICatalogReadModel
    {
        public List<int> Released { get; } = new();
        public Task ReleaseTableAsync(int tableId, CancellationToken cancellationToken) { Released.Add(tableId); return Task.CompletedTask; }
        public Task<CatalogApiClient.TableSnapshotResponse?> GetTableAsync(int tableId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogApiClient.TableSnapshotResponse>?> GetTablesAsync(IEnumerable<int> tableIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>?> GetBranchTableIdsAsync(int branchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogApiClient.DishSnapshotResponse?> GetDishAsync(int dishId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogApiClient.DishSnapshotResponse>?> GetDishesAsync(IEnumerable<int> dishIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogApiClient.BranchSnapshotResponse>?> GetBranchesAsync(IEnumerable<int> branchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task OccupyTableAsync(int tableId, int? currentOrderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> ResetAllTablesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogApiClient.IngredientConsumptionResult> ConsumeIngredientsForOrderAsync(int orderId,
            IReadOnlyList<CatalogApiClient.OrderIngredientConsumptionItem> items, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogApiClient.IngredientConsumptionResult> ValidateIngredientsForOrderAsync(int orderId,
            IReadOnlyList<CatalogApiClient.OrderIngredientConsumptionItem> items, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
