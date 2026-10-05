using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfRestaurant.Orders.Api.Persistence;

// Design-time only: scaffolding does not connect to this database. Runtime always
// resolves the owning service's connection string through DatabaseProvider.
public sealed class OrdersPostgresDesignFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql("Host=localhost;Database=viettable_design", postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "orders"))
            .Options);
}
