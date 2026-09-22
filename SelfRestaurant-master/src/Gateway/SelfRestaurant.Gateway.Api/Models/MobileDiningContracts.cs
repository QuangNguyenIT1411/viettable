namespace SelfRestaurant.Gateway.Api.Models;

public sealed record MobileOrderItemRequest(int DishId, int Quantity, string? Note = null);
public sealed record MobileCreateOrderRequest(IReadOnlyList<MobileOrderItemRequest>? Items, string? Note = null);
public sealed record InternalMobileCreateOrderRequest(int CustomerId, string IdempotencyKey, string RequestHash,
    IReadOnlyList<MobileOrderItemRequest>? Items, string? Note);
public sealed record MobileOrderItemResponse(int ItemId, int DishId, string DishName, int Quantity,
    decimal UnitPrice, decimal LineTotal, string? Note, string Status);
public sealed record MobileOrderResponse(int OrderId, string? OrderCode, int? DiningSessionId,
    string? DiningSessionCode, int? TableId, string Status, DateTime OrderTime, DateTime? CompletedTime,
    decimal Subtotal, IReadOnlyList<MobileOrderItemResponse> Items);
public sealed record MobileOrderTrackingResponse(MobileOrderResponse Order,
    IReadOnlyDictionary<string, int> StatusCounts, DateTime RefreshedAtUtc);

public sealed record MobileCreateServiceRequest(string RequestType, string? Note = null);
public sealed record InternalCreateServiceRequest(int CustomerId, string RequestType, string? Note,
    string IdempotencyKey, string RequestHash);
public sealed record ServiceRequestResponse(int ServiceRequestId, int DiningSessionId, string RequestType,
    string? Note, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? CancelledAtUtc);
