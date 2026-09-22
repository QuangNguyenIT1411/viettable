namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class Orders
{
    public int OrderID { get; set; }
    public string? DiningSessionCode { get; set; }
    public string? SubmitIdempotencyKey { get; set; }
    public string? SubmitRequestHash { get; set; }
    public string? OrderCode { get; set; }
    public DateTime OrderTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string? Note { get; set; }
    public bool? IsActive { get; set; }
    public int? TableID { get; set; }
    public int? CustomerID { get; set; }
    public int StatusID { get; set; }
    public int? CashierID { get; set; }
    public OrderStatus Status { get; set; } = null!;
}
