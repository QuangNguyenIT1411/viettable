using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfRestaurant.Identity.Api.Persistence;

// Design-time only: scaffolding does not connect to this database. Runtime always
// resolves the owning service's connection string through DatabaseProvider.
public sealed class IdentityPostgresDesignFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=viettable_design", postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options);
}
