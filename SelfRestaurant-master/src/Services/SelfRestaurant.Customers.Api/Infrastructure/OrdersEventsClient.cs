using System.Net.Http.Json;
using System.Text.Json;

namespace SelfRestaurant.Customers.Api.Infrastructure;

public sealed class OrdersEventsClient
{
    private readonly HttpClient _http;

    public OrdersEventsClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<OrdersOutboxEventDto>> GetPendingReadyEventsAsync(int take, CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<List<OrdersOutboxEventDto>>(
            $"/api/internal/outbox/pending?eventName=order.status-ready.v1&take={Math.Clamp(take, 1, 100)}",
            cancellationToken);

        return result ?? new List<OrdersOutboxEventDto>();
    }

    public async Task<bool> AckAsync(long outboxEventId, string consumer, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/outbox/{outboxEventId}/ack", new
        {
            consumer
        }, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public sealed record OrdersOutboxEventDto(
        long OutboxEventId,
        string EventName,
        DateTime OccurredAtUtc,
        string Source,
        string? CorrelationId,
        JsonElement Payload);
}
