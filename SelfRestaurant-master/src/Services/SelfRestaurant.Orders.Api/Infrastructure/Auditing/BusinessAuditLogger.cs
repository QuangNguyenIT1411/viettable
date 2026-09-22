using System.Text.Json;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;

namespace SelfRestaurant.Orders.Api.Infrastructure.Auditing;

public sealed class BusinessAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrdersDbContext _db;
    private readonly RequestActorContextAccessor _actorContextAccessor;

    public BusinessAuditLogger(OrdersDbContext db, RequestActorContextAccessor actorContextAccessor)
    {
        _db = db;
        _actorContextAccessor = actorContextAccessor;
    }

    public void Add(
        string actionType,
        string entityType,
        string entityId,
        object? beforeState = null,
        object? afterState = null,
        string? notes = null,
        int? tableId = null,
        int? orderId = null,
        int? orderItemId = null,
        int? dishId = null,
        int? billId = null,
        string? diningSessionCode = null,
        string? idempotencyKey = null,
        RequestActorContext? actorOverride = null)
    {
        var actor = actorOverride ?? _actorContextAccessor.GetCurrent();
        // Keep audit payloads concise and business-focused so retries/conflicts
        // can be investigated without storing raw request bodies or secrets.
        _db.BusinessAuditLogs.Add(new BusinessAuditLogs
        {
            CreatedAtUtc = DateTime.UtcNow,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            ActorType = actor.ActorType,
            ActorId = actor.ActorId,
            ActorCode = actor.ActorCode,
            ActorName = actor.ActorName,
            ActorRoleCode = actor.ActorRoleCode,
            TableId = tableId,
            OrderId = orderId,
            OrderItemId = orderItemId,
            DishId = dishId,
            BillId = billId,
            DiningSessionCode = diningSessionCode,
            CorrelationId = actor.CorrelationId,
            IdempotencyKey = idempotencyKey,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BeforeState = Serialize(beforeState),
            AfterState = Serialize(afterState)
        });
    }

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
