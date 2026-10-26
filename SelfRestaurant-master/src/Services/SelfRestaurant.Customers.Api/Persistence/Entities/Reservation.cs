namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public sealed class Reservation
{
    public int ReservationId { get; set; }
    public string ReservationCode { get; set; } = null!;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public int BranchId { get; set; }
    public int? TableId { get; set; }
    public int PartySize { get; set; }
    public DateTime ReservedAt { get; set; }
    public int ArrivalWindowMinutes { get; set; } = 30;
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public int? ConvertedOrderId { get; set; }
    public string? DiningSessionCode { get; set; }
    public DateTime? CheckInStartedAtUtc { get; set; }
    public string? CheckInIdempotencyKey { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public int? CheckedInByEmployeeId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }
    public string? LastMutationKey { get; set; }
    public string? LastMutationHash { get; set; }
    public DateTime? PreOrderSubmittedAtUtc { get; set; }
    /// <summary>When the restaurant confirmed/rejected the booking or marked the guest absent (not the check-in itself).</summary>
    public DateTime? StatusChangedAtUtc { get; set; }
    /// <summary>The cashier who made that decision; null when the system did (e.g. auto-confirm at a QR check-in).</summary>
    public int? StatusChangedByEmployeeId { get; set; }
    /// <summary>The rejection reason shown to the guest, or how the booking was confirmed ("Tự xác nhận khi check-in").</summary>
    public string? StatusReason { get; set; }
    public ICollection<ReservationPreOrderItem> PreOrderItems { get; set; } = new List<ReservationPreOrderItem>();
    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
