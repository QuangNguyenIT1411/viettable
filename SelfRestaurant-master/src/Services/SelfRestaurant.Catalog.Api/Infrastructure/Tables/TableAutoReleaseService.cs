using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api.Infrastructure.Tables;

public sealed class TableAutoReleaseService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TableAutoReleaseService> _logger;
    private readonly TableAutoReleaseOptions _options;

    public TableAutoReleaseService(
        IServiceScopeFactory scopeFactory,
        IOptions<TableAutoReleaseOptions> options,
        ILogger<TableAutoReleaseService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredIdleTablesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-release idle tables.");
            }

            var loopDelay = _options.CheckIntervalSeconds > 0 ? _options.CheckIntervalSeconds : 60;
            await Task.Delay(TimeSpan.FromSeconds(loopDelay), stoppingToken);
        }
    }

    private async Task ReleaseExpiredIdleTablesAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var timeoutMinutes = _options.IdleTimeoutMinutes > 0 ? _options.IdleTimeoutMinutes : 15;
        var cutoff = DateTime.Now.AddMinutes(-timeoutMinutes);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var availableStatusId = await db.TableStatus
            .Where(x => x.StatusCode == "AVAILABLE")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        var occupiedStatusId = await db.TableStatus
            .Where(x => x.StatusCode == "OCCUPIED")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (!availableStatusId.HasValue || !occupiedStatusId.HasValue)
        {
            return;
        }

        // "No order placed yet" is represented by occupied tables with null CurrentOrderID.
        var tables = await db.DiningTables
            .Where(x =>
                (x.IsActive ?? true) &&
                x.StatusID == occupiedStatusId.Value &&
                x.CurrentOrderID == null &&
                x.UpdatedAt != null &&
                x.UpdatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (tables.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var table in tables)
        {
            table.StatusID = availableStatusId.Value;
            table.CurrentOrderID = null;
            table.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Auto released {Count} idle table(s) after {TimeoutMinutes} minutes.", tables.Count, timeoutMinutes);
    }
}

public sealed class TableAutoReleaseOptions
{
    public bool Enabled { get; set; } = true;
    public int IdleTimeoutMinutes { get; set; } = 15;
    public int CheckIntervalSeconds { get; set; } = 60;
}
