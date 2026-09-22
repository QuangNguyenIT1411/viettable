using System.Net.Http.Json;
using System.Text.Json;

namespace SelfRestaurant.Orders.Api.Infrastructure;

public sealed class BillingEventsClient
{
    private readonly HttpClient _http;

    public BillingEventsClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<BillingOutboxEventDto>> GetPendingPaymentCompletedAsync(int take, CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<List<BillingOutboxEventDto>>(
            $"/api/internal/outbox/pending?eventName=payment.completed.v1&take={Math.Clamp(take, 1, 100)}",
            cancellationToken);

        return result ?? new List<BillingOutboxEventDto>();
    }

    public async Task<bool> AckAsync(long outboxEventId, string consumer, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/outbox/{outboxEventId}/ack", new
        {
            consumer
        }, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public sealed record BillingOutboxEventDto(
        long OutboxEventId,
        string EventName,
        DateTime OccurredAtUtc,
        string Source,
        string? CorrelationId,
        JsonElement Payload);
}
