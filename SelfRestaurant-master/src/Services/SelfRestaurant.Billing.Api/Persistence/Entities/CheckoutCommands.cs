namespace SelfRestaurant.Billing.Api.Persistence.Entities;

public sealed class CheckoutCommands
{
    public long CheckoutCommandId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public int OrderId { get; set; }
    public string? DiningSessionCode { get; set; }
    public int? BillId { get; set; }
    public string? BillCode { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? ChangeAmount { get; set; }
    public int? PointsUsed { get; set; }
    public int? PointsEarned { get; set; }
    public int? CustomerPoints { get; set; }
    public int? PointsBefore { get; set; }
    public string? CustomerName { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
}
