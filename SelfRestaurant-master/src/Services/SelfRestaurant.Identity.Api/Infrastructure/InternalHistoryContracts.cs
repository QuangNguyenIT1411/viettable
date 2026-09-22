namespace SelfRestaurant.Identity.Api.Infrastructure;

public sealed record ChefHistoryAggregateResponse(
    int OrderId,
    string? OrderCode,
    DateTime OrderTime,
    DateTime? CompletedTime,
    string? TableName,
    string? BranchName,
    string StatusCode,
    string StatusName,
    string DishesSummary,
    string? Notes);

public sealed record CashierHistoryAggregateResponse(
    int BillId,
    string BillCode,
    DateTime BillTime,
    string? OrderCode,
    string? TableName,
    string? CustomerName,
    decimal Subtotal,
    decimal Discount,
    decimal PointsDiscount,
    int? PointsUsed,
    decimal TotalAmount,
    string PaymentMethod,
    decimal? PaymentAmount,
    decimal? ChangeAmount);

public sealed record CashierHistoryPagedResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<CashierHistoryAggregateResponse> Items);

public sealed record ChefActivityLogItem(
    long AuditId,
    DateTime TimestampUtc,
    string ActionType,
    int? DishId,
    string? ActorName,
    string? AfterState);

public sealed record ChefActivityLogsPagedResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<ChefActivityLogItem> Logs);

public sealed record ChefCookingHistoryPagedResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<ChefHistoryAggregateResponse> Items);

public sealed record ChefItemCompletionItem(
    long AuditId,
    DateTime TimestampUtc,
    int? OrderId,
    string? OrderCode,
    string? TableName,
    int? DishId,
    string DishName,
    int Quantity,
    string? AfterState);

public sealed record ChefItemCompletionsPagedResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<ChefItemCompletionItem> Logs);

public sealed record CustomerOrderHistoryItem(
    int OrderId,
    string? OrderCode,
    DateTime OrderTime,
    DateTime? CompletedTime,
    string? TableName,
    string StatusCode,
    string StatusName,
    decimal TotalAmount,
    int ItemCount,
    string DishesSummary);

public sealed record CustomerOrderHistoryPagedResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<CustomerOrderHistoryItem> Items);
