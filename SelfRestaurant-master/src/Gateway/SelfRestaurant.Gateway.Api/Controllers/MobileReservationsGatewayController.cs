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
    private readonly SelfRestaurant.Gateway.Api.Infrastructure.CustomerInboxNotifier _inbox;
    private readonly SelfRestaurant.Gateway.Api.Infrastructure.StaffBranchEvents _staffEvents;
    private readonly ReservationDepositCoordinator _deposits;

    public MobileReservationsGatewayController(CustomersClient customers, CatalogClient catalog, IdentityClient identity, OrdersClient orders,
        IConfiguration configuration, ILogger<MobileReservationsGatewayController> logger,
        SelfRestaurant.Gateway.Api.Infrastructure.CustomerInboxNotifier inbox,
        SelfRestaurant.Gateway.Api.Infrastructure.StaffBranchEvents staffEvents,
        ReservationDepositCoordinator deposits)
    {
        _deposits = deposits;
        _inbox = inbox;
        _staffEvents = staffEvents;
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
        // The contact typed on the booking screen wins over the profile (it used to be ignored, 25/10).
        var contactPhone = ReservationRules.NormalizePhone(request.ContactPhone);
        if (!string.IsNullOrWhiteSpace(request.ContactPhone) && contactPhone is null)
            return MobileError("contact_phone_invalid",
                "Số điện thoại liên hệ không hợp lệ. Nhập 10 số bắt đầu bằng 0 (ví dụ 0343000273) hoặc +84.", 400,
                new { field = "contactPhone" });
        var retry = await _customers.GetReservationByIdempotencyKeyAsync(scopedKey, ct);
        if (retry is not null)
        {
            if (retry.CustomerId != actorId || retry.BranchId != request.BranchId || retry.PartySize != request.PartySize
                // An omitted/nonpositive table requests server selection. Its
                // replay must use the original selection, not compare it to null.
                || (request.TableId is > 0 && retry.TableId != request.TableId)
                || Math.Abs((retry.ReservedAt - request.ReservedAt.UtcDateTime).TotalSeconds) > 1
                || !string.Equals(retry.Note?.Trim(), request.Note?.Trim(), StringComparison.Ordinal)
                || (contactPhone is not null && retry.PhoneNumber != contactPhone))
                return MobileError("idempotency_conflict", "Idempotency key đã được dùng với dữ liệu khác.", 409);
            return Ok(retry);
        }

        var validation = ValidateSchedule(request.PartySize, request.ReservedAt);
        if (validation is not null) return validation;
        var seatingResult = await FindSeating(request.BranchId, request.ReservedAt, request.PartySize, null, ct);
        var available = (seatingResult.Layout, seatingResult.Available);
        if (available.Layout is null) return MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404);
        var chosenGroup = (request.TableIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToArray();
        BranchTableDto? table;
        IReadOnlyList<int> others = Array.Empty<int>();
        if (chosenGroup.Length >= 2)
        {
            // A group the guest took on the map (owner 06/11): still free, same area, joined, enough seats.
            var group = chosenGroup.Select(id => seatingResult.Free?.FirstOrDefault(x => x.TableId == id)).ToArray();
            if (group.Any(x => x is null)) return TableTaken(available.Layout, chosenGroup.First(id => group[Array.IndexOf(chosenGroup, id)] is null));
            if (!TableCombiner.IsValidGroup(group!, request.PartySize))
                return MobileError("table_group_invalid", "Các bàn này không ghép được cho số khách đã chọn. Vui lòng chọn lại trên sơ đồ.", 409);
            var ordered = group!.OrderBy(x => x!.DisplayTableNumber).ToArray();
            table = ordered[0];
            others = ordered.Skip(1).Select(x => x!.TableId).ToArray();
        }
        else
        {
            table = request.TableId is > 0
                ? available.Available.FirstOrDefault(x => x.TableId == request.TableId.Value)
                : available.Available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();
            // The guest picked this table on the map but someone else got it first (or it just became unusable).
            if (table is null && request.TableId is > 0) return TableTaken(available.Layout, request.TableId.Value);
            // No single table seats the party: the best group of pushed-together tables (owner 06/11).
            if (table is null && seatingResult.Combination is { Count: > 1 } best)
            {
                var ordered = best.OrderBy(x => x.DisplayTableNumber).ToArray();
                table = ordered[0];
                others = ordered.Skip(1).Select(x => x.TableId).ToArray();
            }
        }
        if (table is null)
            return await NoTableError(request.BranchId, request.ReservedAt, request.PartySize, null, null, null, ct);

        var profile = await _identity.GetMobileProfileAsync(ct);
        if (profile is null) return MobileError("customer_not_found", "Không tìm thấy hồ sơ khách hàng.", 404);
        var phone = contactPhone ?? ReservationRules.NormalizePhone(profile.PhoneNumber);
        if (phone is null)
            return MobileError("contact_phone_required",
                "Vui lòng nhập số điện thoại liên hệ (số trong hồ sơ của bạn chưa hợp lệ).", 422, new { field = "contactPhone" });
        var contactName = string.IsNullOrWhiteSpace(request.ContactName) ? profile.Name : request.ContactName.Trim();
        if (contactName.Length > 100) contactName = contactName[..100];
        var hash = others.Count == 0
            ? Hash(new { request.BranchId, TableId = table.TableId, request.PartySize, ReservedAt = request.ReservedAt.UtcDateTime, Note = request.Note?.Trim() })
            : Hash(new { request.BranchId, TableId = table.TableId, Others = others, request.PartySize, ReservedAt = request.ReservedAt.UtcDateTime, Note = request.Note?.Trim() });
        // From the threshold on (feature flag on), the booking waits for its deposit; unpaid after 15 minutes it is
        // cancelled and the table freed (docs/DEPOSIT_PLAN.md).
        var depositAmount = _deposits.RequiredAmount(request.PartySize);
        try
        {
            // Pending until the cashier confirms (or the guest checks in); the table is held meanwhile.
            var created = await _customers.CreateReservationAsync(new CreateReservationApiRequest(
                contactName, phone, actorId, request.BranchId, table.TableId, request.PartySize,
                request.ReservedAt.UtcDateTime, request.Note, scopedKey, hash,
                depositAmount > 0 ? SelfRestaurant.Deposits.DepositRules.ReservationPendingDeposit : null,
                depositAmount > 0 ? depositAmount : null,
                depositAmount > 0 ? SelfRestaurant.Deposits.DepositRules.DueAt(DateTime.UtcNow, _deposits.Options) : null,
                others.Count > 0 ? others : null), ct);
            if (created is null) return MobileError("create_reservation_failed", "Không thể tạo đặt bàn.", 502);
            // Best effort: a missed Billing row is created by the app's first deposit read.
            try { await _deposits.EnsureCreatedAsync(created, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogWarning("Deposit row deferred for reservation {ReservationId} ({ErrorType}).", created.ReservationId, ex.GetType().Name); }
            await _inbox.PushAsync(actorId, ct); // Customers wrote "Đặt bàn thành công"
            await NudgeCashierAsync(created.BranchId);
            return Ok(created);
        }
        catch (ApiClientException ex) when (ex.StatusCode == 409 && ex.Code is "reservation_slot_unavailable" or "command_in_progress")
        {
            // Two guests confirmed the same table at the same moment; Customers let only the first one in.
            return TableTaken(available.Layout, table.TableId);
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
        // (more guests used to fail with "hết bàn" although other tables were free). A table with enough seats that
        // is no longer free was taken by someone else after the guest chose it on the map: say so.
        var requested = request.TableId is > 0 ? available.Layout?.Tables.FirstOrDefault(x => x.TableId == request.TableId.Value) : null;
        if (requested is not null && requested.NumberOfSeats >= request.PartySize
            && available.Available.All(x => x.TableId != requested.TableId))
            return TableTaken(available.Layout, requested.TableId);
        var table = (request.TableId is > 0 ? available.Available.FirstOrDefault(x => x.TableId == request.TableId.Value) : null)
            ?? available.Available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();
        IReadOnlyList<int> updateOthers = Array.Empty<int>();
        if (table is null)
        {
            // A larger party may still fit on pushed-together tables (owner 06/11).
            var seatingResult = await FindSeating(owned.Reservation.BranchId, request.ReservedAt, request.PartySize, reservationId, ct);
            if (seatingResult.Combination is { Count: > 1 } best)
            {
                var ordered = best.OrderBy(x => x.DisplayTableNumber).ToArray();
                table = ordered[0];
                updateOthers = ordered.Skip(1).Select(x => x.TableId).ToArray();
            }
        }
        if (table is null)
            return await NoTableError(owned.Reservation.BranchId, request.ReservedAt, request.PartySize, reservationId, null, null, ct,
                includeOtherBranches: false);
        var hash = Hash(new { TableId = table.TableId, request.PartySize, ReservedAt = request.ReservedAt.UtcDateTime, Note = request.Note?.Trim() });
        try
        {
            var updated = await _customers.UpdateReservationAsync(reservationId,
                new UpdateReservationInternalRequest(table.TableId, request.PartySize, request.ReservedAt.UtcDateTime,
                    request.Note, $"c{CurrentCustomerId()}:update:{key.Value}", hash, updateOthers.Count > 0 ? updateOthers : null), ct);
            if (updated is null) return MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            await _inbox.PushAsync(CurrentCustomerId(), ct);
            await NudgeCashierAsync(updated.BranchId);
            return Ok(updated);
        }
        catch (ApiClientException ex) when (ex.StatusCode == 409 && ex.Code is "reservation_slot_unavailable" or "command_in_progress")
        {
            return TableTaken(available.Layout, table.TableId);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "update_reservation_failed"); }
    }

    /// <summary>
    /// The two buttons on the booking reminder: "will_come" keeps the booking and tells the cashier; "cancel" cancels
    /// it under the usual rules (a paid deposit is refunded or kept exactly as with the cancel button).
    /// </summary>
    [HttpPost("{reservationId:int}/arrival-confirmation")]
    public async Task<ActionResult<ReservationDto>> ConfirmArrival(int reservationId, MobileArrivalConfirmationRequest request, CancellationToken ct)
    {
        var answer = request?.Response?.Trim().ToLowerInvariant();
        if (answer == "cancel") return await CancelCore(reservationId, viaReminder: true, ct);
        if (answer != "will_come")
            return MobileError("validation_failed", "Câu trả lời phải là will_come hoặc cancel.", 400, new { field = "response" });
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        try
        {
            var result = await _customers.ConfirmArrivalAsync(reservationId, CurrentCustomerId()!.Value, ct);
            if (result is null) return MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            await NudgeCashierAsync(result.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "arrival_confirmation_failed"); }
    }

    [HttpPost("{reservationId:int}/cancel")]
    public Task<ActionResult<ReservationDto>> Cancel(int reservationId, CancellationToken ct) => CancelCore(reservationId, viaReminder: false, ct);

    private async Task<ActionResult<ReservationDto>> CancelCore(int reservationId, bool viaReminder, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        try
        {
            var result = await _customers.CancelReservationAsync(reservationId, ct, viaReminder);
            if (result is null) return MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            // The dishes ordered ahead must never reach the kitchen. Best effort: a cancelled reservation cannot
            // check in, so a missed call only leaves an unused pre-order row.
            try { await _orders.CancelReservationPreOrderAsync(reservationId, ct); }
            catch (Exception ex) { _logger.LogWarning("Pre-order cancel skipped for reservation {ReservationId} ({ErrorType}).", reservationId, ex.GetType().Name); }
            // Paid deposit: refund by hand when cancelled 2 hours ahead, kept when later; an unpaid one just closes.
            await _deposits.SyncQuietlyAsync(result, ct);
            await _inbox.PushAsync(CurrentCustomerId(), ct);
            // The freed table may be the one a waiting guest needs (owner 06/11).
            await HttpContext.RequestServices.GetRequiredService<WaitlistCoordinator>().ProcessBranchQuietlyAsync(result.BranchId, ct);
            await NudgeCashierAsync(result.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "cancel_reservation_failed"); }
    }

    /// <summary>Deposit rules for the booking screens; the app never assumes an amount.</summary>
    [HttpGet("deposit-policy")]
    public ActionResult<object> DepositPolicy() => Ok(_deposits.Policy());

    /// <summary>The booking's deposit, its VietQR while waiting, and the time left. The app reads it every few seconds
    /// while the guest pays; each read also repairs a step a failed call left out.</summary>
    [HttpGet("{reservationId:int}/deposit")]
    public async Task<ActionResult<MobileDepositResponse>> GetDeposit(int reservationId, CancellationToken ct)
    {
        var owned = await GetOwned(reservationId, ct);
        if (owned.Error is not null) return owned.Error;
        try
        {
            var view = await _deposits.SyncAsync(owned.Reservation!, includeQr: true, ct);
            if (view is null)
                return Ok(new MobileDepositResponse(reservationId, false, 0, "NOT_REQUIRED",
                    "Đặt bàn này không cần đặt cọc.", ReservationStatus: owned.Reservation!.Status));
            var d = view.Deposit;
            return Ok(new MobileDepositResponse(reservationId, true, d.GetProperty("amount").GetInt64(), view.Status,
                DepositMessage(view.Status, _deposits.Options.RefundCutoffHours),
                d.TryGetProperty("dueAtUtc", out var due) && due.ValueKind == JsonValueKind.String ? due.GetDateTime() : null,
                d.TryGetProperty("paidAtUtc", out var paid) && paid.ValueKind == JsonValueKind.String ? paid.GetDateTime() : null,
                ReservationDepositView.Text(d, "closedReason"), view.Transfer, view.Reservation.Status));
        }
        catch (ApiClientException ex) { return DownstreamError(ex, "deposit_unavailable"); }
    }

    private static string DepositMessage(string status, int cutoffHours) => status switch
    {
        "PENDING" => "Chuyển khoản đúng số tiền và nội dung để giữ bàn. Hệ thống tự xác nhận khi nhận được tiền.",
        "EXPIRED" => "Đã quá thời gian đặt cọc nên đặt bàn đã được hủy.",
        "CANCELLED" => "Đặt bàn đã hủy trước khi đặt cọc.",
        "PAID" => $"Đã nhận tiền cọc. Tiền cọc được trừ vào hóa đơn khi dùng bữa; hủy trước giờ đến từ {cutoffHours} giờ được hoàn cọc.",
        "APPLIED" => "Tiền cọc đã được trừ vào hóa đơn.",
        "REFUND_REQUIRED" => "Nhà hàng sẽ hoàn tiền cọc cho bạn.",
        "REFUNDED" => "Nhà hàng đã hoàn tiền cọc.",
        "FORFEITED" => $"Tiền cọc không được hoàn (hủy dưới {cutoffHours} giờ trước giờ đến hoặc không đến).",
        _ => "Đặt bàn có đặt cọc.",
    };

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

    private Task<TableAvailability.Result> FindSeating(int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct) =>
        HttpContext.RequestServices.GetRequiredService<TableAvailability>().FindAsync(branchId, at, partySize, excludeReservationId, ct);

    private async Task<(BranchTablesResponse? Layout, IReadOnlyList<BranchTableDto> Available)> FindAvailableTables(
        int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct)
    {
        var result = await HttpContext.RequestServices.GetRequiredService<TableAvailability>()
            .FindAsync(branchId, at, partySize, excludeReservationId, ct);
        return (result.Layout, result.Available);
    }

    /// <summary>Booking hours (first and last slot, restaurant time) for the booking assistant (owner 06/11).</summary>
    [HttpGet("hours")]
    [AllowAnonymous]
    public ActionResult<object> Hours()
    {
        var openFrom = ReservationRules.ConfiguredTime(_configuration, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom);
        var lastSlot = ReservationRules.ConfiguredTime(_configuration, "Reservations:LastSlot", ReservationRules.DefaultLastSlot);
        return Ok(new { openFrom = openFrom.ToString("HH:mm"), lastSlot = lastSlot.ToString("HH:mm"), slotMinutes = AvailabilitySuggestions.SlotMinutes });
    }

    /// <summary>
    /// "Hết bàn?" (owner 06/11): whether the chosen time still has room, the nearest other times of the same day with
    /// room, and other branches with room at that time (closest first when the guest shares a position).
    /// </summary>
    [HttpGet("availability")]
    public async Task<ActionResult<MobileAlternativesResponse>> Availability([FromQuery] int branchId, [FromQuery] DateTimeOffset reservedAt,
        [FromQuery] int partySize, [FromQuery] double? latitude, [FromQuery] double? longitude,
        [FromQuery] int? excludeReservationId, CancellationToken ct)
    {
        if (CurrentCustomerId() is null) return MobileError("invalid_actor", "Access token không hợp lệ.", 401);
        if (branchId <= 0) return MobileError("validation_failed", "Chi nhánh không hợp lệ.", 400, new { field = "branchId" });
        var validation = ValidateSchedule(partySize, reservedAt);
        if (validation is not null) return validation;
        if (excludeReservationId is > 0)
        {
            var owned = await GetOwned(excludeReservationId.Value, ct);
            if (owned.Error is not null) return owned.Error;
        }
        var here = await FindSeating(branchId, reservedAt, partySize, excludeReservationId, ct);
        if (here.Layout is null) return MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404);
        var times = await BuildSuggestions(branchId, reservedAt, partySize, excludeReservationId, ct);
        var branches = excludeReservationId is > 0
            ? Array.Empty<MobileBranchSuggestion>()
            : await BuildBranchSuggestions(branchId, reservedAt, partySize, latitude, longitude, ct);
        return Ok(new MobileAlternativesResponse(here.OptionCount > 0, here.OptionCount, times, branches));
    }

    private async Task<ObjectResult> NoTableError(int branchId, DateTimeOffset at, int partySize, int? excludeReservationId,
        double? latitude, double? longitude, CancellationToken ct, bool includeOtherBranches = true)
    {
        var times = await BuildSuggestions(branchId, at, partySize, excludeReservationId, ct);
        var branches = includeOtherBranches
            ? await BuildBranchSuggestions(branchId, at, partySize, latitude, longitude, ct)
            : Array.Empty<MobileBranchSuggestion>();
        return MobileError("reservation_slot_unavailable", "Khung giờ đã hết bàn phù hợp.", 409,
            new { suggestions = times, branchSuggestions = branches });
    }

    private async Task<IReadOnlyList<MobileAvailabilitySuggestion>> BuildSuggestions(
        int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct)
    {
        var openFrom = ReservationRules.ConfiguredTime(_configuration, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom);
        var lastSlot = ReservationRules.ConfiguredTime(_configuration, "Reservations:LastSlot", ReservationRules.DefaultLastSlot);
        var candidates = AvailabilitySuggestions.CandidateTimes(at, openFrom, lastSlot, DateTimeOffset.UtcNow, ReservationRules.RestaurantTimeZone);
        var suggestions = new List<MobileAvailabilitySuggestion>();
        foreach (var candidate in candidates.Take(AvailabilitySuggestions.MaxTimeChecks))
        {
            var result = await FindSeating(branchId, candidate, partySize, excludeReservationId, ct);
            if (result.OptionCount > 0)
                suggestions.Add(new MobileAvailabilitySuggestion(candidate, result.OptionCount,
                    ReservationRules.LocalClock(candidate.UtcDateTime), LocalDate(candidate)));
            if (suggestions.Count >= AvailabilitySuggestions.MaxTimeSuggestions) break;
        }
        return suggestions.OrderBy(x => x.ReservedAt).ToArray();
    }

    private async Task<IReadOnlyList<MobileBranchSuggestion>> BuildBranchSuggestions(int branchId, DateTimeOffset at, int partySize,
        double? latitude, double? longitude, CancellationToken ct)
    {
        var branches = (await _catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>()).Where(x => x.BranchId != branchId).ToArray();
        var options = new List<AvailabilitySuggestions.BranchOption>();
        foreach (var branch in branches)
        {
            var result = await FindSeating(branch.BranchId, at, partySize, null, ct);
            if (result.Layout is null || result.OptionCount == 0) continue;
            options.Add(new AvailabilitySuggestions.BranchOption(branch.BranchId, branch.Name, branch.Location,
                GeoDistance.Km(latitude, longitude, branch.Latitude, branch.Longitude), result.OptionCount));
        }
        return AvailabilitySuggestions.OrderBranches(options)
            .Select(x => new MobileBranchSuggestion(x.BranchId, x.Name, x.Location, x.DistanceKm, x.AvailableTableCount, at,
                ReservationRules.LocalClock(at.UtcDateTime), LocalDate(at)))
            .ToArray();
    }

    private static string LocalDate(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, ReservationRules.RestaurantTimeZone).ToString("yyyy-MM-dd");

    private async Task<(ReservationDto? Reservation, ObjectResult? Error)> GetOwned(int reservationId, CancellationToken ct)
    {
        if (reservationId <= 0) return (null, MobileError("validation_failed", "Đặt bàn không hợp lệ.", 400));
        var reservation = await _customers.GetReservationByIdAsync(reservationId, ct);
        if (reservation is null) return (null, MobileError("reservation_not_found", "Không tìm thấy đặt bàn.", 404));
        return reservation.CustomerId == CurrentCustomerId()
            ? (reservation, null)
            : (null, MobileError("forbidden", "Bạn không có quyền truy cập đặt bàn này.", 403));
    }

    /// <summary>409 table_taken: the app shows the message and reloads the map.</summary>
    private ObjectResult TableTaken(BranchTablesResponse? layout, int tableId)
    {
        var number = layout?.Tables.FirstOrDefault(x => x.TableId == tableId)?.DisplayTableNumber;
        var label = number is > 0 ? $"Bàn {number}" : "Bàn bạn chọn";
        return MobileError("table_taken", $"{label} vừa được người khác đặt hoặc không còn trống trong khung giờ này. Vui lòng chọn bàn khác trên sơ đồ.",
            409, new { tableId });
    }

    /// <summary>The cashier's reservation panels reload at once (best effort; they also reload on their own).</summary>
    private async Task NudgeCashierAsync(int branchId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _staffEvents.PublishAsync(branchId, new[] { SelfRestaurant.Gateway.Api.Infrastructure.StaffBranchEvents.Reservations }, timeout.Token);
        }
        catch (Exception ex) { _logger.LogWarning("Reservation staff nudge skipped ({ErrorType}).", ex.GetType().Name); }
    }

    private static bool IsOpenReservation(ReservationDto x) =>
        x.Status is not null && new[] { "PendingDeposit", "Pending", "Confirmed", "CheckingIn" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase);

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
