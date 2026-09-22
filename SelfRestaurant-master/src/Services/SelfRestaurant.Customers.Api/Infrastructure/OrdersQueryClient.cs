using System.Net.Http.Json;

namespace SelfRestaurant.Customers.Api.Infrastructure;

public sealed class OrdersQueryClient
{
    private readonly HttpClient _http;

    public OrdersQueryClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<object>> GetCustomerOrdersAsync(int customerId, int take, CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            return Array.Empty<object>();
        }

        var response = await _http.GetFromJsonAsync<List<object>>(
            $"/api/internal/customers/{customerId}/orders?take={Math.Clamp(take, 1, 50)}",
            cancellationToken);

        return response ?? new List<object>();
    }
}
