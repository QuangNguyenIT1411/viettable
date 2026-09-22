namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class BusinessAuditLogs
{
    public long BusinessAuditLogId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? ActorType { get; set; }
    public int? ActorId { get; set; }
    public string? ActorCode { get; set; }
    public string? ActorName { get; set; }
    public string? ActorRoleCode { get; set; }
    public int? TableId { get; set; }
    public int? OrderId { get; set; }
    public int? OrderItemId { get; set; }
    public int? DishId { get; set; }
    public int? BillId { get; set; }
    public string? DiningSessionCode { get; set; }
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Notes { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
}
