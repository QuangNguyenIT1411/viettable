using System.Net.Http.Json;

namespace SelfRestaurant.Billing.Api.Infrastructure;

/// <summary>Customers' notification inbox: "Cộng điểm thưởng" / "Lên hạng" after a paid invoice's points settle.</summary>
public sealed class CustomerInboxClient(HttpClient http)
{
    public async Task LoyaltySettledAsync(int customerId, int paymentId, int pointsBefore, int pointsEarned, int pointsAfter,
        CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"/api/internal/mobile/customers/{customerId}/notifications/loyalty",
            new { paymentId, pointsBefore, pointsEarned, pointsAfter }, ct);
        response.EnsureSuccessStatusCode();
    }
}
