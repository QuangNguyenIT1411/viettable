using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1/reservations")]
public sealed class MobileReservationsGatewayController : ControllerBase
{
    private readonly CustomersClient _customers;
    private readonly CatalogClient _catalog;
    private readonly IdentityClient _identity;
    private readonly OrdersClient _orders;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileReservationsGatewayController> _logger;

    public MobileReservationsGatewayController(CustomersClient customers, CatalogClient catalog, IdentityClient identity, OrdersClient orders,
        IConfiguration configuration, ILogger<MobileReservationsGatewayController> logger)
    {
        _customers = customers;
        _catalog = catalog;
        _identity = identity;
        _orders = orders;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateMobileReservationRequest request, CancellationToken ct)
    {
        var actorId = CurrentCustomerId();
        if (actorId is null) return MobileError("invalid_actor", "Access token không chứa khách hàng hợp lệ.", 401);
        var key = RequireIdempotencyKey();
        if (key.Result is not null) return key.Result;
        var scopedKey = $"c{actorId}:create:{key.Value}";
        var retry = await _customers.GetReservationByIdempotencyKeyAsync(scopedKey, ct);
        if (retry is not null)
        {
            if (retry.CustomerId != actorId || retry.BranchId != request.BranchId || retry.PartySize != request.PartySize
                // An omitted/nonpositive table requests server selection. Its
                // replay must use the original selection, not compare it to null.
                || (request.TableId is > 0 && retry.TableId != request.TableId)
                || Math.Abs((retry.ReservedAt - request.ReservedAt.UtcDateTime).TotalSeconds) > 1
                || !string.Equals(retry.Note?.Trim(), request.Note?.Trim(), StringComparison.Ordinal))
                return MobileError("idempotency_conflict", "Idempotency key đã được dùng với dữ liệu khác.", 409);
            return Ok(retry);
        }

        var validation = ValidateSchedule(request.PartySize, request.ReservedAt);
        if (validation is not null) return validation;
        var available = await FindAvailableTables(request.BranchId, request.ReservedAt, request.PartySize, null, ct);
        if (available.Layout is null) return MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404);
        var table = request.TableId is > 0
            ? available.Available.FirstOrDefault(x => x.TableId == request.TableId.Value)
            : available.Available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();
        if (table is null)
            return MobileError("reservation_slot_unavailable", "Khung giờ đã hết bàn phù hợp.", 409,
                new { suggestions = await BuildSuggestions(request.BranchId, request.ReservedAt, request.PartySize, null, ct) });

