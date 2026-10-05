using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

// Execute only against the disposable developer database, never local SQL Server
// or a cloud database. The caller provides the connection through environment.
public sealed class PostgresDatabaseTests
{
    private static string Connection => Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Disposable PostgreSQL test connection is required.");

    [Theory]
    [InlineData("catalog")]
    [InlineData("orders")]
    [InlineData("customers")]
    [InlineData("identity")]
    [InlineData("billing")]
    [Trait("Category", "PostgresIntegration")]
    public async Task Baseline_applies_idempotently_and_context_can_connect(string owner)
    {
        await using var db = PostgresModelTests.Context(owner, Connection);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations().Count(), (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.True(await db.Database.CanConnectAsync());
        if (db is SelfRestaurant.Identity.Api.Persistence.IdentityDbContext identity)
            _ = await identity.CustomerLoyalty.CountAsync();
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Exact_money_utc_and_unique_idempotency_survive_round_trip()
    {
        await using var db = (BillingDbContext)PostgresModelTests.Context("billing", Connection);
        var marker = "PG-TEST-" + Guid.NewGuid().ToString("N");
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var quote = new CheckoutQuote { QuoteCode = marker, IdempotencyKey = marker,
            RequestHash = marker, DiningSessionCode = marker, ItemsJson = "[]", Status = "OPEN", Subtotal = 35000m,
            TotalAmount = 35000m, CreatedAtUtc = now, UpdatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(15) };
        db.CheckoutQuotes.Add(quote);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var read = await db.CheckoutQuotes.SingleAsync(x => x.IdempotencyKey == marker);
        Assert.Equal(35000m, read.TotalAmount);
        Assert.Equal(DateTimeKind.Utc, read.CreatedAtUtc.Kind);
        Assert.InRange(Math.Abs((read.CreatedAtUtc - now).TotalMilliseconds), 0, 1);
        db.CheckoutQuotes.Add(new CheckoutQuote { QuoteCode = marker + "2", IdempotencyKey = marker,
            RequestHash = marker, DiningSessionCode = marker, ItemsJson = "[]", Status = "OPEN", CreatedAtUtc = now, UpdatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(15) });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, ((Npgsql.PostgresException)error.GetBaseException()).SqlState);
        await tx.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Quote_lock_serializes_competing_transactions_and_releases_on_commit()
    {
        await using var first = (BillingDbContext)PostgresModelTests.Context("billing", Connection);
        await using var second = (BillingDbContext)PostgresModelTests.Context("billing", Connection);
        await using var tx1 = await first.Database.BeginTransactionAsync();
        await using var tx2 = await second.Database.BeginTransactionAsync();
        await PaymentFinalizationService.LockQuoteAsync(first, int.MaxValue, CancellationToken.None);
        var waiting = PaymentFinalizationService.LockQuoteAsync(second, int.MaxValue, CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        await tx1.CommitAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await tx2.CommitAsync();
    }
}
