namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class SubmitCommands
{
    public long SubmitCommandId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public int TableId { get; set; }
    public string? DiningSessionCode { get; set; }
    public int? OrderId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
}
