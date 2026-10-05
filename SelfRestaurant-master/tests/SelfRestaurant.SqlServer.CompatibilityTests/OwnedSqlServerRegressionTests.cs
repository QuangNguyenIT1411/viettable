using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using Xunit;

namespace SelfRestaurant.SqlServer.CompatibilityTests;

// Explicit opt-in integration project. No migrations/seed/reset/row deletion.
public sealed class OwnedSqlServerRegressionTests
{
    private static string Connection(string owner) => Environment.GetEnvironmentVariable(
        "VIETTABLE_SQLSERVER_" + owner.ToUpperInvariant() + "_CONNECTION")
        ?? throw new InvalidOperationException("Owning SQL Server test connection must be supplied through environment.");

    [Theory]
    [InlineData("Catalog")][InlineData("Orders")][InlineData("Customers")][InlineData("Identity")][InlineData("Billing")]
    public async Task Current_compiled_owned_models_read_existing_sql_server(string owner)
    {
        var connection = Connection(owner);
        await using DbContext db = owner switch {
            "Catalog" => new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connection).Options),
            "Orders" => new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlServer(connection).Options),
            "Customers" => new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>().UseSqlServer(connection).Options),
            "Identity" => new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).Options),
            "Billing" => new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(connection).Options),
            _ => throw new ArgumentOutOfRangeException(nameof(owner)) };
        Assert.True(db.Database.IsSqlServer());
        Assert.True(await db.Database.CanConnectAsync());
        // Billing already owns SQL Server billing.Bills; this is not a PG override.
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.GetSchema() == owner.ToLowerInvariant()
            && !(owner == "Billing" && entity.GetTableName() == "Bills"));
        // Materialize real data without printing profiles/tokens/financial records.
        switch (db) {
            case CatalogDbContext catalog: _ = await catalog.Dishes.AsNoTracking().Take(1).ToListAsync(); break;
            case OrdersDbContext orders: _ = await orders.Orders.AsNoTracking().Take(1).ToListAsync(); break;
            case CustomersDbContext customers: _ = await customers.Reservations.AsNoTracking().Take(1).ToListAsync(); break;
            case IdentityDbContext identity: _ = await identity.Customers.AsNoTracking().Take(1).ToListAsync(); break;
            case BillingDbContext billing: _ = await billing.Invoices.AsNoTracking().Take(1).ToListAsync(); break;
        }
    }

    [Fact]
    public async Task Provider_neutral_stock_update_is_atomic_and_rolled_back()
    {
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(Connection("Catalog")).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var ingredient = await db.Ingredients.AsNoTracking().FirstAsync(x => x.CurrentStock >= 1m);
        var affected = await db.Ingredients.Where(x => x.IngredientID == ingredient.IngredientID && x.CurrentStock >= 1m)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CurrentStock, x => x.CurrentStock - 1m));
        Assert.Equal(1, affected);
        Assert.Equal(ingredient.CurrentStock - 1m, await db.Ingredients.Where(x => x.IngredientID == ingredient.IngredientID).Select(x => x.CurrentStock).SingleAsync());
        await tx.RollbackAsync();
        Assert.Equal(ingredient.CurrentStock, await db.Ingredients.Where(x => x.IngredientID == ingredient.IngredientID).Select(x => x.CurrentStock).SingleAsync());
    }

    [Fact]
    public async Task Payment_lock_keeps_sql_server_transaction_semantics()
    {
        await using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(Connection("Billing")).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        await PaymentFinalizationService.LockQuoteAsync(db, int.MaxValue, CancellationToken.None);
        await tx.RollbackAsync(); // Lock only; no payment/quote/invoice fixture.
    }
}
