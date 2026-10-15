using SelfRestaurant.Gateway.Api.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1")]
public sealed class MobileCheckInsGatewayController : ControllerBase
{
    private readonly CustomersClient _customers;
    private readonly CatalogClient _catalog;
    private readonly OrdersClient _orders;
    private readonly ILogger<MobileCheckInsGatewayController> _logger;
    private readonly IHubContext<CustomerNotificationsHub> _staffUpdatesHub;

    public MobileCheckInsGatewayController(CustomersClient customers, CatalogClient catalog, OrdersClient orders,
        ILogger<MobileCheckInsGatewayController> logger, IHubContext<CustomerNotificationsHub> staffUpdatesHub)
    {
        _customers = customers;
        _catalog = catalog;
        _orders = orders;
        _logger = logger;
        _staffUpdatesHub = staffUpdatesHub;
    }

    [HttpPost("check-ins/qr/validate")]
    public async Task<ActionResult<MobileQrValidationResponse>> ValidateQr(MobileQrValidationRequest request, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var table = await ResolveTable(request.QrCode, ct);
        if (table.Error is not null) return table.Error;

        ReservationDto? reservation = null;
        if (request.ReservationId is > 0)
        {
            reservation = await _customers.GetReservationByIdAsync(request.ReservationId.Value, ct);
            if (reservation is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            if (reservation.CustomerId != actor) return Error("forbidden", "Bạn không sở hữu đặt bàn này.", 403);
        }
        else
        {
            var reservations = await _customers.GetCustomerReservationsAsync(actor.Value, ct);
            reservation = reservations.Where(IsCheckInState)
                .OrderBy(x => Math.Abs((x.ReservedAt - DateTime.UtcNow).TotalMinutes)).FirstOrDefault();
        }

        if (reservation is null)
            return Ok(new MobileQrValidationResponse("WALK_IN_AVAILABLE", table.Table!.TableId, table.Table.BranchId,
                table.Table.DisplayTableNumber, null, null, false, "QR hợp lệ. Có thể check-in walk-in."));
        if (!IsCheckInState(reservation))
            return Error("reservation_state_conflict", "Trạng thái đặt bàn không cho phép check-in.", 409);
        if (!WithinArrivalWindow(reservation))
            return Ok(new MobileQrValidationResponse("RESERVATION_NOT_READY", table.Table!.TableId, table.Table.BranchId,
                table.Table.DisplayTableNumber, reservation.ReservationId, reservation.TableId, false,
                "Chưa đến hoặc đã quá khung giờ check-in của đặt bàn."));
        if (reservation.BranchId != table.Table!.BranchId || reservation.TableId != table.Table.TableId)
            return Ok(new MobileQrValidationResponse("MISMATCH_CONFIRMATION_REQUIRED", table.Table.TableId,
                table.Table.BranchId, table.Table.DisplayTableNumber, reservation.ReservationId, reservation.TableId,
                true, "QR không trùng bàn đã đặt. Cần xác nhận rõ trước khi tiếp tục."));
        return Ok(new MobileQrValidationResponse("READY", table.Table.TableId, table.Table.BranchId,
            table.Table.DisplayTableNumber, reservation.ReservationId, reservation.TableId, false, "Sẵn sàng check-in."));
    }

    [HttpPost("check-ins/reservation")]
    public async Task<ActionResult<MobileDiningSessionResponse>> CheckInReservation(MobileReservationCheckInRequest request, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = RequireKey();
        if (key.Error is not null) return key.Error;
        var reservation = await _customers.GetReservationByIdAsync(request.ReservationId, ct);
        if (reservation is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
        if (reservation.CustomerId != actor) return Error("forbidden", "Bạn không sở hữu đặt bàn này.", 403);
        if (!IsCheckInState(reservation) && !string.Equals(reservation.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
            return Error("reservation_state_conflict", "Trạng thái đặt bàn không cho phép check-in.", 409);
        if (!WithinArrivalWindow(reservation) && !string.Equals(reservation.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
            return Error("outside_arrival_window", "Chưa đến hoặc đã quá khung giờ check-in.", 409,
                new { reservation.ReservedAt, reservation.ArrivalWindowMinutes });
        var table = await ResolveTable(request.QrCode, ct);
        if (table.Error is not null) return table.Error;
        if (table.Table!.BranchId != reservation.BranchId)
            return Error("branch_mismatch", "QR thuộc chi nhánh khác với đặt bàn.", 409);
        var mismatch = reservation.TableId is > 0 && reservation.TableId != table.Table.TableId;
        if (mismatch && !request.ConfirmTableMismatch)
            return Error("table_mismatch_confirmation_required", "QR không trùng bàn đã đặt. Gửi lại với xác nhận rõ.", 409,
                new { scannedTableId = table.Table.TableId, reservedTableId = reservation.TableId, confirmationRequired = true });
        if (mismatch && !table.Table.IsAvailable)
            return Error("table_unavailable", "Bàn quét thay thế hiện không sẵn sàng.", 409);

        var hash = Hash(new { actor, reservation.ReservationId, table.Table.BranchId, table.Table.TableId });
        var scopedKey = $"c{actor}:reservation-checkin:{key.Value}";
        try
        {
            await _customers.BeginReservationCheckInAsync(reservation.ReservationId, ct);
            var session = await _orders.CreateDiningSessionAsync(new DiningSessionCreateRequest(actor.Value,
                table.Table.BranchId, table.Table.TableId, reservation.ReservationId, scopedKey, hash), ct);
            if (session is null) return Error("check_in_failed", "Không thể tạo phiên phục vụ.", 502);
            await _customers.CompleteReservationCheckInAsync(reservation.ReservationId,
                new CheckInReservationRequest(session.ConvertedOrderId, session.SessionCode, null,
                    DateTime.UtcNow, table.Table.TableId, new[] { table.Table.TableId }), ct);
            await _orders.OccupyTableAsync(table.Table.TableId, ct);
            return Ok(session);
        }
        catch (ApiClientException ex)
        {
            try { await _customers.FailReservationCheckInAsync(reservation.ReservationId, ct); } catch { }
            return Downstream(ex, "check_in_failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobile reservation check-in failed for reservation {ReservationId}.", request.ReservationId);
            return Error("check_in_failed", "Không thể hoàn tất check-in.", 502);
        }
    }

    [HttpPost("check-ins/walk-in")]
    public async Task<ActionResult<MobileDiningSessionResponse>> CheckInWalkIn(MobileWalkInCheckInRequest request, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = RequireKey();
        if (key.Error is not null) return key.Error;
        var table = await ResolveTable(request.QrCode, ct);
        if (table.Error is not null) return table.Error;
        var resolvedTable = table.Table!;
        var hash = Hash(new { actor, resolvedTable.BranchId, resolvedTable.TableId, Type = "walk-in" });
        try
        {
            var session = await _orders.CreateDiningSessionAsync(new DiningSessionCreateRequest(actor.Value,
                resolvedTable.BranchId, resolvedTable.TableId, null, $"c{actor}:walkin:{key.Value}", hash), ct);
            if (session is null) return Error("check_in_failed", "Không thể tạo phiên phục vụ.", 502);
            await _orders.OccupyTableAsync(resolvedTable.TableId, ct);
            return Ok(session);
        }
        catch (ApiClientException ex) { return Downstream(ex, "check_in_failed"); }
    }

    [HttpGet("dining-sessions/active")]
    public async Task<ActionResult<MobileDiningSessionResponse>> GetActive(CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var session = await _orders.GetActiveDiningSessionAsync(actor.Value, ct);
            return session is null ? Error("active_session_not_found", "Không có phiên phục vụ đang hoạt động.", 404) : Ok(session);
        }
        catch (ApiClientException ex) { return Downstream(ex, "get_session_failed"); }
    }

    [HttpGet("dining-sessions/{sessionId:int}")]
    public async Task<ActionResult<MobileDiningSessionResponse>> Get(int sessionId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var session = await _orders.GetDiningSessionAsync(sessionId, actor.Value, ct);
            return session is null ? Error("session_not_found", "Không tìm thấy phiên phục vụ.", 404) : Ok(session);
        }
        catch (ApiClientException ex) { return Downstream(ex, "get_session_failed"); }
    }

    [HttpPost("dining-sessions/{sessionId:int}/cancel")]
    public async Task<ActionResult<MobileDiningSessionResponse>> CancelEmptySession(int sessionId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        MobileDiningSessionResponse? session;
        try { session = await _orders.CancelEmptyDiningSessionAsync(sessionId, actor.Value, ct); }
        catch (ApiClientException ex) { return Downstream(ex, "cancel_session_failed"); }
        if (session is null) return Error("session_not_found", "Không tìm thấy phiên phục vụ.", 404);
        try
        {
            // The table is already released; a missed refresh only delays the cashier's table list.
            await _staffUpdatesHub.Clients.Group(CustomerNotificationsHub.BranchGroup(session.BranchId))
                .SendAsync("cashierDashboardChanged", new { tableId = session.TableId, branchId = session.BranchId }, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Cashier refresh after session cancel failed for branch {BranchId}.", session.BranchId); }
        return Ok(session);
    }

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("dining-sessions/{sessionId:int}/cancel-payment-request")]
    public async Task<ActionResult<MobileDiningSessionResponse>> CancelPaymentRequest(int sessionId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var session = await _orders.CancelPaymentRequestAsync(sessionId, actor.Value, ct);
            return session is null ? Error("session_not_found", "Không tìm thấy phiên phục vụ.", 404) : Ok(session);
        }
        catch (ApiClientException ex) { return Downstream(ex, "cancel_payment_request_failed"); }
    }

    private async Task<(BranchTableDto? Table, ObjectResult? Error)> ResolveTable(string? qr, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(qr)) return (null, Error("invalid_qr", "Mã QR không hợp lệ.", 400));
        var table = await _catalog.GetTableByQrAsync(qr.Trim(), ct);
        return table is null ? (null, Error("qr_not_found", "Không tìm thấy bàn từ mã QR.", 404)) : (table, null);
    }

    private static bool IsCheckInState(ReservationDto x) =>
        string.Equals(x.Status, "Confirmed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(x.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase);
    private static bool WithinArrivalWindow(ReservationDto x) =>
        Math.Abs((x.ReservedAt - DateTime.UtcNow).TotalMinutes) <= Math.Max(1, x.ArrivalWindowMinutes);
    private (string? Value, ObjectResult? Error) RequireKey()
    {
        var value = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > 64
            ? (null, Error("idempotency_key_required", "Idempotency-Key là bắt buộc và tối đa 64 ký tự.", 400))
            : (value, null);
    }
    private int? CurrentCustomerId() => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private ObjectResult Downstream(ApiClientException ex, string fallback) => Error(ex.Code ?? fallback, ex.Message, ex.StatusCode, ParseDetails(ex.ResponseBody));
    private static object? ParseDetails(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.TryGetProperty("details", out var details) ? details.Clone() : null; }
        catch { return null; }
    }
    private ObjectResult Error(string code, string message, int status, object? details = null) =>
        StatusCode(status, MobileApiError.Create(code, message,
            Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier, details));
}
