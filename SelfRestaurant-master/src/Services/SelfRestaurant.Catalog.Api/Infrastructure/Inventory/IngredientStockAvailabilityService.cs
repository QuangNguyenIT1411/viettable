using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api.Infrastructure.Inventory;

public sealed class IngredientStockAvailabilityService
{
    private readonly CatalogDbContext _db;

    public IngredientStockAvailabilityService(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<int, IngredientStockAvailability>> BuildIngredientStockAvailabilityMapAsync(
        IEnumerable<int> ingredientIds,
        CancellationToken cancellationToken)
    {
        var ids = ingredientIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, IngredientStockAvailability>();
        }

        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => ids.Contains(i.IngredientID))
            .Select(i => new
            {
                i.IngredientID,
                i.CurrentStock
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryDate = today.AddDays(7);
        var batches = await _db.IngredientBatches
            .AsNoTracking()
            .Where(b => ids.Contains(b.IngredientID) && b.IsActive)
            .Select(b => new
            {
                b.IngredientID,
                b.QuantityRemaining,
                b.ExpiryDate
            })
            .ToListAsync(cancellationToken);

        var batchLookup = batches
            .GroupBy(b => b.IngredientID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return ingredients.ToDictionary(
            i => i.IngredientID,
            i =>
            {
                batchLookup.TryGetValue(i.IngredientID, out var activeBatches);
                activeBatches ??= [];

                var remainingBatches = activeBatches
                    .Where(b => b.QuantityRemaining > 0)
                    .ToList();
                var usableBatches = remainingBatches
                    .Where(b => b.ExpiryDate >= today)
                    .ToList();
                var hasActiveBatches = activeBatches.Count > 0;
                var usableBatchStock = usableBatches.Sum(b => b.QuantityRemaining);

                return new IngredientStockAvailability(
                    CurrentStock: i.CurrentStock,
                    HasActiveBatches: hasActiveBatches,
                    TotalBatchStock: remainingBatches.Sum(b => b.QuantityRemaining),
                    UsableBatchStock: usableBatchStock,
                    AvailabilityStock: hasActiveBatches ? usableBatchStock : i.CurrentStock,
                    ExpiredBatchCount: remainingBatches.Count(b => b.ExpiryDate < today),
                    NearExpiryBatchCount: remainingBatches.Count(b => b.ExpiryDate >= today && b.ExpiryDate <= nearExpiryDate),
                    NearestExpiryDate: usableBatches.Count == 0 ? null : usableBatches.Min(b => b.ExpiryDate));
            });
    }
}

public readonly record struct IngredientStockAvailability(
    decimal CurrentStock,
    bool HasActiveBatches,
    decimal TotalBatchStock,
    decimal UsableBatchStock,
    decimal AvailabilityStock,
    int ExpiredBatchCount,
    int NearExpiryBatchCount,
    DateOnly? NearestExpiryDate);
