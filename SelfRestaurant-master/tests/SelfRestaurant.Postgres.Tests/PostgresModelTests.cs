using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

public sealed class PostgresModelTests
{
    internal static DbContext Context(string owner, string connection) => owner switch
    {
        "catalog" => new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
        "orders" => new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
        "customers" => new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
        "identity" => new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
        "billing" => new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
        _ => throw new ArgumentException(nameof(owner))
    };

    [Theory]
    [InlineData("catalog")]
    [InlineData("orders")]
    [InlineData("customers")]
    [InlineData("identity")]
    [InlineData("billing")]
    public void Model_has_only_owned_schema_and_no_sql_server_dialect(string owner)
    {
        using var db = Context(owner, "Host=localhost;Database=design");
        Assert.Equal(owner, db.Model.GetDefaultSchema());
        foreach (var entity in db.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is not null) Assert.Equal(owner, entity.GetSchema());
            if (entity.GetViewName() is not null) Assert.Equal(owner, entity.GetViewSchema());
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(DateTime)) Assert.Equal("timestamp with time zone", property.GetColumnType());
                if (type == typeof(DateOnly)) Assert.Equal("date", property.GetColumnType());
            }
        }
        var sql = db.Database.GenerateCreateScript();
        Assert.DoesNotContain("nvarchar", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("datetime2", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("getdate()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(db.Database.GetMigrations());
    }

    [Fact]
    public void Instant_converter_preserves_utc_and_rejects_unspecified_kind()
    {
        using var db = Context("billing", "Host=localhost;Database=design");
        var converter = db.Model.FindEntityType(typeof(SelfRestaurant.Billing.Api.Persistence.Entities.CheckoutQuote))!
            .FindProperty("CreatedAtUtc")!.GetValueConverter()!;
        var utc = new DateTime(2026, 10, 5, 1, 2, 3, DateTimeKind.Utc);
        Assert.Equal(utc, converter.ConvertToProvider(utc));
        Assert.Equal(utc, converter.ConvertToProvider(utc.ToLocalTime()));
        Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void SqlServer_keeps_native_rowversion_and_legacy_schema()
    {
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost;Database=design;Integrated Security=true").Options);
        var batch = db.Model.FindEntityType(typeof(SelfRestaurant.Catalog.Api.Persistence.Entities.IngredientBatches))!;
        var token = batch.FindProperty("RowVersion")!;
        Assert.Equal("rowversion", token.GetColumnType());
        Assert.True(token.IsConcurrencyToken);
        Assert.Null(db.Model.GetDefaultSchema());
    }
}
