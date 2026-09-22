namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class OutboxEvents
{
    public long OutboxEventId { get; set; }
    public string EventName { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public string Source { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
}
