using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class CatalogClient : ApiClientBase
{
    public CatalogClient(HttpClient http) : base(http)
    {
    }

    public Task<IReadOnlyList<BranchDto>?> GetBranchesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<BranchDto>>("/api/branches", cancellationToken);

    public Task<BranchTablesResponse?> GetBranchTablesAsync(int branchId, CancellationToken cancellationToken) =>
        GetAsync<BranchTablesResponse>($"/api/branches/{branchId}/tables", cancellationToken);

    public Task<MenuResponse?> GetMenuAsync(int branchId, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var query = date is null ? "" : $"?date={date:yyyy-MM-dd}";
        return GetAsync<MenuResponse>($"/api/branches/{branchId}/menu{query}", cancellationToken);
    }

    public Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var flag = includeInactive ? "true" : "false";
        return GetAsync<IReadOnlyList<CategoryDto>>($"/api/categories?includeInactive={flag}", cancellationToken);
    }

    public Task CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/categories", request, cancellationToken);

    public Task UpdateCategoryAsync(int categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/categories/{categoryId}", request, cancellationToken);

    public Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/categories/{categoryId}", cancellationToken);

    public Task<BranchTableDto?> GetTableByQrAsync(string code, CancellationToken cancellationToken) =>
        GetAsync<BranchTableDto>($"/api/tables/qr/{Uri.EscapeDataString(code)}", cancellationToken);

    public Task<AdminDishPagedResponse?> GetAdminDishesAsync(
        string? search,
        int? categoryId,
        int page,
        int pageSize,
        bool includeInactive,
        bool vegetarianOnly,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
            $"includeInactive={(includeInactive ? "true" : "false")}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }
        if (categoryId is > 0)
        {
            qs.Add($"categoryId={categoryId.Value}");
        }
        if (vegetarianOnly)
        {
            qs.Add("vegetarianOnly=true");
        }

        return GetAsync<AdminDishPagedResponse>($"/api/admin/dishes?{string.Join("&", qs)}", cancellationToken);
    }

    public Task<AdminDishDto?> GetAdminDishByIdAsync(int dishId, CancellationToken cancellationToken) =>
        GetAsync<AdminDishDto>($"/api/admin/dishes/{dishId}", cancellationToken);

    public Task CreateAdminDishAsync(AdminUpsertDishRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/dishes", request, cancellationToken);

    public Task CreateChefDishForBranchAsync(int branchId, AdminUpsertDishRequest request, CancellationToken cancellationToken) =>
        PostAsync($"/api/admin/branches/{branchId}/chef/dishes", request, cancellationToken);

    public Task UpdateAdminDishAsync(int dishId, AdminUpsertDishRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/dishes/{dishId}", request, cancellationToken);

    public Task UpdateChefDishForBranchAsync(int branchId, int dishId, AdminUpsertDishRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/branches/{branchId}/chef/dishes/{dishId}", request, cancellationToken);

    public Task<ChefSetCatalogDishAvailabilityResponse?> SetChefDishAvailabilityAsync(int branchId, int dishId, bool available, CancellationToken cancellationToken) =>
        PostForAsync<ChefSetCatalogDishAvailabilityRequest, ChefSetCatalogDishAvailabilityResponse>(
            $"/api/admin/branches/{branchId}/chef/dishes/{dishId}/availability",
            new ChefSetCatalogDishAvailabilityRequest(available),
            cancellationToken);

    public Task DeactivateAdminDishAsync(int dishId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/admin/dishes/{dishId}/deactivate", new { }, cancellationToken);

    public Task DeleteAdminDishAsync(int dishId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/admin/dishes/{dishId}", cancellationToken);

    public Task<AdminDishIngredientsDto?> GetDishIngredientsAsync(int dishId, CancellationToken cancellationToken) =>
        GetAsync<AdminDishIngredientsDto>($"/api/admin/dishes/{dishId}/ingredients", cancellationToken);

    public Task UpdateDishIngredientsAsync(int dishId, IReadOnlyList<AdminDishIngredientItemRequest> items, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/dishes/{dishId}/ingredients", new AdminUpdateDishIngredientsRequest(items), cancellationToken);

    public async Task<IReadOnlyList<AdminRelatedIngredientDishDto>> GetIngredientRelatedDishesAsync(int ingredientId, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<AdminRelatedIngredientDishDto>>($"/api/admin/ingredients/{ingredientId}/related-dishes", cancellationToken);
        return list ?? Array.Empty<AdminRelatedIngredientDishDto>();
    }

    public Task<AdminIngredientPagedResponse?> GetAdminIngredientsAsync(
        string? search,
        int page,
        int pageSize,
        bool includeInactive,
        string? stockStatus,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
            $"includeInactive={(includeInactive ? "true" : "false")}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }
        if (!string.IsNullOrWhiteSpace(stockStatus) && !string.Equals(stockStatus, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            qs.Add($"stockStatus={Uri.EscapeDataString(stockStatus.Trim())}");
        }

        return GetAsync<AdminIngredientPagedResponse>($"/api/admin/ingredients?{string.Join("&", qs)}", cancellationToken);
    }

    public Task<AdminIngredientDto?> GetAdminIngredientByIdAsync(int ingredientId, CancellationToken cancellationToken) =>
        GetAsync<AdminIngredientDto>($"/api/admin/ingredients/{ingredientId}", cancellationToken);

    public Task CreateAdminIngredientAsync(AdminUpsertIngredientRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/ingredients", request, cancellationToken);

    public Task UpdateAdminIngredientAsync(int ingredientId, AdminUpsertIngredientRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/ingredients/{ingredientId}", request, cancellationToken);

    public Task DeactivateAdminIngredientAsync(int ingredientId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/admin/ingredients/{ingredientId}/deactivate", new { }, cancellationToken);

    public Task DeleteAdminIngredientAsync(int ingredientId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/admin/ingredients/{ingredientId}", cancellationToken);

    public async Task<IReadOnlyList<AdminIngredientBatchDto>> GetIngredientBatchesAsync(int ingredientId, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<AdminIngredientBatchDto>>($"/api/admin/ingredients/{ingredientId}/batches", cancellationToken);
        return list ?? Array.Empty<AdminIngredientBatchDto>();
    }

    public Task CreateIngredientBatchAsync(int ingredientId, CreateIngredientBatchRequest request, CancellationToken cancellationToken) =>
        PostAsync($"/api/admin/ingredients/{ingredientId}/batches", request, cancellationToken);

    public Task UpdateIngredientBatchAsync(int ingredientId, int batchId, UpdateIngredientBatchRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/ingredients/{ingredientId}/batches/{batchId}", request, cancellationToken);

    public Task DeactivateIngredientBatchAsync(int ingredientId, int batchId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/admin/ingredients/{ingredientId}/batches/{batchId}/deactivate", new { }, cancellationToken);

    public Task<PagedResponse<IngredientStockMovementDto>?> GetIngredientStockMovementsAsync(
        int ingredientId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        GetAsync<PagedResponse<IngredientStockMovementDto>>(
            $"/api/admin/ingredients/{ingredientId}/stock-movements?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}",
            cancellationToken);

    public Task<InventorySummaryDto?> GetInventorySummaryAsync(CancellationToken cancellationToken) =>
        GetAsync<InventorySummaryDto>("/api/admin/inventory/summary", cancellationToken);

    public Task<PagedResponse<InventoryBatchDto>?> GetInventoryBatchesAsync(
        string? search,
        string? status,
        int? ingredientId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            qs.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }
        if (ingredientId is > 0)
        {
            qs.Add($"ingredientId={ingredientId.Value}");
        }

        return GetAsync<PagedResponse<InventoryBatchDto>>($"/api/admin/inventory/batches?{string.Join("&", qs)}", cancellationToken);
    }

    public Task StockInAsync(InventoryStockInRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/inventory/stock-in", request, cancellationToken);

    public Task StockOutAsync(InventoryStockOutRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/inventory/stock-out", request, cancellationToken);

    public Task<PagedResponse<InventoryMovementDto>?> GetInventoryMovementsAsync(
        int? ingredientId,
        int? batchId,
        string? movementType,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
        };

        if (ingredientId is > 0) qs.Add($"ingredientId={ingredientId.Value}");
        if (batchId is > 0) qs.Add($"batchId={batchId.Value}");
        if (!string.IsNullOrWhiteSpace(movementType)) qs.Add($"movementType={Uri.EscapeDataString(movementType.Trim())}");
        if (dateFrom is not null) qs.Add($"dateFrom={dateFrom:yyyy-MM-dd}");
        if (dateTo is not null) qs.Add($"dateTo={dateTo:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search.Trim())}");

        return GetAsync<PagedResponse<InventoryMovementDto>>($"/api/admin/inventory/movements?{string.Join("&", qs)}", cancellationToken);
    }

    public Task<AdminUnitPagedResponse?> GetAdminUnitsAsync(
        string? search,
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
            $"includeInactive={(includeInactive ? "true" : "false")}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        return GetAsync<AdminUnitPagedResponse>($"/api/admin/units?{string.Join("&", qs)}", cancellationToken);
    }

    public Task<AdminUnitDto?> GetAdminUnitByIdAsync(int unitId, CancellationToken cancellationToken) =>
        GetAsync<AdminUnitDto>($"/api/admin/units/{unitId}", cancellationToken);

    public Task CreateAdminUnitAsync(AdminUpsertUnitRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/units", request, cancellationToken);

    public Task UpdateAdminUnitAsync(int unitId, AdminUpsertUnitRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/units/{unitId}", request, cancellationToken);

    public Task DeleteAdminUnitAsync(int unitId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/admin/units/{unitId}", cancellationToken);

    public async Task<IReadOnlyList<TableStatusDto>> GetTableStatusesAsync(CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<TableStatusDto>>("/api/admin/table-statuses", cancellationToken);
        return list ?? Array.Empty<TableStatusDto>();
    }

    public Task<AdminTablePagedResponse?> GetAdminTablesAsync(
        int? branchId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
        };

        if (branchId is > 0)
        {
            qs.Add($"branchId={branchId.Value}");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        return GetAsync<AdminTablePagedResponse>($"/api/admin/tables?{string.Join("&", qs)}", cancellationToken);
    }

    public Task<AdminTableDto?> GetAdminTableByIdAsync(int tableId, CancellationToken cancellationToken) =>
        GetAsync<AdminTableDto>($"/api/admin/tables/{tableId}", cancellationToken);

    public Task CreateAdminTableAsync(AdminUpsertTableRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/admin/tables", request, cancellationToken);

    public Task UpdateAdminTableAsync(int tableId, AdminUpsertTableRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/admin/tables/{tableId}", request, cancellationToken);

    public Task DeactivateAdminTableAsync(int tableId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/admin/tables/{tableId}/deactivate", new { }, cancellationToken);

    public Task DeleteAdminTableAsync(int tableId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/admin/tables/{tableId}", cancellationToken);

    public Task ResetDevTestStateAsync(CancellationToken cancellationToken) =>
        PostAsync<object>("/api/dev/reset-test-state", new { }, cancellationToken);
}
