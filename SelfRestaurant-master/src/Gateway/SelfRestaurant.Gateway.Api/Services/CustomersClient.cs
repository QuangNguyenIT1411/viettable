using SelfRestaurant.Gateway.Api.Models;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class CustomersClient : ApiClientBase
{
    private readonly HttpClient _identityFallbackHttp;

    public CustomersClient(HttpClient http, IHttpClientFactory httpClientFactory) : base(http)
    {
        _identityFallbackHttp = httpClientFactory.CreateClient("CustomersFallbackIdentity");
    }

    public Task<CustomerProfileResponse?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        GetAsync<CustomerProfileResponse>($"/api/customers/{customerId}", cancellationToken);

    public Task<JsonElement> GetMobileFavoritesAsync(int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/customers/{customerId}/favorites", ct);
    public Task<JsonElement> AddMobileFavoriteAsync(int customerId, int dishId, string dishName, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/favorites/dishes/{dishId}", new { dishName }, ct);
    public Task RemoveMobileFavoriteAsync(int customerId, int dishId, CancellationToken ct) => DeleteAsync($"/api/internal/mobile/customers/{customerId}/favorites/dishes/{dishId}", ct);
    public Task<JsonElement> GetMobileNotificationsAsync(int customerId, string? cursor, int limit, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/customers/{customerId}/notifications?cursor={Uri.EscapeDataString(cursor ?? string.Empty)}&limit={limit}", ct);
    public Task<JsonElement> ReadMobileNotificationAsync(int customerId, long notificationId, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/notifications/{notificationId}/read", new { }, ct);
    public Task<JsonElement> AddPaymentSucceededNotificationAsync(int customerId, int paymentId, int diningSessionId, decimal amount, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/notifications/payment-succeeded", new { paymentId, diningSessionId, amount }, ct);
    public Task<JsonElement> CreateMobileReviewAsync(int customerId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/reviews", request, ct);
    public Task<JsonElement> GetMobileReviewsAsync(int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/customers/{customerId}/reviews", ct);
    public Task<JsonElement> UpdateMobileReviewAsync(int customerId, int reviewId, object request, CancellationToken ct) => PutForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/reviews/{reviewId}", request, ct);
    public Task DeleteMobileReviewAsync(int customerId, int reviewId, CancellationToken ct) => DeleteAsync($"/api/internal/mobile/customers/{customerId}/reviews/{reviewId}", ct);
    public Task<JsonElement> GetSupportFaqsAsync(CancellationToken ct) => GetAsync<JsonElement>("/api/internal/mobile/support/faqs", ct);
    public Task<JsonElement> CreateSupportRequestAsync(int customerId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/customers/{customerId}/support/requests", request, ct);

    public Task<ReservationDto?> CreateReservationAsync(
        CreateReservationApiRequest request,
        CancellationToken cancellationToken) =>
        PostForAsync<CreateReservationApiRequest, ReservationDto>(
            "/api/customers/reservations",
            request,
            cancellationToken);

    public Task<ReservationDto?> GetReservationByCodeAsync(string reservationCode, CancellationToken cancellationToken) =>
        GetAsync<ReservationDto>($"/api/customers/reservations/{Uri.EscapeDataString(reservationCode)}", cancellationToken);

    public Task<ReservationDto?> GetReservationByIdAsync(int reservationId, CancellationToken cancellationToken) =>
        GetAsync<ReservationDto>($"/api/customers/reservations/by-id/{reservationId}", cancellationToken);

    public Task<ReservationDto?> GetReservationByIdempotencyKeyAsync(string key, CancellationToken cancellationToken) =>
        GetAsync<ReservationDto>($"/api/internal/reservations/by-idempotency/{Uri.EscapeDataString(key)}", cancellationToken);

    public async Task<IReadOnlyList<ReservationDto>> GetCustomerReservationsAsync(
        int customerId,
        CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<ReservationDto>>($"/api/customers/{customerId}/reservations", cancellationToken);
        return list ?? Array.Empty<ReservationDto>();
    }

    public async Task<IReadOnlyList<int>> GetOccupiedTableIdsAsync(int branchId, DateTime fromUtc, DateTime toUtc,
        int? excludeReservationId, CancellationToken cancellationToken)
    {
        var query = $"branchId={branchId}&fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";
        if (excludeReservationId is > 0) query += $"&excludeReservationId={excludeReservationId.Value}";
        return await GetAsync<IReadOnlyList<int>>($"/api/internal/reservations/occupied-tables?{query}", cancellationToken)
            ?? Array.Empty<int>();
    }

    public Task<ReservationDto?> UpdateReservationAsync(int reservationId, UpdateReservationInternalRequest request,
        CancellationToken cancellationToken) =>
        PutForAsync<UpdateReservationInternalRequest, ReservationDto>($"/api/internal/reservations/{reservationId}", request, cancellationToken);

    public Task<ReservationDto?> SubmitPreOrderAsync(int reservationId, SubmitPreOrderInternalRequest request,
        CancellationToken cancellationToken) =>
        PostForAsync<SubmitPreOrderInternalRequest, ReservationDto>($"/api/internal/reservations/{reservationId}/preorder/submit", request, cancellationToken);

    public async Task<IReadOnlyList<ReservationDto>> GetTodayReservationsAsync(
        int? branchId,
        string? status,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>();
        if (branchId is > 0)
        {
            qs.Add($"branchId={branchId.Value}");
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            qs.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }
        if (date.HasValue)
        {
            qs.Add($"date={date.Value:yyyy-MM-dd}");
        }

        var suffix = qs.Count == 0 ? string.Empty : $"?{string.Join("&", qs)}";
        var list = await GetAsync<IReadOnlyList<ReservationDto>>($"/api/customers/reservations/today{suffix}", cancellationToken);
        return list ?? Array.Empty<ReservationDto>();
    }

    public Task<ReservationDto?> ReplaceReservationPreOrderItemsAsync(
        int reservationId,
        ReplaceReservationPreOrderItemsApiRequest request,
        CancellationToken cancellationToken) =>
        PostForAsync<ReplaceReservationPreOrderItemsApiRequest, ReservationDto>(
            $"/api/customers/reservations/{reservationId}/preorder-items",
            request,
            cancellationToken);

    public Task<ReservationDto?> CancelReservationAsync(int reservationId, CancellationToken cancellationToken) =>
        PostForAsync<object, ReservationDto>(
            $"/api/customers/reservations/{reservationId}/cancel",
            new { },
            cancellationToken);

    public Task<ReservationDto?> BeginReservationCheckInAsync(int reservationId, CancellationToken cancellationToken) =>
        PostForAsync<object, ReservationDto>(
            $"/api/customers/reservations/{reservationId}/begin-check-in",
            new { },
            cancellationToken);

    public Task<ReservationDto?> CompleteReservationCheckInAsync(
        int reservationId,
        CheckInReservationRequest request,
        CancellationToken cancellationToken) =>
        PostForAsync<CheckInReservationRequest, ReservationDto>(
            $"/api/customers/reservations/{reservationId}/complete-check-in",
            request,
            cancellationToken);

    public Task<ReservationDto?> FailReservationCheckInAsync(int reservationId, CancellationToken cancellationToken) =>
        PostForAsync<object, ReservationDto>(
            $"/api/customers/reservations/{reservationId}/fail-check-in",
            new { },
            cancellationToken);

    public async Task<IReadOnlyList<ReadyDishNotificationDto>> GetReadyNotificationsAsync(
        int customerId,
        int? tableId,
        CancellationToken cancellationToken)
    {
        var qs = new List<string> { "status=OPEN" };
        if (tableId is > 0)
        {
            qs.Add($"tableId={tableId.Value}");
        }

        var list = await GetAsync<IReadOnlyList<ReadyDishNotificationDto>>(
            $"/api/customers/{customerId}/ready-notifications?{string.Join("&", qs)}",
            cancellationToken);
        return list ?? Array.Empty<ReadyDishNotificationDto>();
    }

    public Task ResolveReadyNotificationAsync(long notificationId, int customerId, int? tableId, CancellationToken cancellationToken)
    {
        var suffix = tableId is > 0 ? $"?tableId={tableId.Value}" : string.Empty;
        return PostAsync<object>($"/api/customers/{customerId}/ready-notifications/{notificationId}/resolve{suffix}", new { }, cancellationToken);
    }

    public Task ResetDevTestStateAsync(CancellationToken cancellationToken) =>
        PostAsync<object>("/api/dev/reset-test-state", new { }, cancellationToken);

    public async Task<IReadOnlyList<CustomerOrderHistoryDto>> GetOrdersAsync(int customerId, int take, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<CustomerOrderHistoryDto>>(
            $"/api/customers/{customerId}/orders?take={take}",
            cancellationToken);
        return list ?? Array.Empty<CustomerOrderHistoryDto>();
    }

    public Task UpdateProfileAsync(int customerId, UpdateCustomerProfileRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/customers/{customerId}/profile", request, cancellationToken);

    public Task<AdminCustomerPagedResponse?> GetAdminCustomersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        GetAdminCustomersCompatAsync(search, page, pageSize, cancellationToken);

    private Task<AdminCustomerPagedResponse?> GetAdminCustomersCompatAsync(
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        return ExecuteWithFallbackAsync(
            () => GetAsync<AdminCustomerPagedResponse>($"/api/customers/admin/customers?{string.Join("&", qs)}", cancellationToken),
            () => GetAsync<AdminCustomerPagedResponse>($"/api/identity/admin/customers?{string.Join("&", qs)}", cancellationToken, _identityFallbackHttp));
    }

    public Task<AdminCustomerDto?> GetAdminCustomerByIdAsync(int customerId, CancellationToken cancellationToken) =>
        ExecuteWithFallbackAsync(
            () => GetAsync<AdminCustomerDto>($"/api/customers/admin/customers/{customerId}", cancellationToken),
            () => GetAsync<AdminCustomerDto>($"/api/identity/admin/customers/{customerId}", cancellationToken, _identityFallbackHttp));

    public Task CreateAdminCustomerAsync(AdminUpsertCustomerRequest request, CancellationToken cancellationToken) =>
        ExecuteWithFallbackAsync(
            () => PostAsync("/api/customers/admin/customers", request, cancellationToken),
            () => PostAsync("/api/identity/admin/customers", request, cancellationToken, _identityFallbackHttp));

    public Task UpdateAdminCustomerAsync(int customerId, AdminUpsertCustomerRequest request, CancellationToken cancellationToken) =>
        ExecuteWithFallbackAsync(
            () => PutAsync($"/api/customers/admin/customers/{customerId}", request, cancellationToken),
            () => PutAsync($"/api/identity/admin/customers/{customerId}", request, cancellationToken, _identityFallbackHttp));

    public Task DeactivateAdminCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        ExecuteWithFallbackAsync(
            () => PostAsync<object>($"/api/customers/admin/customers/{customerId}/deactivate", new { }, cancellationToken),
            () => PostAsync<object>($"/api/identity/admin/customers/{customerId}/deactivate", new { }, cancellationToken, _identityFallbackHttp));

    public Task<object?> GetCustomerActivityLogsAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken) =>
        GetAsync<object>($"/api/identity/admin/customers/{customerId}/activity-logs?page={page}&pageSize={pageSize}", cancellationToken, _identityFallbackHttp);

    public Task<object?> GetCustomerOrderHistoryAsync(int customerId, int page, int pageSize, int days, CancellationToken cancellationToken) =>
        GetAsync<object>($"/api/internal/customers/{customerId}/order-history?page={page}&pageSize={pageSize}&days={days}", cancellationToken, _identityFallbackHttp);

    private static async Task<TResponse?> ExecuteWithFallbackAsync<TResponse>(Func<Task<TResponse?>> primaryCall, params Func<Task<TResponse?>>[] fallbackCalls)
    {
        try
        {
            var primaryResponse = await primaryCall();
            if (primaryResponse is not null)
            {
                return primaryResponse;
            }
        }
        catch (Exception primaryException)
        {
            var lastException = primaryException;
            foreach (var fallbackCall in fallbackCalls)
            {
                try
                {
                    var fallbackResponse = await fallbackCall();
                    if (fallbackResponse is not null)
                    {
                        return fallbackResponse;
                    }
                }
                catch (Exception fallbackException)
                {
                    lastException = fallbackException;
                }
            }

            throw lastException;
        }

        foreach (var fallbackCall in fallbackCalls)
        {
            var fallbackResponse = await fallbackCall();
            if (fallbackResponse is not null)
            {
                return fallbackResponse;
            }
        }

        return default;
    }

    private static async Task ExecuteWithFallbackAsync(Func<Task> primaryCall, params Func<Task>[] fallbackCalls)
    {
        try
        {
            await primaryCall();
            return;
        }
        catch (Exception primaryException)
        {
            var lastException = primaryException;
            foreach (var fallbackCall in fallbackCalls)
            {
                try
                {
                    await fallbackCall();
                    return;
                }
                catch (Exception fallbackException)
                {
                    lastException = fallbackException;
                }
            }

            throw lastException;
        }
    }
}
