namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public sealed class ReadyDishNotifications
{
    public long ReadyDishNotificationId { get; set; }
    public int OrderId { get; set; }
    public int? OrderItemId { get; set; }
    public int? DishId { get; set; }
    public string? DishName { get; set; }
    public int? CustomerId { get; set; }
    public int? TableId { get; set; }
    public string EventName { get; set; } = "order.status-ready.v1";
    public string Message { get; set; } = null!;
    public string Status { get; set; } = "OPEN";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
