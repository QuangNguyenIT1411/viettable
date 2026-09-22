using System.Net.Http.Json;

namespace SelfRestaurant.Identity.Api.Infrastructure;

public sealed class BillingApiClient
{
    private readonly HttpClient _http;

    public BillingApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CashierHistoryPagedResponse?> GetCashierHistoryAsync(
        int employeeId,
        int page,
        int pageSize,
        int days,
        CancellationToken cancellationToken)
    {
        var response = await _http.GetFromJsonAsync<CashierHistoryPagedResponse>(
            $"/api/internal/employees/{employeeId}/cashier/history?page={page}&pageSize={pageSize}&days={days}",
            cancellationToken);

        return response;
    }
}
