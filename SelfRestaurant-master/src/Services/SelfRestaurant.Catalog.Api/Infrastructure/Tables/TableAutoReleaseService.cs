using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api.Infrastructure.Tables;

public sealed class TableAutoReleaseService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TableAutoReleaseService> _logger;
    private readonly TableAutoReleaseOptions _options;
    private readonly OrdersActiveTablesClient _orders;

    public TableAutoReleaseService(
        IServiceScopeFactory scopeFactory,
        IOptions<TableAutoReleaseOptions> options,
        OrdersActiveTablesClient orders,
        ILogger<TableAutoReleaseService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _orders = orders;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-release idle tables.");
            }

            var loopDelay = _options.CheckIntervalSeconds > 0 ? _options.CheckIntervalSeconds : 60;
            await Task.Delay(TimeSpan.FromSeconds(loopDelay), stoppingToken);
        }
    }

    // Bounded recovery entrypoint; CloudHost invokes it on demand, never as a hosted loop.
    public async Task RunOnceAsync(CancellationToken cancellationToken)
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

        // Never release a checked-in customer just because the first order has not
        // been placed yet. If Orders is unavailable, fail closed and retry later.
        var activeTableIds = await _orders.GetAsync(cancellationToken);
        // "No order placed yet" is represented by occupied tables with null CurrentOrderID.
        var tables = await db.DiningTables
            .Where(x =>
                (x.IsActive ?? true) &&
                x.StatusID == occupiedStatusId.Value &&
                x.CurrentOrderID == null &&
                x.UpdatedAt != null &&
                x.UpdatedAt <= cutoff &&
                !activeTableIds.Contains(x.TableID))
            .OrderBy(x => x.UpdatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (tables.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        var released = 0;
        foreach (var table in tables)
        {
            var observedAt = table.UpdatedAt;
            released += await db.DiningTables
                .Where(x => x.TableID == table.TableID && x.StatusID == occupiedStatusId.Value
                    && x.CurrentOrderID == null && x.UpdatedAt == observedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.StatusID, availableStatusId.Value)
                    .SetProperty(x => x.CurrentOrderID, (int?)null)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        }

        if (released > 0)
            _logger.LogInformation("Auto released {Count} eligible idle tables after {TimeoutMinutes} minutes.", released, timeoutMinutes);
    }
}

public sealed class TableAutoReleaseOptions
{
    public bool Enabled { get; set; } = true;
    public int IdleTimeoutMinutes { get; set; } = 15;
    public int CheckIntervalSeconds { get; set; } = 60;
}
