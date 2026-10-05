using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfRestaurant.Customers.Api.Persistence;

// Design-time only: scaffolding does not connect to this database. Runtime always
// resolves the owning service's connection string through DatabaseProvider.
public sealed class CustomersPostgresDesignFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql("Host=localhost;Database=viettable_design", postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "customers"))
            .Options);
}
