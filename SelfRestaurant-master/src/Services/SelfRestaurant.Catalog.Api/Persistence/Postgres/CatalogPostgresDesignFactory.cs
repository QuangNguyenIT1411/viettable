using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfRestaurant.Catalog.Api.Persistence;

// Design-time only: scaffolding does not connect to this database. Runtime always
// resolves the owning service's connection string through DatabaseProvider.
public sealed class CatalogPostgresDesignFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=viettable_design", postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"))
            .Options);
}
