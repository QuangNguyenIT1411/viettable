namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class DiningSession
{
    public int DiningSessionId { get; set; }
    public string SessionCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int TableId { get; set; }
    public int? ReservationId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string CheckInIdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int? ConvertedOrderId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? PaymentRequestedAtUtc { get; set; }
    public int? CheckoutQuoteId { get; set; }
    public int? PaymentId { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
