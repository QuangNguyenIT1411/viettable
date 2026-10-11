using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SelfRestaurant.CloudCompatibility;

internal static class DatabaseProvider
{
    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string owner)
    {
        using var optionsTiming = CloudHosting.Measure(owner, "database-options-no-io");
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connection = configuration.GetConnectionString(owner + "Db");
        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            // Never fall back to the local SQL Server string for a cloud provider.
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException($"Missing connection string: ConnectionStrings:{owner}Db");
            options.UseNpgsql(connection, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", owner.ToLowerInvariant()));
        }
        else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            connection ??= configuration.GetConnectionString("RestaurantDb");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException($"Missing connection string: ConnectionStrings:{owner}Db");
            options.UseSqlServer(connection);
        }
        else
            throw new InvalidOperationException("Database:Provider must be SqlServer or Postgres.");
    }
}
