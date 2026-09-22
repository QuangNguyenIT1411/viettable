using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;

namespace SelfRestaurant.Orders.Api.Infrastructure;

/// <summary>
/// Orders-local catalog read model. This class is the only place allowed to read or refresh
/// branch/table/dish snapshots inside Orders.Api. Controllers and background workers must depend
/// on ICatalogReadModel instead of touching snapshot DbSets directly.
/// </summary>
public sealed class CatalogApiClient : ICatalogReadModel
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly OrdersDbContext _db;
    private static readonly TimeSpan SnapshotCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BranchSnapshotFreshnessTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TableSnapshotFreshnessTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DishSnapshotFreshnessTtl = TimeSpan.FromMinutes(10);

    public CatalogApiClient(HttpClient http, IMemoryCache cache, OrdersDbContext db)
    {
        _http = http;
        _cache = cache;
        _db = db;
    }

    public Task<TableSnapshotResponse?> GetTableAsync(int tableId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"catalog:table:{tableId}",
            () => GetTableCoreAsync(tableId, cancellationToken));

    public Task<IReadOnlyList<TableSnapshotResponse>?> GetTablesAsync(IEnumerable<int> tableIds, CancellationToken cancellationToken)
    {
        var ids = tableIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<TableSnapshotResponse>?>(Array.Empty<TableSnapshotResponse>());
        }

        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        return GetOrCreateAsync(
            $"catalog:tables:{string.Join(",", ids)}",
            () => GetTablesCoreAsync(ids, query, cancellationToken));
    }

    public Task<IReadOnlyList<int>?> GetBranchTableIdsAsync(int branchId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"catalog:branch-table-ids:{branchId}",
            () => GetBranchTableIdsCoreAsync(branchId, cancellationToken));

    // Dish availability is business-critical for add-item/submit flows, so these reads
    // must prefer live Catalog state over Orders-local snapshots.
    public Task<DishSnapshotResponse?> GetDishAsync(int dishId, CancellationToken cancellationToken) =>
        GetDishCoreAsync(dishId, cancellationToken);

    public Task<IReadOnlyList<DishSnapshotResponse>?> GetDishesAsync(IEnumerable<int> dishIds, CancellationToken cancellationToken)
    {
        var ids = dishIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<DishSnapshotResponse>?>(Array.Empty<DishSnapshotResponse>());
        }

        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        return GetDishesCoreAsync(ids, query, cancellationToken);
    }

    public Task<IReadOnlyList<BranchSnapshotResponse>?> GetBranchesAsync(IEnumerable<int> branchIds, CancellationToken cancellationToken)
    {
        var ids = branchIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<BranchSnapshotResponse>?>(Array.Empty<BranchSnapshotResponse>());
        }

        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        return GetOrCreateAsync(
            $"catalog:branches:{string.Join(",", ids)}",
            () => GetBranchesCoreAsync(ids, query, cancellationToken));
    }

    public async Task OccupyTableAsync(int tableId, int? currentOrderId, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync($"/api/internal/tables/{tableId}/occupy", new TableOccupancyRequest(currentOrderId), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseTableAsync(int tableId, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"/api/internal/tables/{tableId}/release", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> ResetAllTablesAsync(CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync("/api/internal/tables/reset-all", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ResetAllTablesResponse>(cancellationToken);
        return payload?.ResetTables ?? 0;
    }

    public async Task<IngredientConsumptionResult> ConsumeIngredientsForOrderAsync(
        int orderId,
        IReadOnlyList<OrderIngredientConsumptionItem> items,
        CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync(
            "/api/internal/inventory/consume",
            new IngredientConsumptionRequest(orderId, items),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IngredientConsumptionResult>(cancellationToken)
                ?? new IngredientConsumptionResult(true, null, Array.Empty<IngredientConsumptionIssue>());
        }

        if ((int)response.StatusCode is 400 or 409)
        {
            return await response.Content.ReadFromJsonAsync<IngredientConsumptionResult>(cancellationToken)
                ?? new IngredientConsumptionResult(false, "Không thể trừ nguyên liệu trong kho.", Array.Empty<IngredientConsumptionIssue>());
        }

        response.EnsureSuccessStatusCode();
        return new IngredientConsumptionResult(true, null, Array.Empty<IngredientConsumptionIssue>());
    }

    public async Task<IngredientConsumptionResult> ValidateIngredientsForOrderAsync(
        int orderId,
        IReadOnlyList<OrderIngredientConsumptionItem> items,
        CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync(
            "/api/internal/inventory/validate",
            new IngredientConsumptionRequest(orderId, items),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IngredientConsumptionResult>(cancellationToken)
                ?? new IngredientConsumptionResult(true, null, Array.Empty<IngredientConsumptionIssue>());
        }

        if ((int)response.StatusCode is 400 or 409)
        {
            return await response.Content.ReadFromJsonAsync<IngredientConsumptionResult>(cancellationToken)
                ?? new IngredientConsumptionResult(false, "Không thể kiểm tra kho nguyên liệu.", Array.Empty<IngredientConsumptionIssue>());
        }

        response.EnsureSuccessStatusCode();
        return new IngredientConsumptionResult(true, null, Array.Empty<IngredientConsumptionIssue>());
    }

    private Task<T?> GetOrCreateAsync<T>(string cacheKey, Func<Task<T?>> factory)
    {
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SnapshotCacheTtl;
            return await factory();
        })!;
    }

    private async Task<TableSnapshotResponse?> GetTableCoreAsync(int tableId, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogTableSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TableId == tableId, cancellationToken);
        if (IsTableSnapshotFresh(cached?.RefreshedAtUtc))
        {
            return Map(cached!);
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<TableSnapshotResponse>($"/api/internal/tables/{tableId}", cancellationToken);
            if (remote is not null)
            {
                await UpsertTableSnapshotAsync(remote, cancellationToken);
            }

            return remote ?? (cached is null ? null : Map(cached));
        }
        catch
        {
            return cached is null ? null : Map(cached);
        }
    }

    private async Task<IReadOnlyList<TableSnapshotResponse>?> GetTablesCoreAsync(int[] ids, string query, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogTableSnapshots.AsNoTracking()
            .Where(x => ids.Contains(x.TableId))
            .ToListAsync(cancellationToken);
        if (cached.Count == ids.Length && cached.All(x => IsTableSnapshotFresh(x.RefreshedAtUtc)))
        {
            return cached.Select(Map).ToArray();
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<TableSnapshotResponse>>($"/api/internal/tables:batch?{query}", cancellationToken);
            if (remote is not null)
            {
                await UpsertTableSnapshotsAsync(remote, cancellationToken);
                return remote;
            }
        }
        catch
        {
        }

        return cached.Select(Map).ToArray();
    }

    private async Task<DishSnapshotResponse?> GetDishCoreAsync(int dishId, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogDishSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DishId == dishId, cancellationToken);

        try
        {
            var remote = await _http.GetFromJsonAsync<DishSnapshotResponse>($"/api/internal/dishes/{dishId}", cancellationToken);
            if (remote is not null)
            {
                await UpsertDishSnapshotAsync(remote, cancellationToken);
            }

            return remote ?? (cached is null ? null : Map(cached));
        }
        catch
        {
            return cached is null ? null : Map(cached);
        }
    }

    private async Task<IReadOnlyList<DishSnapshotResponse>?> GetDishesCoreAsync(int[] ids, string query, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogDishSnapshots.AsNoTracking()
            .Where(x => ids.Contains(x.DishId))
            .ToListAsync(cancellationToken);

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<DishSnapshotResponse>>($"/api/internal/dishes:batch?{query}", cancellationToken);
            if (remote is not null)
            {
                await UpsertDishSnapshotsAsync(remote, cancellationToken);
                return remote;
            }
        }
        catch
        {
        }

        return cached.Select(Map).ToArray();
    }

    private async Task<IReadOnlyList<BranchSnapshotResponse>?> GetBranchesCoreAsync(int[] ids, string query, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogBranchSnapshots.AsNoTracking()
            .Where(x => ids.Contains(x.BranchId))
            .ToListAsync(cancellationToken);
        if (cached.Count == ids.Length && cached.All(x => IsBranchSnapshotFresh(x.RefreshedAtUtc)))
        {
            return cached.Select(Map).ToArray();
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<BranchSnapshotResponse>>($"/api/internal/branches:batch?{query}", cancellationToken);
            if (remote is not null)
            {
                await UpsertBranchSnapshotsAsync(remote, cancellationToken);
                return remote;
            }
        }
        catch
        {
        }

        return cached.Select(Map).ToArray();
    }

    private async Task UpsertDishSnapshotAsync(DishSnapshotResponse snapshot, CancellationToken cancellationToken)
    {
        await UpsertDishSnapshotsAsync(new[] { snapshot }, cancellationToken);
    }

    private async Task UpsertDishSnapshotsAsync(IEnumerable<DishSnapshotResponse> snapshots, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = snapshots.ToList();
        var ids = list.Select(x => x.DishId).Distinct().ToArray();
        var existing = await _db.CatalogDishSnapshots.Where(x => ids.Contains(x.DishId)).ToDictionaryAsync(x => x.DishId, cancellationToken);
        foreach (var item in list)
        {
            if (!existing.TryGetValue(item.DishId, out var entity))
            {
                entity = new CatalogDishSnapshots { DishId = item.DishId };
                _db.CatalogDishSnapshots.Add(entity);
            }

            entity.Name = item.Name;
            entity.CategoryId = item.CategoryId;
            entity.CategoryName = item.CategoryName;
            entity.Price = item.Price;
            entity.Unit = item.Unit;
            entity.Image = item.Image;
            entity.IsActive = item.IsActive;
            entity.Available = item.Available;
            entity.RefreshedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertTableSnapshotAsync(TableSnapshotResponse snapshot, CancellationToken cancellationToken)
    {
        await UpsertTableSnapshotsAsync(new[] { snapshot }, cancellationToken);
    }

    private async Task UpsertTableSnapshotsAsync(IEnumerable<TableSnapshotResponse> snapshots, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = snapshots.ToList();
        var ids = list.Select(x => x.TableId).Distinct().ToArray();
        var existing = await _db.CatalogTableSnapshots.Where(x => ids.Contains(x.TableId)).ToDictionaryAsync(x => x.TableId, cancellationToken);
        foreach (var item in list)
        {
            if (!existing.TryGetValue(item.TableId, out var entity))
            {
                entity = new CatalogTableSnapshots { TableId = item.TableId };
                _db.CatalogTableSnapshots.Add(entity);
            }

            entity.BranchId = item.BranchId;
            entity.TableNumber = item.TableNumber;
            entity.QrCode = item.QrCode;
            entity.IsActive = item.IsActive;
            entity.StatusId = item.StatusId;
            entity.StatusCode = item.StatusCode;
            entity.StatusName = item.StatusName;
            entity.RefreshedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertBranchSnapshotsAsync(IEnumerable<BranchSnapshotResponse> snapshots, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = snapshots.ToList();
        var ids = list.Select(x => x.BranchId).Distinct().ToArray();
        var existing = await _db.CatalogBranchSnapshots.Where(x => ids.Contains(x.BranchId)).ToDictionaryAsync(x => x.BranchId, cancellationToken);
        foreach (var item in list)
        {
            if (!existing.TryGetValue(item.BranchId, out var entity))
            {
                entity = new CatalogBranchSnapshots { BranchId = item.BranchId };
                _db.CatalogBranchSnapshots.Add(entity);
            }

            entity.Name = item.Name;
            entity.Location = item.Location;
            entity.IsActive = item.IsActive;
            entity.RefreshedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Policy: use fresh local snapshots first, refresh remotely when stale/missing, and
    // fall back to stale local data if Catalog.Api is temporarily unavailable.
    private async Task<IReadOnlyList<int>?> GetBranchTableIdsCoreAsync(int branchId, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogTableSnapshots
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.TableId)
            .ToListAsync(cancellationToken);

        if (cached.Count > 0 && cached.All(x => IsTableSnapshotFresh(x.RefreshedAtUtc)))
        {
            return cached.Select(x => x.TableId).ToArray();
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<int>>($"/api/internal/branches/{branchId}/table-ids", cancellationToken);
            if (remote is not null)
            {
                _ = await GetTablesAsync(remote, cancellationToken);
                return remote;
            }
        }
        catch
        {
        }

        return cached.Select(x => x.TableId).ToArray();
    }

    private static bool IsBranchSnapshotFresh(DateTime? refreshedAtUtc)
        => IsFresh(refreshedAtUtc, BranchSnapshotFreshnessTtl);

    private static bool IsTableSnapshotFresh(DateTime? refreshedAtUtc)
        => IsFresh(refreshedAtUtc, TableSnapshotFreshnessTtl);

    private static bool IsDishSnapshotFresh(DateTime? refreshedAtUtc)
        => IsFresh(refreshedAtUtc, DishSnapshotFreshnessTtl);

    private static bool IsFresh(DateTime? refreshedAtUtc, TimeSpan ttl)
        => refreshedAtUtc.HasValue && refreshedAtUtc.Value >= DateTime.UtcNow.Subtract(ttl);

    private static TableSnapshotResponse Map(CatalogTableSnapshots entity) =>
        new(entity.TableId, entity.BranchId, entity.TableNumber, entity.QrCode, entity.IsActive, entity.StatusId, entity.StatusCode, entity.StatusName);

    private static DishSnapshotResponse Map(CatalogDishSnapshots entity) =>
        new(entity.DishId, entity.Name, entity.CategoryId, entity.CategoryName, entity.Price, entity.Unit, entity.Image, entity.IsActive, entity.Available);

    private static BranchSnapshotResponse Map(CatalogBranchSnapshots entity) =>
        new(entity.BranchId, entity.Name, entity.Location, entity.IsActive);

    public sealed record TableSnapshotResponse(
        int TableId,
        int BranchId,
        int TableNumber,
        string? QrCode,
        bool IsActive,
        int StatusId,
        string? StatusCode,
        string? StatusName);

    public sealed record DishSnapshotResponse(
        int DishId,
        string Name,
        int CategoryId,
        string? CategoryName,
        decimal Price,
        string? Unit,
        string? Image,
        bool IsActive,
        bool Available);

    public sealed record BranchSnapshotResponse(
        int BranchId,
        string Name,
        string? Location,
        bool IsActive);

    public sealed record TableOccupancyRequest(int? CurrentOrderId);
    public sealed record OrderIngredientConsumptionItem(int DishId, int Quantity, int? OrderItemId = null);
    public sealed record IngredientConsumptionIssue(
        int IngredientId,
        string IngredientName,
        decimal RequiredQuantity,
        decimal AvailableQuantity,
        string? Unit);
    public sealed record IngredientConsumptionResult(
        bool Success,
        string? Message,
        IReadOnlyList<IngredientConsumptionIssue> Issues);
    private sealed record IngredientConsumptionRequest(
        int OrderId,
        IReadOnlyList<OrderIngredientConsumptionItem> Items);

    private sealed record ResetAllTablesResponse(bool Success, int ResetTables);
}
