using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfRestaurant.Billing.Api.Persistence;

// Design-time only: scaffolding does not connect to this database. Runtime always
// resolves the owning service's connection string through DatabaseProvider.
public sealed class BillingPostgresDesignFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=viettable_design", postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "billing"))
            .Options);
}
