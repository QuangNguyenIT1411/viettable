namespace SelfRestaurant.Orders.Api.Infrastructure;

/// <summary>
/// Read-only catalog boundary for Orders. All branch/table/dish reads must go through
/// local snapshot policy in this contract instead of querying snapshot tables directly.
/// </summary>
public interface ICatalogReadModel
{
    Task<CatalogApiClient.TableSnapshotResponse?> GetTableAsync(int tableId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogApiClient.TableSnapshotResponse>?> GetTablesAsync(IEnumerable<int> tableIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>?> GetBranchTableIdsAsync(int branchId, CancellationToken cancellationToken);
    Task<CatalogApiClient.DishSnapshotResponse?> GetDishAsync(int dishId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogApiClient.DishSnapshotResponse>?> GetDishesAsync(IEnumerable<int> dishIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogApiClient.BranchSnapshotResponse>?> GetBranchesAsync(IEnumerable<int> branchIds, CancellationToken cancellationToken);
    Task OccupyTableAsync(int tableId, int? currentOrderId, CancellationToken cancellationToken);
    Task ReleaseTableAsync(int tableId, CancellationToken cancellationToken);
    Task<int> ResetAllTablesAsync(CancellationToken cancellationToken);
    Task<CatalogApiClient.IngredientConsumptionResult> ConsumeIngredientsForOrderAsync(
        int orderId,
        IReadOnlyList<CatalogApiClient.OrderIngredientConsumptionItem> items,
        CancellationToken cancellationToken);
    Task<CatalogApiClient.IngredientConsumptionResult> ValidateIngredientsForOrderAsync(
        int orderId,
        IReadOnlyList<CatalogApiClient.OrderIngredientConsumptionItem> items,
        CancellationToken cancellationToken);
}
