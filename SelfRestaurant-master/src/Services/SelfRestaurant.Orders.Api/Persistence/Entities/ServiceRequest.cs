namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class ServiceRequest
{
    public int ServiceRequestId { get; set; }
    public int DiningSessionId { get; set; }
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int TableId { get; set; }
    public string RequestType { get; set; } = null!;
    public string? Note { get; set; }
    public string Status { get; set; } = "OPEN";
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
