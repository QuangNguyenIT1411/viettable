namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class ReservationPreOrderItem
{
    public int PreOrderItemId { get; set; }
    public int PreOrderId { get; set; }
    public int DishId { get; set; }
    public string DishNameSnapshot { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public ReservationPreOrder PreOrder { get; set; } = null!;
}
