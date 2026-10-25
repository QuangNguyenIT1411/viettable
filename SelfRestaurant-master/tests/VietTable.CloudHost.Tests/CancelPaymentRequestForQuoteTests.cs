using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Orders.Api.Controllers;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Cashier cancels a waiting payment (25/10): Billing unlocks the session in Orders for EXACTLY that quote, so the
/// guest can request payment again (e.g. cash) and the session is never left stuck at PAYMENT_REQUESTED.
/// </summary>
public sealed class CancelPaymentRequestForQuoteTests
{
    private const int Owner = 11;

    [Fact]
    public async Task Unlocks_a_session_waiting_for_that_quote_and_a_retry_is_a_no_op()
    {
        await using var db = NewDb();
        db.DiningSessions.Add(Session(1, "PAYMENT_REQUESTED", quoteId: 7));
        await db.SaveChangesAsync();

        var result = await Controller(db).CancelPaymentRequest(1, new(Owner, 7), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var stored = await db.DiningSessions.SingleAsync();
        Assert.Equal("ACTIVE", stored.Status);
        Assert.Null(stored.CheckoutQuoteId);
        Assert.Null(stored.PaymentRequestedAtUtc);
        // Billing retries after a failed commit: the session is already unlocked, that is success.
        Assert.IsType<OkObjectResult>((await Controller(db).CancelPaymentRequest(1, new(Owner, 7), CancellationToken.None)).Result);
        Assert.Equal("ACTIVE", (await db.DiningSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Never_unlocks_a_session_waiting_for_another_quote()
    {
        await using var db = NewDb();
        db.DiningSessions.Add(Session(1, "PAYMENT_REQUESTED", quoteId: 8));
        await db.SaveChangesAsync();

        var result = await Controller(db).CancelPaymentRequest(1, new(Owner, 7), CancellationToken.None);

        AssertRejected(result, 409, "payment_request_conflict");
        var stored = await db.DiningSessions.SingleAsync();
        Assert.Equal("PAYMENT_REQUESTED", stored.Status);
        Assert.Equal(8, stored.CheckoutQuoteId);
    }

    [Fact]
    public async Task Closed_session_and_other_customer_are_refused()
    {
        await using var db = NewDb();
        var closed = Session(1, "CLOSED", quoteId: 7);
        closed.EndedAtUtc = DateTime.UtcNow;
        db.DiningSessions.AddRange(closed, Session(2, "PAYMENT_REQUESTED", quoteId: 9));
        await db.SaveChangesAsync();

        AssertRejected(await Controller(db).CancelPaymentRequest(1, new(Owner, 7), CancellationToken.None), 409, "session_closed");
        AssertRejected(await Controller(db).CancelPaymentRequest(2, new(Owner + 1, 9), CancellationToken.None), 403, "forbidden");
        Assert.Equal("PAYMENT_REQUESTED", (await db.DiningSessions.SingleAsync(x => x.DiningSessionId == 2)).Status);
    }

    [Fact]
    public async Task Without_a_quote_the_existing_unlock_is_unchanged()
    {
        await using var db = NewDb();
        db.DiningSessions.Add(Session(1, "PAYMENT_REQUESTED", quoteId: 8));
        await db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>((await Controller(db).CancelPaymentRequest(1, new(Owner), CancellationToken.None)).Result);
        Assert.Equal("ACTIVE", (await db.DiningSessions.SingleAsync()).Status);
    }

    private static DiningSession Session(int id, string status, int? quoteId) => new()
    {
        DiningSessionId = id, SessionCode = "DS-PAY-" + id, CustomerId = Owner, BranchId = 3, TableId = 37,
        Status = status, CheckoutQuoteId = quoteId, PaymentRequestedAtUtc = quoteId is null ? null : DateTime.UtcNow,
        CheckInIdempotencyKey = "checkin-" + id, RequestHash = new string('b', 64),
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

    private static OrdersDbContext NewDb() => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    // The unlock touches only the session row; Catalog and the other dependencies are not used.
    private static DiningSessionsController Controller(OrdersDbContext db) => new(db, null!, null!, null!);
}
