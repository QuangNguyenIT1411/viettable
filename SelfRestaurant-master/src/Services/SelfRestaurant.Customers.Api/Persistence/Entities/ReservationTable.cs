namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public sealed class ReservationTable
{
    public int ReservationTableId { get; set; }
    public int ReservationId { get; set; }
    public int TableId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