        var profile = await _identity.GetMobileProfileAsync(ct);
        if (profile is null) return MobileError("customer_not_found", "Không tìm thấy hồ sơ khách hàng.", 404);
        var phone = NormalizeReservationPhone(profile.PhoneNumber);
        if (phone is null) return MobileError("profile_phone_invalid", "Hãy cập nhật số điện thoại Việt Nam hợp lệ trước khi đặt bàn.", 422);
        var hash = Hash(new { request.BranchId, TableId = table.TableId, request.PartySize, ReservedAt = request.ReservedAt.UtcDateTime, Note = request.Note?.Trim() });
        try
        {
            var created = await _customers.CreateReservationAsync(new CreateReservationApiRequest(
                profile.Name, phone, actorId, request.BranchId, table.TableId, request.PartySize,
                request.ReservedAt.UtcDateTime, request.Note, scopedKey, hash, "Confirmed"), ct);
            return created is null ? MobileError("create_reservation_failed", "Không thể tạo đặt bàn.", 502) : Ok(created);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "create_reservation_failed"); }
    }

    [HttpGet]
    public async Task<ActionResult<MobileReservationListResponse>> List(
        [FromQuery] string? scope, [FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var actorId = CurrentCustomerId();
        if (actorId is null) return MobileError("invalid_actor", "Access token không hợp lệ.", 401);
        if (limit is < 1 or > 100) return MobileError("validation_failed", "limit phải từ 1 đến 100.", 400);
        var cursorId = DecodeCursor(cursor);
        if (cursor is not null && cursorId is null) return MobileError("invalid_cursor", "Cursor không hợp lệ.", 400);
        // Customers marks missed reservations NoShow on this read, so "upcoming" is simply the open statuses: a guest
        // a few minutes late (still inside the arrival window) keeps the reservation in "Sắp tới".
        var query = (await _customers.GetCustomerReservationsAsync(actorId.Value, ct)).AsEnumerable();
        if (string.Equals(scope, "upcoming", StringComparison.OrdinalIgnoreCase))
            query = query.Where(IsOpenReservation);
        else if (string.Equals(scope, "history", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !IsOpenReservation(x));
        if (cursorId is > 0) query = query.Where(x => x.ReservationId < cursorId.Value);
        var page = query.OrderByDescending(x => x.ReservationId).Take(limit + 1).ToArray();
        var hasMore = page.Length > limit;
        var items = page.Take(limit).ToArray();
        return Ok(new MobileReservationListResponse(items,
            hasMore && items.Length > 0 ? EncodeCursor(items[^1].ReservationId) : null));
    }

    [HttpGet("{reservationId:int}")]
    public async Task<ActionResult<ReservationDto>> Get(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        return owned.Error ?? Ok(owned.Reservation!);
    }

    [HttpPut("{reservationId:int}")]
    public async Task<ActionResult<ReservationDto>> Update(int reservationId, UpdateMobileReservationRequest request, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        var key = RequireIdempotencyKey();
        if (key.Result is not null) return key.Result;
        var validation = ValidateSchedule(request.PartySize, request.ReservedAt);
        if (validation is not null) return validation;
        var available = await FindAvailableTables(owned.Reservation!.BranchId, request.ReservedAt, request.PartySize, reservationId, ct);
        // Keep the guest's table when it still fits the new time and party; otherwise move them to the best free one
        // (more guests used to fail with "hết bàn" although other tables were free).
        var table = (request.TableId is > 0 ? available.Available.FirstOrDefault(x => x.TableId == request.TableId.Value) : null)
            ?? available.Available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();
        if (table is null)
            return MobileError("reservation_slot_unavailable", "Khung giờ đã hết bàn phù hợp.", 409,
                new { suggestions = await BuildSuggestions(owned.Reservation.BranchId, request.ReservedAt, request.PartySize, reservationId, ct) });
        var hash = Hash(new { TableId = table.TableId, request.PartySize, ReservedAt = request.ReservedAt.UtcDateTime, Note = request.Note?.Trim() });
        try
        {
            var updated = await _customers.UpdateReservationAsync(reservationId,
                new UpdateReservationInternalRequest(table.TableId, request.PartySize, request.ReservedAt.UtcDateTime,
                    request.Note, $"c{CurrentCustomerId()}:update:{key.Value}", hash), ct);
            return updated is null ? MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404) : Ok(updated);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "update_reservation_failed"); }
    }

    [HttpPost("{reservationId:int}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        try
        {
            var result = await _customers.CancelReservationAsync(reservationId, ct);
            if (result is null) return MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            // The dishes ordered ahead must never reach the kitchen. Best effort: a cancelled reservation cannot
            // check in, so a missed call only leaves an unused pre-order row.
            try { await _orders.CancelReservationPreOrderAsync(reservationId, ct); }
            catch (Exception ex) { _logger.LogWarning("Pre-order cancel skipped for reservation {ReservationId} ({ErrorType}).", reservationId, ex.GetType().Name); }
            return Ok(result);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "cancel_reservation_failed"); }
    }

    [HttpGet("{reservationId:int}/deposit")]
    public async Task<ActionResult<MobileDepositResponse>> GetDeposit(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        return Ok(new MobileDepositResponse(reservationId, false, 0, "NOT_REQUIRED",
            "Chi nhánh hiện chưa yêu cầu đặt cọc cho đặt bàn này."));
    }

    [HttpGet("{reservationId:int}/preorder")]
    public async Task<ActionResult<MobilePreOrderResponse>> GetPreOrder(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        var preOrder = await _orders.GetReservationPreOrderAsync(reservationId, ct);
        return Ok(preOrder is null
            ? new MobilePreOrderResponse(reservationId, "DRAFT", 0, null, Array.Empty<MobilePreOrderItemResponse>())
            : MapPreOrder(preOrder));
    }

    [HttpPut("{reservationId:int}/preorder")]
    public async Task<ActionResult<MobilePreOrderResponse>> UpdatePreOrder(
        int reservationId, MobilePreOrderUpdateRequest request, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        if (!string.Equals(owned.Reservation!.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return MobileError("reservation_state_conflict", "Chỉ đặt bàn đã xác nhận mới được đặt món trước.", 409);
        var requested = request.Items ?? Array.Empty<MobilePreOrderItemRequest>();
        if (requested.Count > 50 || requested.Any(x => x.DishId <= 0 || x.Quantity is < 1 or > 50))
            return MobileError("validation_failed", "Danh sách món đặt trước không hợp lệ.", 400);
        try
        {
            var saved = await _orders.UpsertReservationPreOrderAsync(reservationId,
                new ReservationPreOrderUpsertRequest(CurrentCustomerId()!.Value, owned.Reservation.BranchId, requested), ct);
            return saved is null ? MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404) : Ok(MapPreOrder(saved));
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "update_preorder_failed"); }
    }

    [HttpPost("{reservationId:int}/preorder/submit")]
    public async Task<ActionResult<MobilePreOrderResponse>> SubmitPreOrder(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        var key = RequireIdempotencyKey();
        if (key.Result is not null) return key.Result;
        var draft = await _orders.GetReservationPreOrderAsync(reservationId, ct);
        if (draft is null) return MobileError("preorder_empty", "Giỏ món đặt trước đang trống.", 422);
        var hash = Hash(draft.Items.Select(x => new { x.DishId, x.Quantity, x.UnitPriceSnapshot, x.Note }).ToArray());
        try
        {
            var saved = await _orders.SubmitReservationPreOrderAsync(reservationId,
                new ReservationPreOrderSubmitRequest(CurrentCustomerId()!.Value,
                    $"c{CurrentCustomerId()}:preorder:{key.Value}", hash), ct);
            return saved is null ? MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404) : Ok(MapPreOrder(saved));
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "submit_preorder_failed"); }
    }

    private async Task<(BranchTablesResponse? Layout, IReadOnlyList<BranchTableDto> Available)> FindAvailableTables(
        int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct)
    {
        var layout = await _catalog.GetBranchTablesAsync(branchId, ct);
        if (layout is null) return (null, Array.Empty<BranchTableDto>());
        var occupied = (await _customers.GetOccupiedTableIdsAsync(branchId, at.UtcDateTime,
            at.AddHours(2).UtcDateTime, excludeReservationId, ct)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        return (layout, layout.Tables.Where(x => ReservationRules.TableBookableAt(x, at, now)
            && x.NumberOfSeats >= partySize && !occupied.Contains(x.TableId)).ToArray());
    }

    private async Task<IReadOnlyList<MobileAvailabilitySuggestion>> BuildSuggestions(
        int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct)
    {
        var suggestions = new List<MobileAvailabilitySuggestion>();
        foreach (var offset in new[] { -60, -30, 30, 60 })
        {
            var candidate = at.AddMinutes(offset);
            if (candidate <= DateTimeOffset.UtcNow.AddMinutes(-5)) continue;
            var result = await FindAvailableTables(branchId, candidate, partySize, excludeReservationId, ct);
            if (result.Available.Count > 0) suggestions.Add(new MobileAvailabilitySuggestion(candidate, result.Available.Count));
        }
        return suggestions;
    }

    private async Task<(ReservationDto? Reservation, ObjectResult? Error)> GetOwned(int reservationId, CancellationToken ct)
    {
        if (reservationId <= 0) return (null, MobileError("validation_failed", "Đặt bàn không hợp lệ.", 400));
        var reservation = await _customers.GetReservationByIdAsync(reservationId, ct);
        if (reservation is null) return (null, MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404));
        return reservation.CustomerId == CurrentCustomerId()
            ? (reservation, null)
            : (null, MobileError("forbidden", "Bạn không có quyền truy cập đặt bàn này.", 403));
    }

    private static bool IsOpenReservation(ReservationDto x) =>
        x.Status is not null && new[] { "Pending", "Confirmed", "CheckingIn" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase);

    private ObjectResult? ValidateSchedule(int partySize, DateTimeOffset at)
    {
        if (partySize is < 1 or > 30)
            return MobileError("validation_failed", "Số khách phải từ 1 đến 30.", 400);
        if (at <= DateTimeOffset.UtcNow.AddMinutes(14))
            return MobileError("validation_failed", "Thời gian đặt bàn phải cách hiện tại ít nhất 15 phút.", 400);
        if (at > DateTimeOffset.UtcNow.AddDays(90))
            return MobileError("validation_failed", "Chỉ có thể đặt bàn trước tối đa 90 ngày.", 400);
        var openFrom = ReservationRules.ConfiguredTime(_configuration, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom);
        var lastSlot = ReservationRules.ConfiguredTime(_configuration, "Reservations:LastSlot", ReservationRules.DefaultLastSlot);
        if (!ReservationRules.WithinBookingHours(at, openFrom, lastSlot))
            return MobileError("outside_booking_hours",
                $"Nhà hàng nhận đặt bàn từ {openFrom:HH:mm} đến {lastSlot:HH:mm}. Vui lòng chọn giờ khác.", 400);
        return null;
    }

    private ActionResult<string> RequireIdempotencyKey()
    {
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(key) || key.Length > 64
            ? MobileError("idempotency_key_required", "Idempotency-Key là bắt buộc và tối đa 64 ký tự.", 400)
            : key;
    }

    private int? CurrentCustomerId() =>
        int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;

    private static string? NormalizeReservationPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11) digits = "0" + digits[2..];
        return digits.Length == 10 && digits[0] == '0' ? digits : null;
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static string EncodeCursor(int id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture)));
    private static int? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try { return int.TryParse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var id) && id > 0 ? id : null; }
        catch { return null; }
    }

    private static MobilePreOrderResponse MapPreOrder(ReservationPreOrderResponse preOrder) => new(
        preOrder.ReservationId, preOrder.Status, preOrder.Subtotal, preOrder.SubmittedAtUtc, preOrder.Items);

    private ObjectResult DownstreamError(ApiClientException ex, string fallbackCode) =>
        MobileError(ex.Code ?? fallbackCode, ex.Message, ex.StatusCode, ParseDetails(ex.ResponseBody));
    private static object? ParseDetails(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.TryGetProperty("details", out var value) ? value.Clone() : null; }
        catch { return null; }
    }
    private ObjectResult MobileError(string code, string message, int status, object? details = null)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, details));
    }
}
