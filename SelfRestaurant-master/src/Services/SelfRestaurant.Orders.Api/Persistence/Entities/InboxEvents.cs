namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class InboxEvents
{
    public long InboxEventId { get; set; }
    public string Source { get; set; } = null!;
    public long SourceEventId { get; set; }
    public string EventName { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public string Status { get; set; } = "PROCESSED";
    public int RetryCount { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
}
