namespace SelfRestaurant.Billing.Api.Infrastructure;

public sealed record CashierOrderAggregateResponse(
    int OrderId,
    string? OrderCode,
    DateTime OrderTime,
    int TableId,
    string TableName,
    int? CustomerId,
    string StatusCode,
    string StatusName,
    decimal Subtotal,
    int ItemCount,
    IReadOnlyList<CashierOrderItemAggregateResponse> Items);

public sealed record CashierOrderItemAggregateResponse(
    int ItemId,
    int OrderId,
    int DishId,
    string DishName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Image,
    string? Note,
    string StatusCode);

public sealed record CheckoutContextResponse(
    int OrderId,
    string? OrderCode,
    string? DiningSessionCode,
    int? TableId,
    string? TableName,
    int? BranchId,
    string? BranchName,
    int? CustomerId,
    string StatusCode,
    string StatusName,
    bool IsActive,
    decimal Subtotal);
