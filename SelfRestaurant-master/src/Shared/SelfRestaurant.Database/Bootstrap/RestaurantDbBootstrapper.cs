using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Database.Entities;

namespace SelfRestaurant.Database;

public static class RestaurantDbBootstrapper
{
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await SeedReferenceDataAsync(db, logger, cancellationToken);
    }

    private static async Task WaitForDatabaseAsync(RestaurantDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT 1";
                    command.CommandType = CommandType.Text;
                    _ = await command.ExecuteScalarAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        await db.Database.CloseConnectionAsync();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return;
            }
            catch (Exception ex) when (attempt < 60)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/60). Waiting {Delay}...", attempt, delay);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
            }
        }
    }

    private static async Task SeedReferenceDataAsync(RestaurantDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "OrderStatus", cancellationToken) || !await TableExistsAsync(db, "TableStatus", cancellationToken))
        {
            logger.LogWarning("Missing expected tables; skipping seed.");
            return;
        }

        var changed = false;

        if (!await db.OrderStatus.AnyAsync(cancellationToken))
        {
            db.OrderStatus.AddRange(
                new OrderStatus { StatusCode = "PENDING", StatusName = "Chờ xác nhận" },
                new OrderStatus { StatusCode = "CONFIRMED", StatusName = "Đã xác nhận" },
                new OrderStatus { StatusCode = "PREPARING", StatusName = "Đang chuẩn bị" },
                new OrderStatus { StatusCode = "READY", StatusName = "Sẵn sàng" },
                new OrderStatus { StatusCode = "SERVING", StatusName = "Đang phục vụ" },
                new OrderStatus { StatusCode = "COMPLETED", StatusName = "Hoàn tất" },
                new OrderStatus { StatusCode = "CANCELLED", StatusName = "Đã huỷ" });
            changed = true;
        }

        if (!await db.TableStatus.AnyAsync(cancellationToken))
        {
            db.TableStatus.AddRange(
                new TableStatus { StatusCode = "AVAILABLE", StatusName = "Trống" },
                new TableStatus { StatusCode = "OCCUPIED", StatusName = "Đang dùng" },
                new TableStatus { StatusCode = "RESERVED", StatusName = "Đặt trước" },
                new TableStatus { StatusCode = "INACTIVE", StatusName = "Không hoạt động" });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(RestaurantDbContext db, string tableName, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                                 SELECT 1
                                 FROM
                                 (
                                     SELECT name
                                     FROM sys.tables
                                     WHERE schema_id = SCHEMA_ID('dbo')

                                     UNION ALL

                                     SELECT name
                                     FROM sys.views
                                     WHERE schema_id = SCHEMA_ID('dbo')

                                     UNION ALL

                                     SELECT name
                                     FROM sys.synonyms
                                     WHERE schema_id = SCHEMA_ID('dbo')
                                 ) AS objects
                                 WHERE name = @table
                                 """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@table";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
