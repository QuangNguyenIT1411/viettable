using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SelfRestaurant.CloudCompatibility;

internal static class PostgresBootstrap
{
    public static string SeedProfile(IConfiguration configuration)
    {
        var profile = configuration["Database:SeedProfile"] ?? "None";
        return profile is "None" or "Reference" or "Demo" ? profile
            : throw new InvalidOperationException("Database:SeedProfile must be None, Reference or Demo.");
    }
    public static async Task ValidateOrMigrateAsync(DbContext db, IConfiguration configuration, CancellationToken ct)
    {
        using var validationTiming = CloudHosting.Measure("Postgres", "schema-validation");
        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException("PostgreSQL baseline migration is missing; SQL Server bootstrap will not be executed.");
        if (configuration.GetValue<bool>("Database:ApplyMigrations"))
            await db.Database.MigrateAsync(ct);
        else
        {
            using var pendingTiming = CloudHosting.Measure("Postgres", "pending-migrations-io");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("PostgreSQL schema requires deployment migrations. Set Database:ApplyMigrations only on the migration job.");
        }
    }
}
