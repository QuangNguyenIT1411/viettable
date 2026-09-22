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
    public ICollection<ReservationPreOrderItem> PreOrderItems { get; set; } = new List<ReservationPreOrderItem>();
    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
