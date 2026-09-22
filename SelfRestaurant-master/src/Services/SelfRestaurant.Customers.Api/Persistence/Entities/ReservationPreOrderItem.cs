namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public sealed class ReservationPreOrderItem
{
    public int ReservationItemId { get; set; }
    public int ReservationId { get; set; }
    public int DishId { get; set; }
    public string DishNameSnapshot { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
