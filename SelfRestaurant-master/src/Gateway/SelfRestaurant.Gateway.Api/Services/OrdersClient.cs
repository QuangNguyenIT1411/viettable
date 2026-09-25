using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class OrdersClient : ApiClientBase
{
    public OrdersClient(HttpClient http) : base(http)
    {
    }

    public Task OccupyTableAsync(int tableId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/tables/{tableId}/occupy", new { }, cancellationToken);

    public Task ResetTableAsync(int tableId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/tables/{tableId}/reset", new { }, cancellationToken);

    public Task ResetAllTablesAsync(CancellationToken cancellationToken) =>
        PostAsync<object>("/api/tables/reset-all", new { }, cancellationToken);

    public Task<ActiveOrderResponse?> GetActiveOrderAsync(int tableId, CancellationToken cancellationToken) =>
        GetAsync<ActiveOrderResponse>($"/api/tables/{tableId}/order", cancellationToken);

    public async Task<IReadOnlyList<ActiveOrderResponse>> GetActiveOrdersAsync(int tableId, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<ActiveOrderResponse>>($"/api/tables/{tableId}/orders/active", cancellationToken);
        return list ?? Array.Empty<ActiveOrderResponse>();
    }

    public Task<ActiveOrderResponse?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken) =>
        GetAsync<ActiveOrderResponse>($"/api/orders/{orderId}", cancellationToken);

    public Task<CustomerActiveOrderContextDto?> GetCustomerActiveOrderContextAsync(int customerId, CancellationToken cancellationToken) =>
        GetAsync<CustomerActiveOrderContextDto>($"/api/internal/customers/{customerId}/active-order-context", cancellationToken);

    public Task<ReservationPreOrderResponse?> GetReservationPreOrderAsync(int reservationId, CancellationToken cancellationToken) =>
        GetAsync<ReservationPreOrderResponse>($"/api/internal/reservation-preorders/{reservationId}", cancellationToken);

    public Task<ReservationPreOrderResponse?> UpsertReservationPreOrderAsync(int reservationId,
        ReservationPreOrderUpsertRequest request, CancellationToken cancellationToken) =>
        PutForAsync<ReservationPreOrderUpsertRequest, ReservationPreOrderResponse>(
            $"/api/internal/reservation-preorders/{reservationId}", request, cancellationToken);

    public Task<ReservationPreOrderResponse?> SubmitReservationPreOrderAsync(int reservationId,
        ReservationPreOrderSubmitRequest request, CancellationToken cancellationToken) =>
        PostForAsync<ReservationPreOrderSubmitRequest, ReservationPreOrderResponse>(
            $"/api/internal/reservation-preorders/{reservationId}/submit", request, cancellationToken);

    public Task<MobileDiningSessionResponse?> CreateDiningSessionAsync(DiningSessionCreateRequest request, CancellationToken cancellationToken) =>
        PostForAsync<DiningSessionCreateRequest, MobileDiningSessionResponse>("/api/internal/dining-sessions/check-in", request, cancellationToken);

    public Task<MobileDiningSessionResponse?> GetActiveDiningSessionAsync(int customerId, CancellationToken cancellationToken) =>
        GetAsync<MobileDiningSessionResponse>($"/api/internal/dining-sessions/active?customerId={customerId}", cancellationToken);

    public Task<MobileDiningSessionResponse?> GetDiningSessionAsync(int sessionId, int customerId, CancellationToken cancellationToken) =>
        GetAsync<MobileDiningSessionResponse>($"/api/internal/dining-sessions/{sessionId}?customerId={customerId}", cancellationToken);

    public Task<MobileDiningSessionResponse?> CancelPaymentRequestAsync(int sessionId, int customerId, CancellationToken cancellationToken) =>
        PostForAsync<DiningSessionCustomerRequest, MobileDiningSessionResponse>(
            $"/api/internal/dining-sessions/{sessionId}/cancel-payment-request", new DiningSessionCustomerRequest(customerId), cancellationToken);

    public Task<MobileOrderResponse?> CreateMobileOrderAsync(int sessionId, InternalMobileCreateOrderRequest request, CancellationToken cancellationToken) =>
        PostForAsync<InternalMobileCreateOrderRequest, MobileOrderResponse>($"/api/internal/dining-sessions/{sessionId}/orders", request, cancellationToken);

    public async Task<IReadOnlyList<MobileOrderResponse>> GetMobileOrdersAsync(int customerId, string? scope, CancellationToken cancellationToken)
    {
        var suffix = string.IsNullOrWhiteSpace(scope) ? "" : $"&scope={Uri.EscapeDataString(scope.Trim())}";
        return await GetAsync<IReadOnlyList<MobileOrderResponse>>($"/api/internal/dining-sessions/mobile-orders?customerId={customerId}{suffix}", cancellationToken)
            ?? Array.Empty<MobileOrderResponse>();
    }

    public Task<MobileOrderResponse?> GetMobileOrderAsync(int orderId, int customerId, CancellationToken cancellationToken) =>
        GetAsync<MobileOrderResponse>($"/api/internal/dining-sessions/mobile-orders/{orderId}?customerId={customerId}", cancellationToken);

    public Task<MobileOrderTrackingResponse?> GetMobileOrderTrackingAsync(int orderId, int customerId, CancellationToken cancellationToken) =>
        GetAsync<MobileOrderTrackingResponse>($"/api/internal/dining-sessions/mobile-orders/{orderId}/tracking?customerId={customerId}", cancellationToken);

    public Task<ServiceRequestResponse?> CreateServiceRequestAsync(int sessionId, InternalCreateServiceRequest request, CancellationToken cancellationToken) =>
        PostForAsync<InternalCreateServiceRequest, ServiceRequestResponse>($"/api/internal/dining-sessions/{sessionId}/service-requests", request, cancellationToken);

    public async Task<IReadOnlyList<ServiceRequestResponse>> GetServiceRequestsAsync(int sessionId, int customerId, CancellationToken cancellationToken) =>
        await GetAsync<IReadOnlyList<ServiceRequestResponse>>($"/api/internal/dining-sessions/{sessionId}/service-requests?customerId={customerId}", cancellationToken)
            ?? Array.Empty<ServiceRequestResponse>();

    public Task<ServiceRequestResponse?> CancelServiceRequestAsync(int requestId, int customerId, CancellationToken cancellationToken) =>
        PostForAsync<DiningSessionCustomerRequest, ServiceRequestResponse>(
            $"/api/internal/dining-sessions/service-requests/{requestId}/cancel", new DiningSessionCustomerRequest(customerId), cancellationToken);

    public async Task<IReadOnlyList<StaffServiceRequestResponse>> GetStaffServiceRequestsAsync(int branchId, CancellationToken ct) =>
        await GetAsync<IReadOnlyList<StaffServiceRequestResponse>>($"/api/internal/service-requests?branchId={branchId}", ct)
        ?? Array.Empty<StaffServiceRequestResponse>();

    public Task<StaffServiceRequestResponse?> AcknowledgeServiceRequestAsync(int id, StaffServiceRequestCommand command, CancellationToken ct) =>
        PostForAsync<StaffServiceRequestCommand, StaffServiceRequestResponse>($"/api/internal/service-requests/{id}/acknowledge", command, ct);

    public Task<StaffServiceRequestResponse?> CompleteServiceRequestAsync(int id, StaffServiceRequestCommand command, CancellationToken ct) =>
        PostForAsync<StaffServiceRequestCommand, StaffServiceRequestResponse>($"/api/internal/service-requests/{id}/complete", command, ct);

    public Task<DishReferenceStatusDto?> GetDishReferenceStatusAsync(int dishId, CancellationToken cancellationToken) =>
        GetAsync<DishReferenceStatusDto>($"/api/internal/dishes/{dishId}/references", cancellationToken);

    public Task<ActiveOrderResponse?> AddItemAsync(int tableId, int dishId, int quantity, string? note, string? expectedDiningSessionCode, CancellationToken cancellationToken) =>
        PostForAsync<object, ActiveOrderResponse>(
            $"/api/tables/{tableId}/order/items",
            new { dishId, quantity, note, expectedDiningSessionCode },
            cancellationToken);

    public Task UpdateQuantityAsync(int tableId, int itemId, int quantity, CancellationToken cancellationToken) =>
        PutAsync($"/api/tables/{tableId}/order/items/{itemId}", new { quantity }, cancellationToken);

    public Task UpdateItemNoteAsync(int tableId, int itemId, string? note, CancellationToken cancellationToken) =>
        PutAsync($"/api/tables/{tableId}/order/items/{itemId}/note", new { note }, cancellationToken);

    public Task RemoveItemAsync(int tableId, int itemId, CancellationToken cancellationToken) =>
        DeleteAsync($"/api/tables/{tableId}/order/items/{itemId}", cancellationToken);

    public Task SubmitOrderAsync(int tableId, string idempotencyKey, string? expectedDiningSessionCode, CancellationToken cancellationToken) =>
        PostAsync($"/api/tables/{tableId}/order/submit", new { idempotencyKey, expectedDiningSessionCode }, cancellationToken);

    public Task<ActiveOrderResponse?> SubmitOrderBatchAsync(
        int tableId,
        IReadOnlyList<AddOrderItemPayload> items,
        string? customerPhoneNumber,
        string idempotencyKey,
        string? expectedDiningSessionCode,
        CancellationToken cancellationToken) =>
        PostForAsync<object, ActiveOrderResponse>(
            $"/api/tables/{tableId}/order/submit-batch",
            new
            {
                items = items.Select(item => new
                {
                    dishId = item.DishId,
                    quantity = item.Quantity,
                    note = item.Note,
                }).ToArray(),
                customerPhoneNumber,
                idempotencyKey,
                expectedDiningSessionCode,
            },
            cancellationToken);

    public Task<LinkDiningSessionTablesResponse?> LinkDiningSessionTablesAsync(
        string diningSessionCode,
        int primaryTableId,
        IReadOnlyList<int> tableIds,
        CancellationToken cancellationToken) =>
        PostForAsync<object, LinkDiningSessionTablesResponse>(
            $"/internal/orders/dining-sessions/{Uri.EscapeDataString(diningSessionCode)}/tables",
            new { primaryTableId, tableIds },
            cancellationToken);

    public Task ConfirmOrderReceivedAsync(int orderId, int customerId, int? itemId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/confirm-received?customerId={customerId}{(itemId is > 0 ? $"&itemId={itemId}" : "")}", new { }, cancellationToken);

    public Task ResetDevTestStateAsync(CancellationToken cancellationToken) =>
        PostAsync<object>("/api/dev/reset-test-state", new { }, cancellationToken);

    public Task<LoyaltyScanResponse?> ScanLoyaltyCardAsync(int tableId, string phoneNumber, CancellationToken cancellationToken) =>
        PostForAsync<object, LoyaltyScanResponse>(
            $"/api/tables/{tableId}/order/scan-loyalty-card",
            new { phoneNumber },
            cancellationToken);

    public Task<IReadOnlyList<int>?> GetTopDishIdsAsync(int branchId, int count, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<int>>($"/api/branches/{branchId}/top-dishes?count={count}", cancellationToken);

    public Task<AdminOrderStatsResponse?> GetAdminStatsAsync(DateOnly? date, int? branchId, CancellationToken cancellationToken)
    {
        var queryParts = new List<string>();
        if (date is not null)
        {
            queryParts.Add($"date={date:yyyy-MM-dd}");
        }
        if (branchId is > 0)
        {
            queryParts.Add($"branchId={branchId.Value}");
        }
        var query = queryParts.Count == 0 ? "" : $"?{string.Join("&", queryParts)}";
        return GetAsync<AdminOrderStatsResponse>($"/api/admin/stats{query}", cancellationToken);
    }

    public Task<AdminRevenueReportDto?> GetAdminRevenueReportAsync(int days, int? branchId, CancellationToken cancellationToken)
    {
        var qs = $"days={Math.Clamp(days, 1, 365)}";
        if (branchId is > 0)
        {
            qs += $"&branchId={branchId.Value}";
        }
        return GetAsync<AdminRevenueReportDto>($"/api/admin/reports/revenue?{qs}", cancellationToken);
    }

    public Task<AdminTopDishReportDto?> GetAdminTopDishesReportAsync(int days, int take, int? branchId, CancellationToken cancellationToken) =>
        GetAsync<AdminTopDishReportDto>(
            $"/api/admin/reports/top-dishes?days={Math.Clamp(days, 1, 365)}&take={Math.Clamp(take, 1, 50)}{(branchId is > 0 ? $"&branchId={branchId.Value}" : string.Empty)}",
            cancellationToken);

    public async Task<IReadOnlyList<ChefOrderDto>> GetChefOrdersAsync(int branchId, string? status, CancellationToken cancellationToken)
    {
        var qs = string.IsNullOrWhiteSpace(status) ? "" : $"?status={Uri.EscapeDataString(status)}";
        var list = await GetAsync<IReadOnlyList<ChefOrderDto>>($"/api/branches/{branchId}/chef/orders{qs}", cancellationToken);
        return list ?? Array.Empty<ChefOrderDto>();
    }

    public Task ChefStartAsync(int orderId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/chef/start", new { }, cancellationToken);

    public Task ChefReadyAsync(int orderId, int chefId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/chef/ready?chefId={chefId}", new { }, cancellationToken);

    public Task ChefStartItemAsync(int orderId, int itemId, int chefId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/items/{itemId}/chef/start?chefId={chefId}", new { }, cancellationToken);

    public Task ChefReadyItemAsync(int orderId, int itemId, int chefId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/items/{itemId}/chef/ready?chefId={chefId}", new { }, cancellationToken);

    public Task ChefCancelItemAsync(int orderId, int itemId, string? reason, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/items/{itemId}/cancel", new { reason }, cancellationToken);

    public Task ChefServeAsync(int orderId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/chef/serve", new { }, cancellationToken);

    public Task ChefUpdateStatusAsync(int orderId, string statusCode, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/status", new { statusCode }, cancellationToken);

    public Task ChefCancelAsync(int orderId, string? reason, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/orders/{orderId}/cancel", new { reason }, cancellationToken);

    public Task ChefUpdateItemNoteAsync(int orderId, int itemId, string? note, bool append, CancellationToken cancellationToken) =>
        PutAsync($"/api/orders/{orderId}/items/{itemId}/chef-note", new { note, append }, cancellationToken);

    public Task<ChefOrderItemIngredientOverridesDto?> GetOrderItemIngredientOverridesAsync(int orderId, int itemId, CancellationToken cancellationToken) =>
        GetAsync<ChefOrderItemIngredientOverridesDto>($"/api/orders/{orderId}/items/{itemId}/ingredient-overrides", cancellationToken);

    public Task UpdateOrderItemIngredientOverridesAsync(int orderId, int itemId, ChefUpdateOrderItemIngredientOverrideRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/orders/{orderId}/items/{itemId}/ingredient-overrides", request, cancellationToken);

    public async Task<IReadOnlyList<ChefHistoryDto>> GetChefHistoryAsync(int branchId, int take, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<ChefHistoryDto>>($"/api/branches/{branchId}/chef/history?take={take}", cancellationToken);
        return list ?? Array.Empty<ChefHistoryDto>();
    }
}

public sealed record LoyaltyScanCustomerResponse(
    string Name,
    string Phone,
    int CurrentPoints,
    int CardPoints);

public sealed record LoyaltyScanResponse(
    bool Success,
    string Message,
    LoyaltyScanCustomerResponse? Customer);

public sealed record AddOrderItemPayload(
    int DishId,
    int Quantity,
    string? Note);

public sealed record LinkDiningSessionTablesResponse(
    string DiningSessionCode,
    int PrimaryTableId,
    IReadOnlyList<int> TableIds,
    IReadOnlyList<LinkDiningSessionTableResponse>? Tables);

public sealed record LinkDiningSessionTableResponse(int TableId, bool IsPrimary);

public sealed record DishReferenceStatusDto(
    int DishId,
    bool HasHistory,
    int OrderItemCount);
