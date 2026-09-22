namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class ReservationPreOrder
{
    public int PreOrderId { get; set; }
    public int ReservationId { get; set; }
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string? SubmitIdempotencyKey { get; set; }
    public string? SubmitRequestHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int? ConvertedOrderId { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public ICollection<ReservationPreOrderItem> Items { get; set; } = new List<ReservationPreOrderItem>();
}
