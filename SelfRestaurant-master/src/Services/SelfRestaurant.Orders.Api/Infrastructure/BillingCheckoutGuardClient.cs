using System.Net.Http.Json;

namespace SelfRestaurant.Orders.Api.Infrastructure;

public sealed class BillingCheckoutGuardClient
{
    private readonly HttpClient _http;

    public BillingCheckoutGuardClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CheckoutStateResponse?> GetCheckoutStateAsync(
        int? orderId,
        string? diningSessionCode,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (orderId is > 0)
        {
            query.Add($"orderId={orderId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(diningSessionCode))
        {
            query.Add($"diningSessionCode={Uri.EscapeDataString(diningSessionCode)}");
        }

        if (query.Count == 0)
        {
            return null;
        }

        return await _http.GetFromJsonAsync<CheckoutStateResponse>(
            $"/api/internal/checkout-state?{string.Join("&", query)}",
            cancellationToken);
    }

    public sealed record CheckoutStateResponse(
        bool HasCheckoutInProgress,
        bool HasCompletedCheckout,
        string? Message);
}
