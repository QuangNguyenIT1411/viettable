using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class BillingOwnershipTests
{
    [Fact]
    public void Bills_map_to_owned_schema_not_legacy_synonym()
    {
        using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=unused;Database=RESTAURANT_BILLING;Integrated Security=true").Options);
        var bill = db.Model.FindEntityType(typeof(Bills))!;
        Assert.Equal("billing", bill.GetSchema());
        Assert.Equal("Bills", bill.GetTableName());
        Assert.Empty(bill.GetForeignKeys());
        Assert.Equal("Invoices", db.Model.FindEntityType(typeof(Invoice))!.GetTableName());
    }

    [Fact]
    public void Embedded_owned_schema_has_no_external_database_or_alias_creation()
    {
        using var stream = typeof(BillingDbContext).Assembly.GetManifestResourceStream("Billing.OwnedTables.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();
        Assert.Contains("CREATE TABLE billing.Bills", sql);
        Assert.DoesNotContain("RESTAURANT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYNONYM", sql, StringComparison.OrdinalIgnoreCase);
    }
}
