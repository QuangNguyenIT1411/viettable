using System.Net.Http.Json;

namespace SelfRestaurant.Identity.Api.Infrastructure;

public sealed class OrdersApiClient
{
    private readonly HttpClient _http;

    public OrdersApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChefCookingHistoryPagedResponse?> GetChefHistoryAsync(
        int branchId,
        int? employeeId,
        int page,
        int pageSize,
        int days,
        CancellationToken cancellationToken)
    {
        var url = $"/api/internal/branches/{branchId}/chef/history?page={page}&pageSize={pageSize}&days={days}";
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            url += $"&employeeId={employeeId.Value}";
        }
        
        var response = await _http.GetFromJsonAsync<ChefCookingHistoryPagedResponse>(url, cancellationToken);
        return response;
    }

    public async Task<ChefItemCompletionsPagedResponse?> GetChefItemCompletionsAsync(
        int branchId,
        int? employeeId,
        int page,
        int pageSize,
        int days,
        CancellationToken cancellationToken)
    {
        var url = $"/api/internal/branches/{branchId}/chef/item-completions?page={page}&pageSize={pageSize}&days={days}";
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            url += $"&employeeId={employeeId.Value}";
        }
        
        var response = await _http.GetFromJsonAsync<ChefItemCompletionsPagedResponse>(url, cancellationToken);
        return response;
    }

    public async Task<CustomerOrderHistoryPagedResponse?> GetCustomerOrderHistoryAsync(
        int customerId,
        int page,
        int pageSize,
        int days,
        CancellationToken cancellationToken)
    {
        var url = $"/api/internal/customers/{customerId}/order-history?page={page}&pageSize={pageSize}&days={days}";
        var response = await _http.GetFromJsonAsync<CustomerOrderHistoryPagedResponse>(url, cancellationToken);
        return response;
    }
}
