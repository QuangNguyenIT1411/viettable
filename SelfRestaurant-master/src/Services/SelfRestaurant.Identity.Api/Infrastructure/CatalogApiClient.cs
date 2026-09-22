using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Infrastructure;

public sealed class CatalogApiClient
{
    private static readonly TimeSpan SnapshotCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SnapshotFreshnessTtl = TimeSpan.FromMinutes(15);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IdentityDbContext _db;

    public CatalogApiClient(HttpClient http, IMemoryCache cache, IdentityDbContext db)
    {
        _http = http;
        _cache = cache;
        _db = db;
    }

    public Task<BranchSnapshotResponse?> GetBranchAsync(int branchId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"catalog:branch:{branchId}",
            () => GetBranchCoreAsync(branchId, cancellationToken));

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

    public async Task<int> GetActiveBranchCountAsync(CancellationToken cancellationToken)
        => (await GetAllActiveBranchesAsync(cancellationToken)).Count;

    public async Task<IReadOnlyList<BranchSnapshotResponse>> GetAllActiveBranchesAsync(CancellationToken cancellationToken)
        => await GetOrCreateRequiredAsync(
               "catalog:branches:active",
               () => GetAllActiveBranchesCoreAsync(cancellationToken))
           ?? Array.Empty<BranchSnapshotResponse>();

    public async Task<ChefActivityLogsPagedResponse?> GetChefActivityLogsAsync(
        int branchId,
        int chefId,
        int page,
        int pageSize,
        int days,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ChefActivityLogsPagedResponse>(
                $"/api/admin/branches/{branchId}/chef/{chefId}/activity-logs?page={page}&pageSize={pageSize}&days={days}",
                cancellationToken);
            return response;
        }
        catch
        {
            return null;
        }
    }

    private Task<T?> GetOrCreateAsync<T>(string cacheKey, Func<Task<T?>> factory)
    {
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SnapshotCacheTtl;
            return await factory();
        })!;
    }

    private Task<T> GetOrCreateRequiredAsync<T>(string cacheKey, Func<Task<T>> factory)
        where T : notnull
    {
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SnapshotCacheTtl;
            return await factory();
        })!;
    }

    private async Task<BranchSnapshotResponse?> GetBranchCoreAsync(int branchId, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogBranchSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId, cancellationToken);

        if (IsFresh(cached?.RefreshedAtUtc))
        {
            return Map(cached!);
        }

        try
        {
            var remote = (await _http.GetFromJsonAsync<IReadOnlyList<BranchSnapshotResponse>>(
                $"/api/internal/branches:batch?ids={branchId}",
                cancellationToken))?.FirstOrDefault();

            if (remote is not null)
            {
                await UpsertBranchSnapshotsAsync(new[] { remote }, cancellationToken);
                return remote;
            }
        }
        catch
        {
        }

        return cached is null ? null : Map(cached);
    }

    private async Task<IReadOnlyList<BranchSnapshotResponse>?> GetBranchesCoreAsync(int[] ids, string query, CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogBranchSnapshots
            .AsNoTracking()
            .Where(x => ids.Contains(x.BranchId))
            .ToListAsync(cancellationToken);

        if (cached.Count == ids.Length && cached.All(x => IsFresh(x.RefreshedAtUtc)))
        {
            return cached.Select(Map).ToArray();
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<BranchSnapshotResponse>>(
                $"/api/internal/branches:batch?{query}",
                cancellationToken);

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

    private async Task<IReadOnlyList<BranchSnapshotResponse>> GetAllActiveBranchesCoreAsync(CancellationToken cancellationToken)
    {
        var cached = await _db.CatalogBranchSnapshots
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        if (cached.Count > 0 && cached.All(x => IsFresh(x.RefreshedAtUtc)))
        {
            return cached.Select(Map).ToArray();
        }

        try
        {
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<PublicBranchResponse>>("/api/branches", cancellationToken);
            if (remote is not null)
            {
                var materialized = remote
                    .Select(x => new BranchSnapshotResponse(x.BranchId, x.Name, x.Location, true))
                    .ToArray();
                await ReplaceActiveBranchSnapshotSetAsync(materialized, cancellationToken);
                return materialized;
            }
        }
        catch
        {
        }

        return cached.Select(Map).ToArray();
    }

    private async Task UpsertBranchSnapshotsAsync(IEnumerable<BranchSnapshotResponse> snapshots, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = snapshots.ToList();
        var ids = list.Select(x => x.BranchId).Distinct().ToArray();
        var existing = await _db.CatalogBranchSnapshots
            .Where(x => ids.Contains(x.BranchId))
            .ToDictionaryAsync(x => x.BranchId, cancellationToken);

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

    private async Task ReplaceActiveBranchSnapshotSetAsync(IEnumerable<BranchSnapshotResponse> branches, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = branches.ToList();
        var ids = list.Select(x => x.BranchId).Distinct().ToHashSet();
        var existing = await _db.CatalogBranchSnapshots.ToListAsync(cancellationToken);

        foreach (var entity in existing)
        {
            if (!ids.Contains(entity.BranchId))
            {
                entity.IsActive = false;
                entity.RefreshedAtUtc = now;
            }
        }

        foreach (var item in list)
        {
            var entity = existing.FirstOrDefault(x => x.BranchId == item.BranchId);
            if (entity is null)
            {
                entity = new CatalogBranchSnapshots { BranchId = item.BranchId };
                _db.CatalogBranchSnapshots.Add(entity);
            }

            entity.Name = item.Name;
            entity.Location = item.Location;
            entity.IsActive = true;
            entity.RefreshedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsFresh(DateTime? refreshedAtUtc)
        => refreshedAtUtc.HasValue && refreshedAtUtc.Value >= DateTime.UtcNow.Subtract(SnapshotFreshnessTtl);

    private static BranchSnapshotResponse Map(CatalogBranchSnapshots entity)
        => new(entity.BranchId, entity.Name, entity.Location, entity.IsActive);

    public sealed record BranchSnapshotResponse(int BranchId, string Name, string? Location, bool IsActive);
    private sealed record PublicBranchResponse(int BranchId, string Name, string? Location);
}
