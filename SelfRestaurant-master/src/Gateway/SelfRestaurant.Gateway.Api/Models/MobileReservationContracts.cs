namespace SelfRestaurant.Gateway.Api.Models;

public sealed record MobileBranchDetailResponse(int BranchId, string Name, string? Location,
    int ActiveTableCount, int MinSeats, int MaxSeats, string? Image = null);
public sealed record MobileNearbyBranchResponse(int BranchId, string Name, string? Location, double? DistanceKm, string? Image = null);
public sealed record MobileAreaResponse(string AreaCode, string Name, int TableCount);
public sealed record MobileTableLayoutResponse(int BranchId, string BranchName, string AreaCode,
    IReadOnlyList<MobileTableResponse> Tables);
public sealed record MobileTableResponse(int TableId, int TableNumber, int NumberOfSeats,
    string AreaCode, bool PhysicallyAvailable, bool ReservationAvailable);
public sealed record MobileAvailabilityResponse(int BranchId, DateTimeOffset ReservedAt, int PartySize,
    int DurationMinutes, IReadOnlyList<MobileTableResponse> AvailableTables,
    IReadOnlyList<MobileAvailabilitySuggestion> Suggestions,
    IReadOnlyList<MobileTableMapItem>? Tables = null);
/// <summary>
/// One table of the branch on the booking map (reservation item 5): Status AVAILABLE, BOOKED, TOO_SMALL, IN_USE or
/// INACTIVE with its Vietnamese label. Never says who booked it.
/// </summary>
public sealed record MobileTableMapItem(int TableId, int TableNumber, int NumberOfSeats, string Status, string StatusLabel);
public sealed record MobileAvailabilitySuggestion(DateTimeOffset ReservedAt, int AvailableTableCount);
public sealed record MobileQrContextResponse(int TableId, int BranchId, int TableNumber);

public sealed record CreateMobileReservationRequest(int BranchId, int? TableId, int PartySize,
    DateTimeOffset ReservedAt, string? Note = null, string? ContactName = null, string? ContactPhone = null);
public sealed record UpdateMobileReservationRequest(int? TableId, int PartySize,
    DateTimeOffset ReservedAt, string? Note = null);
public sealed record MobileReservationListResponse(IReadOnlyList<ReservationDto> Items, string? NextCursor);
/// <summary>Status: NOT_REQUIRED, PENDING, EXPIRED, CANCELLED, PAID, APPLIED, REFUND_REQUIRED, REFUNDED, FORFEITED.
/// Transfer (VietQR, content "VT DC{id}") only while PENDING.</summary>
public sealed record MobileDepositResponse(int ReservationId, bool Required, long AmountVnd,
    string Status, string PolicyMessage, DateTime? DueAtUtc = null, DateTime? PaidAtUtc = null,
    string? ClosedReason = null, System.Text.Json.JsonElement? Transfer = null, string? ReservationStatus = null);
public sealed record MobilePreOrderItemRequest(int DishId, int Quantity, string? Note = null);
public sealed record MobilePreOrderUpdateRequest(IReadOnlyList<MobilePreOrderItemRequest>? Items);
public sealed record MobilePreOrderResponse(int ReservationId, string Status, decimal Subtotal,
    DateTime? SubmittedAtUtc, IReadOnlyList<MobilePreOrderItemResponse> Items);
public sealed record MobilePreOrderItemResponse(int PreOrderItemId, int DishId, string DishNameSnapshot,
    decimal UnitPriceSnapshot, int Quantity, string? Note);

public sealed record ReservationPreOrderUpsertRequest(int CustomerId, int BranchId,
    IReadOnlyList<MobilePreOrderItemRequest>? Items);
public sealed record ReservationPreOrderSubmitRequest(int CustomerId, string IdempotencyKey, string RequestHash);
public sealed record ReservationPreOrderResponse(int ReservationId, int CustomerId, int BranchId, string Status,
    decimal Subtotal, DateTime? SubmittedAtUtc, IReadOnlyList<MobilePreOrderItemResponse> Items);

public sealed record MobileQrValidationRequest(string QrCode, int? ReservationId = null);
public sealed record MobileQrValidationResponse(string Outcome, int TableId, int BranchId, int TableNumber,
    int? ReservationId, int? ReservedTableId, bool ConfirmationRequired, string Message);
public sealed record MobileReservationCheckInRequest(int ReservationId, string QrCode, bool ConfirmTableMismatch = false);
public sealed record MobileWalkInCheckInRequest(string QrCode);
public sealed record DiningSessionCreateRequest(int CustomerId, int BranchId, int TableId, int? ReservationId,
    string IdempotencyKey, string RequestHash);
public sealed record DiningSessionCustomerRequest(int CustomerId);
public sealed record MobileDiningSessionResponse(int DiningSessionId, string SessionCode, int CustomerId,
    int BranchId, int TableId, int? ReservationId, string Status, int? ConvertedOrderId,
    DateTime StartedAtUtc, DateTime? PaymentRequestedAtUtc, DateTime? EndedAtUtc, bool AlreadyCheckedIn);
