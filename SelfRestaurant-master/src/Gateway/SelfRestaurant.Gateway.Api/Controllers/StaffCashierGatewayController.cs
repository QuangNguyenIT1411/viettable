using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Route("api/gateway/staff/cashier")]
public sealed class StaffCashierGatewayController : ControllerBase
{
    private static readonly string[] CashierRoles = ["CASHIER", "MANAGER", "ADMIN"];
    public sealed record CashierReservationCheckInRequest(int? TableId, IReadOnlyList<int>? TableIds);
    public sealed record CashierReservationRejectRequest(string? Reason);

    private readonly BillingClient _billingClient;
    private readonly IdentityClient _identityClient;
    private readonly CatalogClient _catalogClient;
    private readonly CustomersClient _customersClient;
    private readonly OrdersClient _ordersClient;
    private readonly IHubContext<CustomerNotificationsHub> _customerNotificationsHub;
    private readonly IHubContext<MobileNotificationsHub> _mobileNotificationsHub;
    private readonly ILogger<StaffCashierGatewayController> _logger;
    private readonly CustomerPaymentNotifier _paymentNotifier;

    public StaffCashierGatewayController(
        CustomerPaymentNotifier paymentNotifier,
        BillingClient billingClient,
        IdentityClient identityClient,
        CatalogClient catalogClient,
        CustomersClient customersClient,
        OrdersClient ordersClient,
        IHubContext<CustomerNotificationsHub> customerNotificationsHub,
        IHubContext<MobileNotificationsHub> mobileNotificationsHub,
        ILogger<StaffCashierGatewayController> logger)
    {
        _billingClient = billingClient;
        _identityClient = identityClient;
        _catalogClient = catalogClient;
        _customersClient = customersClient;
        _ordersClient = ordersClient;
        _customerNotificationsHub = customerNotificationsHub;
        _mobileNotificationsHub = mobileNotificationsHub;
        _logger = logger;
        _paymentNotifier = paymentNotifier;
    }

    [HttpGet("session")]
    public ActionResult<StaffSessionDto> GetSession() => Ok(BuildSessionDto());

    [HttpGet("onsite-payments")]
    public async Task<IActionResult> MobileOnsitePayments(CancellationToken ct) => await CashierPayment(null, ct);

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.OnsitePayments)]
    [HttpPost("onsite-payments/{paymentId:int}/confirm")]
    [Consumes("application/json")]
    public async Task<IActionResult> ConfirmMobileOnsitePayment(int paymentId, CancellationToken ct) => await CashierPayment(paymentId, ct);

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.OnsitePayments)]
    [HttpPost("onsite-payments/{paymentId:int}/cancel")]
    [Consumes("application/json")]
    public async Task<IActionResult> CancelMobileOnsitePayment(int paymentId, [FromServices] CustomerInboxNotifier inbox, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (staff.BranchId <= 0) return Error("branch_required", "Tài khoản chưa được gán chi nhánh.", 403);
        try
        {
            var result = await _billingClient.CancelCashierMobilePaymentAsync(staff, paymentId, ct);
            // The guest's waiting screen changes at once (not on its next status check) and the inbox says why.
            if (result.TryGetProperty("customerId", out var c) && c.TryGetInt32(out var customerId) && customerId > 0
                && result.TryGetProperty("diningSessionId", out var d) && d.TryGetInt32(out var sessionId))
            {
                var amount = result.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var parsed) ? parsed : 0m;
                try
                {
                    await _mobileNotificationsHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId))
                        .SendAsync("paymentUpdated", new { diningSessionId = sessionId, paymentId, status = "CANCELLED" }, ct);
                }
                catch (Exception ex) { _logger.LogWarning("Payment {PaymentId} cancel realtime skipped ({ErrorType}).", paymentId, ex.GetType().Name); }
                await inbox.NotifyAsync(customerId, CustomerInboxNotifier.PaymentCancelled, paymentId,
                    $"Thu ngân đã hủy giao dịch chờ {decimal.Round(amount).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.')}đ. Vui lòng chọn lại cách thanh toán.",
                    null, ct);
            }
            return Ok(result);
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "cashier_payment_cancel_failed", ex.Message, ex.StatusCode); }
    }

    private async Task<IActionResult> CashierPayment(int? paymentId, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (staff.BranchId <= 0) return Error("branch_required", "Tài khoản chưa được gán chi nhánh.", 403);
        try
        {
            var result = await _billingClient.CashierMobilePaymentAsync(staff, paymentId, ct);
            if (paymentId is not null) await NotifyCustomerPaymentAsync(result, ct);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "cashier_payment_failed", ex.Message, ex.StatusCode); }
    }

    // Best effort after the payment is durably SUCCEEDED: an inbox row for the customer plus a realtime nudge.
    // A failure here must never turn a collected payment into an error for the cashier.
    private async Task NotifyCustomerPaymentAsync(JsonElement payment, CancellationToken ct)
    {
        if (payment.ValueKind != JsonValueKind.Object
            || !payment.TryGetProperty("status", out var status) || status.GetString() != "SUCCEEDED"
            || !payment.TryGetProperty("customerId", out var customer) || !customer.TryGetInt32(out var customerId) || customerId <= 0
            || !payment.TryGetProperty("paymentId", out var paymentValue) || !paymentValue.TryGetInt32(out var paymentId)
            || !payment.TryGetProperty("diningSessionId", out var session) || !session.TryGetInt32(out var diningSessionId)) return;
        var amount = payment.TryGetProperty("amount", out var amountValue) && amountValue.TryGetDecimal(out var parsed) ? parsed : 0m;
        await NotifyCustomerPaymentAsync(customerId, paymentId, diningSessionId, amount, ct);
    }

    // Shared by the onsite-payments panel and the dashboard "Thanh toán" button so both notify identically.
    private Task NotifyCustomerPaymentAsync(int customerId, int paymentId, int diningSessionId, decimal amount, CancellationToken ct) =>
        _paymentNotifier.NotifyAsync(customerId, paymentId, diningSessionId, amount, ct);

    [HttpPost("auth/login")]
    public async Task<ActionResult<object>> Login([FromBody] StaffLoginApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ thông tin.", 400);
        }

        try
        {
            var staff = await _identityClient.StaffLoginAsync(new StaffLoginRequest(request.Username.Trim(), request.Password), cancellationToken);
            if (staff is null)
            {
                return Error("invalid_credentials", "Tên đăng nhập hoặc mật khẩu không đúng.", 401);
            }

            if (!CashierRoles.Contains(staff.RoleCode, StringComparer.OrdinalIgnoreCase))
            {
                return Error("forbidden", "Bạn không có quyền truy cập trang Thu Ngân.", 403);
            }

            ApplyStaffSession(staff);
            return Ok(new { success = true, session = BuildSessionDto(), nextPath = "/Staff/Cashier/Index" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff cashier login failed.");
            return Error("login_failed", ex.Message, 400);
        }
    }

    [HttpPost("auth/logout")]
    public ActionResult<object> Logout()
    {
        var userName = HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeName) ?? "Người dùng";
        ClearStaffSession();
        return Ok(new
        {
            success = true,
            nextPath = $"/Staff/Account/Login?message={Uri.EscapeDataString($"Tạm biệt {userName}! Bạn đã đăng xuất thành công.")}&type=success"
        });
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<CashierDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        return Ok(await BuildDashboardAsync(staff.BranchId, staff, includeBills: false, billsDate: null, cancellationToken));
    }

    [HttpGet("service-requests")]
    public async Task<IActionResult> GetServiceRequests(CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản nhân viên chi nhánh.", 401);
        try { return Ok(await _ordersClient.GetStaffServiceRequestsAsync(staff.BranchId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "service_requests_failed", ex.Message, ex.StatusCode); }
    }

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.ServiceRequests)]
    [HttpPost("service-requests/{requestId:int}/acknowledge")]
    public Task<IActionResult> AcknowledgeServiceRequest(int requestId, [FromServices] CustomerInboxNotifier inbox, CancellationToken ct) =>
        UpdateServiceRequest(requestId, complete: false, inbox, ct);

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.ServiceRequests)]
    [HttpPost("service-requests/{requestId:int}/complete")]
    public Task<IActionResult> CompleteServiceRequest(int requestId, [FromServices] CustomerInboxNotifier inbox, CancellationToken ct) =>
        UpdateServiceRequest(requestId, complete: true, inbox, ct);

    private async Task<IActionResult> UpdateServiceRequest(int requestId, bool complete, CustomerInboxNotifier inbox, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản nhân viên chi nhánh.", 401);
        try
        {
            var command = new StaffServiceRequestCommand(staff.BranchId, staff.EmployeeId);
            var updated = complete
                ? await _ordersClient.CompleteServiceRequestAsync(requestId, command, ct)
                : await _ordersClient.AcknowledgeServiceRequestAsync(requestId, command, ct);
            if (updated is null) return Error("service_request_not_found", "Không tìm thấy yêu cầu phục vụ.", 404);
            await _mobileNotificationsHub.Clients.Group(MobileNotificationsHub.CustomerGroup(updated.CustomerId))
                .SendAsync("serviceRequestUpdated", new { updated.ServiceRequestId, updated.DiningSessionId, updated.Status, updated.UpdatedAtUtc }, ct);
            var label = CustomerInboxNotifier.RequestLabel(updated.RequestType);
            await inbox.NotifyAsync(updated.CustomerId,
                complete ? CustomerInboxNotifier.ServiceCompleted : CustomerInboxNotifier.ServiceAcknowledged, updated.ServiceRequestId,
                complete ? $"Yêu cầu {label} của bạn đã được nhân viên xử lý xong." : $"Nhân viên đã nhận yêu cầu {label} của bạn.", null, ct);
            return Ok(updated);
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "service_request_update_failed", ex.Message, ex.StatusCode); }
    }

    [HttpGet("/api/cashier/reservations/today")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetTodayReservations(
        [FromQuery] int? branchId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);

        // The cashier's own branch only (a branchId of another branch used to be honoured).
        if (branchId is > 0 && branchId != staff.BranchId)
            return Error("branch_mismatch", "Chỉ xem được đặt bàn của chi nhánh đang làm việc.", 403);
        try
        {
            var reservations = await _customersClient.GetTodayReservationsAsync(staff.BranchId, status, date, cancellationToken);
            return Ok(reservations);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "get_reservations_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    /// <summary>"Đặt bàn chờ xác nhận": every pending booking of the branch, today and the coming days.</summary>
    [HttpGet("/api/cashier/reservations/pending")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetPendingReservations(CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _customersClient.GetPendingReservationsAsync(staff.BranchId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "get_reservations_failed", ex.Message, ex.StatusCode); }
    }

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Reservations)]
    [HttpPost("/api/cashier/reservations/{reservationId:int}/confirm")]
    public Task<ActionResult<ReservationDto>> ConfirmReservation(int reservationId, [FromServices] CustomerInboxNotifier inbox,
        CancellationToken ct) => DecideReservation(reservationId, "confirm", null, inbox, ct);

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Reservations)]
    [HttpPost("/api/cashier/reservations/{reservationId:int}/reject")]
    public Task<ActionResult<ReservationDto>> RejectReservation(int reservationId, [FromBody] CashierReservationRejectRequest? request,
        [FromServices] CustomerInboxNotifier inbox, CancellationToken ct) =>
        DecideReservation(reservationId, "reject", request?.Reason, inbox, ct);

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Reservations)]
    [HttpPost("/api/cashier/reservations/{reservationId:int}/no-show")]
    public Task<ActionResult<ReservationDto>> MarkReservationNoShow(int reservationId, [FromServices] CustomerInboxNotifier inbox,
        CancellationToken ct) => DecideReservation(reservationId, "no-show", null, inbox, ct);

    /// <summary>Deposits of the cashier's branch with their bookings (newest first).</summary>
    [HttpGet("/api/cashier/deposits")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetDeposits([FromServices] ReservationDepositCoordinator deposits, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await deposits.StaffListAsync(staff.BranchId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "get_deposits_failed", ex.Message, ex.StatusCode); }
    }

    /// <summary>"Đã hoàn cọc": the cashier refunded the guest by hand (only a deposit waiting for a refund).</summary>
    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Reservations)]
    [HttpPost("/api/cashier/deposits/{depositId:int}/refunded")]
    public async Task<ActionResult<object>> MarkDepositRefunded(int depositId, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _billingClient.MarkDepositRefundedAsync(depositId, staff.EmployeeId, staff.BranchId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "deposit_refund_failed", ex.Message, ex.StatusCode); }
    }

    private async Task<ActionResult<ReservationDto>> DecideReservation(int reservationId, string decision, string? reason,
        CustomerInboxNotifier inbox, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (reservationId <= 0) return Error("invalid_reservation", "Đặt bàn không hợp lệ.", 400);
        if (decision == "reject" && string.IsNullOrWhiteSpace(reason))
            return Error("reason_required", "Vui lòng chọn lý do từ chối.", 400);
        try
        {
            var current = await _customersClient.GetReservationByIdAsync(reservationId, ct);
            if (current is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            if (current.BranchId != staff.BranchId)
                return Error("branch_mismatch", "Đặt bàn không thuộc chi nhánh của thu ngân hiện tại.", 403);
            var updated = await _customersClient.DecideReservationAsync(reservationId, decision, staff.EmployeeId, reason?.Trim(), ct);
            if (updated is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            // Dishes ordered ahead must never reach the kitchen once the booking is closed (best effort, like a guest cancel).
            if (decision == "no-show")
                try { await _ordersClient.CancelReservationPreOrderAsync(reservationId, ct); }
                catch (Exception ex) { _logger.LogWarning("Pre-order cancel skipped for reservation {ReservationId} ({ErrorType}).", reservationId, ex.GetType().Name); }
            // Rejected → refund by hand; no-show → deposit kept (docs/DEPOSIT_PLAN.md).
            if (decision is "reject" or "no-show")
                await HttpContext.RequestServices.GetRequiredService<ReservationDepositCoordinator>().SyncQuietlyAsync(updated, ct);
            if (!string.Equals(current.Status, updated.Status, StringComparison.OrdinalIgnoreCase))
                await inbox.ReservationChangedAsync(updated.CustomerId, updated.ReservationId, updated.Status, ct);
            return Ok(updated);
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "reservation_update_failed", ex.Message, ex.StatusCode); }
    }

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Reservations)]
    [HttpPost("/api/cashier/reservations/{reservationId:int}/check-in")]
    public async Task<ActionResult<CashierReservationCheckInResultDto>> CheckInReservation(
        int reservationId,
        [FromBody] CashierReservationCheckInRequest? request,
        [FromServices] CustomerInboxNotifier inbox,
        CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (reservationId <= 0) return Error("invalid_reservation", "Đặt bàn không hợp lệ.", 400);

        try
        {
            var previousState = await _customersClient.GetReservationByIdAsync(reservationId, cancellationToken);
            if (previousState is null)
            {
                return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            }
            if (previousState.BranchId != staff.BranchId)
            {
                return Error("branch_mismatch", "Đặt bàn không thuộc chi nhánh của thu ngân hiện tại.", 403);
            }

            var wasAlreadyCheckingIn = string.Equals(previousState.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase);
            var selectedTableIds = NormalizeSelectedTableIds(request?.TableIds);
            if (selectedTableIds.Count == 0 && request?.TableId is > 0)
            {
                selectedTableIds.Add(request.TableId.Value);
            }
            if (selectedTableIds.Count == 0 && previousState.TableId is > 0)
            {
                selectedTableIds.Add(previousState.TableId.Value);
            }
            var tableId = request?.TableId is > 0 && selectedTableIds.Contains(request.TableId.Value)
                ? request.TableId.Value
                : previousState.TableId is > 0
                    ? previousState.TableId.Value
                    : selectedTableIds.FirstOrDefault();
            if (tableId <= 0)
            {
                return Error("missing_table", "Vui lòng chọn ít nhất một bàn trước khi check-in.", 400);
            }
            if (!selectedTableIds.Contains(tableId))
            {
                selectedTableIds.Insert(0, tableId);
            }

            var branchTables = await _catalogClient.GetBranchTablesAsync(previousState.BranchId, cancellationToken);
            var branchTableList = branchTables?.Tables ?? [];
            foreach (var selectedTableId in selectedTableIds)
            {
                var selectedTable = branchTableList.FirstOrDefault(x => x.TableId == selectedTableId && x.BranchId == previousState.BranchId);
                if (selectedTable is null)
                {
                    return Error("table_not_found", $"Không tìm thấy bàn #{selectedTableId} trong chi nhánh.", 404);
                }

                var isExistingReservationTable = previousState.TableId == selectedTableId;
                if (!wasAlreadyCheckingIn
                    && !isExistingReservationTable
                    && !selectedTable.IsAvailable
                    && !string.Equals(selectedTable.StatusCode, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                {
                    var number = selectedTable.DisplayTableNumber > 0 ? selectedTable.DisplayTableNumber : selectedTable.TableId;
                    return Error("table_unavailable", $"Bàn {number} hiện không sẵn sàng để check-in.", 409);
                }
            }

            var reservation = await _customersClient.BeginReservationCheckInAsync(reservationId, cancellationToken, staff.EmployeeId);
            if (reservation is null)
            {
                return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            }

            if (string.Equals(reservation.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
            {
                ActiveOrderResponse? existingOrder = null;
                if (reservation.ConvertedOrderId is > 0)
                {
                    existingOrder = await _ordersClient.GetOrderByIdAsync(reservation.ConvertedOrderId.Value, cancellationToken);
                }

                await inbox.PushAsync(previousState?.CustomerId, cancellationToken); // Customers wrote "Đã nhận bàn"
            return Ok(new CashierReservationCheckInResultDto(
                    true,
                    "Đặt bàn đã được check-in trước đó.",
                    reservation,
                    existingOrder,
                    AlreadyCheckedIn: true));
            }

            if (!string.Equals(reservation.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
            {
                return Error("invalid_check_in_state", "Đặt bàn chưa sẵn sàng để check-in.", 409);
            }

            if (reservation.BranchId != staff.BranchId)
            {
                await _customersClient.FailReservationCheckInAsync(reservation.ReservationId, cancellationToken);
                return Error("branch_mismatch", "Đặt bàn không thuộc chi nhánh của thu ngân hiện tại.", 403);
            }

            var pendingItems = reservation.PreOrderItems
                .Where(item => string.Equals(item.Status, "Pending", StringComparison.OrdinalIgnoreCase) && item.Quantity > 0)
                .ToArray();

            var idempotencyKey = string.IsNullOrWhiteSpace(reservation.CheckInIdempotencyKey)
                ? $"reservation-checkin-{reservation.ReservationId}"
                : reservation.CheckInIdempotencyKey;

            if (pendingItems.Length == 0)
            {
                var checkedInWithoutOrder = await _customersClient.CompleteReservationCheckInAsync(
                    reservation.ReservationId,
                    new CheckInReservationRequest(null, null, staff.EmployeeId, DateTime.UtcNow, tableId, selectedTableIds),
                    cancellationToken);

                if (checkedInWithoutOrder is null)
                {
                    return Error("reservation_update_failed", "Không thể cập nhật trạng thái check-in.", 502);
                }

                await OccupyAssignedTablesAsync(selectedTableIds, primaryTableIdToSkip: null, cancellationToken);

                await inbox.PushAsync(previousState?.CustomerId, cancellationToken); // Customers wrote "Đã nhận bàn"
            return Ok(new CashierReservationCheckInResultDto(
                    true,
                    "Đã check-in đặt bàn. Chưa có món đặt trước nên chưa tạo đơn bếp.",
                    checkedInWithoutOrder,
                    null,
                    AlreadyCheckedIn: false));
            }

            var orderItems = pendingItems
                .Select(item => new AddOrderItemPayload(
                    item.DishId,
                    item.Quantity,
                    string.IsNullOrWhiteSpace(item.Note) ? $"Đặt trước {reservation.ReservationCode}" : $"{item.Note} (Đặt trước {reservation.ReservationCode})"))
                .ToArray();

            ActiveOrderResponse? order;
            try
            {
                order = await _ordersClient.SubmitOrderBatchAsync(
                    tableId,
                    orderItems,
                    reservation.PhoneNumber,
                    idempotencyKey,
                    expectedDiningSessionCode: null,
                    cancellationToken);
            }
            catch
            {
                await _customersClient.FailReservationCheckInAsync(reservation.ReservationId, cancellationToken);
                throw;
            }

            if (order is null)
            {
                await _customersClient.FailReservationCheckInAsync(reservation.ReservationId, cancellationToken);
                return Error("order_create_failed", "Không thể tạo đơn hàng từ đặt bàn.", 502);
            }

            if (!string.IsNullOrWhiteSpace(order.DiningSessionCode))
            {
                try
                {
                    await _ordersClient.LinkDiningSessionTablesAsync(
                        order.DiningSessionCode,
                        tableId,
                        selectedTableIds,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    await _customersClient.FailReservationCheckInAsync(reservation.ReservationId, cancellationToken);
                    _logger.LogError(ex, "Failed to link tables to dining session {DiningSessionCode} for reservation {ReservationId}.", order.DiningSessionCode, reservation.ReservationId);
                    throw;
                }
            }
            ReservationDto? checkedInReservation;
            try
            {
                checkedInReservation = await _customersClient.CompleteReservationCheckInAsync(
                    reservation.ReservationId,
                    new CheckInReservationRequest(order.OrderId, order.DiningSessionCode, staff.EmployeeId, DateTime.UtcNow, tableId, selectedTableIds),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation complete check-in failed after order creation for reservation {ReservationId} with key {IdempotencyKey}.", reservation.ReservationId, idempotencyKey);
                return Error(
                    "reservation_completion_failed",
                    "Đã tạo đơn hàng nhưng chưa hoàn tất cập nhật đặt bàn. Bấm Nhận khách lại để hoàn tất, hệ thống sẽ dùng cùng khóa xử lý và không tạo đơn trùng.",
                    502,
                    new { idempotencyKey, orderId = order.OrderId, orderCode = order.OrderCode });
            }

            if (checkedInReservation is null)
            {
                return Error("reservation_update_failed", "Đã tạo đơn hàng nhưng không thể cập nhật trạng thái đặt bàn. Bấm Nhận khách lại để hoàn tất.", 502);
            }

            await OccupyAssignedTablesAsync(selectedTableIds, primaryTableIdToSkip: tableId, cancellationToken);

            await inbox.PushAsync(previousState?.CustomerId, cancellationToken);
            return Ok(new CashierReservationCheckInResultDto(
                true,
                "Đã check-in và tạo đơn hàng thành công.",
                checkedInReservation,
                order,
                AlreadyCheckedIn: false));
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "check_in_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reservation check-in failed.");
            return Error("check_in_failed", ex.Message, 400);
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<CashierHistoryDto>> GetHistory([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);

        var bills = await _billingClient.GetBillsAsync(staff.EmployeeId, staff.BranchId, null, Math.Clamp(take, 1, 300), cancellationToken);
        return Ok(new CashierHistoryDto(staff, bills.Select(MapBill).ToArray(), BuildAccountDto(staff)));
    }

    [HttpGet("report")]
    public async Task<ActionResult<CashierReportScreenDto>> GetReport([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var report = await _billingClient.GetReportAsync(staff.EmployeeId, staff.BranchId, targetDate, cancellationToken);
        if (report is null)
        {
            return Ok(new CashierReportScreenDto(staff, targetDate, 0, 0, Array.Empty<CashierBillHistoryItemDto>(), BuildAccountDto(staff)));
        }

        return Ok(new CashierReportScreenDto(
            staff,
            report.Date,
            report.BillCount,
            report.TotalRevenue,
            report.Bills.Select(MapBill).ToArray(),
            BuildAccountDto(staff),
            report.CashRevenue,
            report.TransferRevenue,
            report.OtherRevenue));
    }

    // VietQR shown on the counter screen. The cashier confirms receipt by hand after checking the bank app.
    [HttpGet("transfer-qr")]
    public async Task<IActionResult> OrderTransferQr([FromQuery] int orderId, [FromQuery] decimal amount, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _billingClient.CashierOrderTransferQrAsync(staff, orderId, amount, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "transfer_qr_failed", ex.Message, ex.StatusCode); }
    }

    // SePay transfers that were NOT auto-confirmed (too little money, unknown code, wrong branch, already paid…).
    [HttpGet("bank-transfers/alerts")]
    public async Task<IActionResult> BankTransferAlerts(CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _billingClient.CashierBankTransferAlertsAsync(staff, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "bank_transfer_alerts_failed", ex.Message, ex.StatusCode); }
    }

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.OnsitePayments)]
    [HttpPost("bank-transfers/{eventId:long}/resolve")]
    public async Task<IActionResult> ResolveBankTransferAlert(long eventId, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _billingClient.ResolveBankTransferAlertAsync(staff, eventId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "bank_transfer_resolve_failed", ex.Message, ex.StatusCode); }
    }

    [HttpGet("onsite-payments/{paymentId:int}/transfer-qr")]
    public async Task<IActionResult> PaymentTransferQr(int paymentId, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try { return Ok(await _billingClient.CashierPaymentTransferQrAsync(staff, paymentId, ct)); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "transfer_qr_failed", ex.Message, ex.StatusCode); }
    }

    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders, StaffBranchEvents.OnsitePayments)]
    [HttpPost("orders/{orderId:int}/checkout")]
    public async Task<ActionResult<CashierCheckoutResultDto>> Checkout(int orderId, [FromBody] CashierCheckoutApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);

        try
        {
            var response = await _billingClient.CheckoutAsync(orderId, new CashierCheckoutRequest(
                EmployeeId: staff.EmployeeId,
                Discount: Math.Max(0, request.Discount),
                PointsUsed: Math.Max(0, request.PointsUsed),
                PaymentMethod: string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod.Trim().ToUpperInvariant(),
                PaymentAmount: Math.Max(0, request.PaymentAmount),
                IdempotencyKey: request.IdempotencyKey,
                BranchId: staff.BranchId), cancellationToken);

            if (response is null)
            {
                return Error("checkout_failed", "Không nhận được phản hồi thanh toán.", 502);
            }

            if (response.MobilePaymentId is int mobilePaymentId && response.CustomerId is int payerId && response.DiningSessionId is int paidSessionId)
            {
                await NotifyCustomerPaymentAsync(payerId, mobilePaymentId, paidSessionId, response.TotalAmount, cancellationToken);
            }

            await _customerNotificationsHub.Clients
                .Group(CustomerNotificationsHub.BranchGroup(staff.BranchId))
                .SendAsync("cashierDashboardChanged", new { orderId, branchId = staff.BranchId }, cancellationToken);

            return Ok(new CashierCheckoutResultDto(
                response.BillCode,
                response.TotalAmount,
                response.ChangeAmount,
                response.PointsUsed,
                response.PointsEarned,
                response.CustomerPoints,
                response.CustomerName,
                response.PointsBefore,
                "Thanh toán thành công."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cashier checkout failed for order {OrderId}", orderId);
            return Error("checkout_failed", ex.Message, 400);
        }
    }

    [HttpPut("account")]
    public async Task<ActionResult<CashierAccountDto>> UpdateAccount([FromBody] CashierAccountUpdateApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ họ tên, email và số điện thoại.", 400);
        }

        try
        {
            var profile = await _identityClient.UpdateStaffProfileAsync(
                staff.EmployeeId,
                new StaffUpdateProfileRequest(request.Name.Trim(), request.Phone.Trim(), request.Email.Trim()),
                cancellationToken);

            if (profile is not null)
            {
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeName, profile.Name);
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeePhone, profile.Phone ?? string.Empty);
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeEmail, profile.Email ?? string.Empty);
            }

            var refreshed = RequireCashier() ?? staff;
            return Ok(BuildAccountDto(refreshed));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cashier update account failed.");
            return Error("update_account_failed", ex.Message, 400);
        }
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<object>> ChangePassword([FromBody] CashierChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ thông tin đổi mật khẩu.", 400);
        }
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Error("password_mismatch", "Xác nhận mật khẩu mới không khớp.", 400);
        }

        try
        {
            await _identityClient.StaffChangePasswordAsync(new StaffChangePasswordRequest(staff.EmployeeId, request.CurrentPassword, request.NewPassword), cancellationToken);
            return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cashier change password failed.");
            return Error("change_password_failed", ex.Message, 400);
        }
    }

    private async Task<CashierDashboardDto> BuildDashboardAsync(int branchId, StaffSessionUserDto staff, bool includeBills, DateOnly? billsDate, CancellationToken cancellationToken)
    {
        // Independent downstream requests own separate request-scoped DbContexts.
        // Start all three before awaiting, retaining the same authorization and projection.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var ordersTask = _billingClient.GetCashierOrdersAsync(branchId, cancellationToken);
        var tablesTask = _catalogClient.GetBranchTablesAsync(branchId, cancellationToken);
        var reportTask = _billingClient.GetBranchReportAsync(branchId, today, cancellationToken);
        var openSessionsTask = GetOpenSessionsAsync(branchId, cancellationToken);
        await Task.WhenAll(ordersTask, tablesTask, reportTask, openSessionsTask);
        var openSessionByTableId = (await openSessionsTask)
            .GroupBy(x => x.TableId).ToDictionary(g => g.Key, g => g.First());
        var orders = await ordersTask;
        var orderCards = orders.Select(o => new CashierOrderCardDto(
            o.OrderId,
            o.OrderCode ?? $"ORD{o.OrderId}",
            o.StatusCode,
            o.StatusName,
            o.CustomerId,
            o.CustomerName ?? string.Empty,
            o.CustomerPoints,
            o.Subtotal,
            o.ItemCount,
            o.Items.Select(i => new CashierOrderItemCardDto(i.DishName, i.Quantity, i.UnitPrice, i.LineTotal, ResolveDishImage(i.Image, i.DishName), i.StatusCode)).ToArray(),
            PromotionId: o.PromotionId, PromotionName: o.PromotionName, PromotionDiscount: o.PromotionDiscount)).ToArray();

        var activeOrderByTableId = orders
            .Where(o => o.TableId > 0 && !string.Equals(o.StatusCode, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            .GroupBy(o => o.TableId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OrderTime).First());

        var orderById = orderCards.ToDictionary(o => o.OrderId);
        IReadOnlyList<CashierTableDto> tableCards;

        var tables = await tablesTask;
        if (tables?.Tables is { Count: > 0 })
        {
            tableCards = tables.Tables.Select(t =>
            {
                activeOrderByTableId.TryGetValue(t.TableId, out var activeOrderRow);
                CashierOrderCardDto? order = null;
                if (activeOrderRow is not null)
                {
                    orderById.TryGetValue(activeOrderRow.OrderId, out order);
                }

                var number = t.DisplayTableNumber > 0 ? t.DisplayTableNumber : t.TableId;
                openSessionByTableId.TryGetValue(t.TableId, out var openSession);
                return new CashierTableDto(
                    t.TableId,
                    $"Bàn {number}",
                    t.NumberOfSeats,
                    order is not null || openSession is not null ? "OCCUPIED" : MapTableStatusCode(t.StatusName, t.IsAvailable),
                    order?.OrderId,
                    openSession?.DiningSessionId,
                    openSession is { CanClose: true } && order is null);
            }).ToArray();
        }
        else
        {
            tableCards = orders
                .GroupBy(o => o.TableId)
                .Select(g => new CashierTableDto(g.Key, g.First().TableName, 0, "OCCUPIED", g.First().OrderId))
                .OrderBy(x => x.TableId)
                .ToArray();
        }

        var todayReport = await reportTask;
        var todayOrders = todayReport?.BillCount ?? orderCards.Length;
        var todayRevenue = todayReport?.TotalRevenue ?? orderCards.Sum(x => x.Items.Sum(i => i.LineTotal));

        _ = includeBills;
        _ = billsDate;
        return new CashierDashboardDto(staff, tableCards, orderCards, todayOrders, todayRevenue, BuildAccountDto(staff));
    }

    // The table map must still load if open sessions cannot be read; those tables then show their Catalog status.
    private async Task<IReadOnlyList<OpenDiningSessionResponse>> GetOpenSessionsAsync(int branchId, CancellationToken ct)
    {
        try { return await _ordersClient.GetOpenDiningSessionsAsync(branchId, ct); }
        catch (Exception ex) when (ex is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Open dining sessions unavailable for branch {BranchId} ({ErrorType}).", branchId, ex.GetType().Name);
            return Array.Empty<OpenDiningSessionResponse>();
        }
    }

    // "Đóng bàn": the guest left and nothing is left to pay. Orders re-checks; nothing closes on its own.
    [StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("dining-sessions/{sessionId:int}/close")]
    public async Task<IActionResult> CloseDiningSession(int sessionId, CancellationToken ct)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);
        try
        {
            var session = await _ordersClient.StaffCloseDiningSessionAsync(sessionId, new StaffServiceRequestCommand(staff.BranchId, staff.EmployeeId), ct);
            return Ok(new { success = true, message = "Đã đóng bàn.", tableId = session?.TableId });
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "close_session_failed", ex.Message, ex.StatusCode); }
    }

    private CashierBillHistoryItemDto MapBill(CashierBillSummaryDto bill) => new(
        bill.BillId,
        bill.BillCode,
        bill.BillTime,
        bill.OrderCode ?? string.Empty,
        bill.TableName,
        bill.Subtotal,
        bill.Discount,
        bill.PointsDiscount,
        bill.PointsUsed,
        bill.TotalAmount,
        bill.PaymentMethod,
        bill.PaymentAmount,
        bill.ChangeAmount,
        bill.CustomerName ?? string.Empty,
        bill.OrderId,
        bill.AutoConfirmed);

    private CashierAccountDto BuildAccountDto(StaffSessionUserDto staff) => new(
        staff.EmployeeId,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeName) ?? staff.Name,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeUsername) ?? staff.Username,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeEmail) ?? string.Empty,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeePhone) ?? string.Empty,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeBranchName) ?? staff.BranchName,
        HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeRoleName) ?? staff.RoleName);

    private static string MapTableStatusCode(string statusName, bool isAvailable)
    {
        if (isAvailable) return "AVAILABLE";
        var text = (statusName ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Contains("available") || text.Contains("empty") || text.Contains("trong") || text.Contains("trống")) return "AVAILABLE";
        return "OCCUPIED";
    }

    private static List<int> NormalizeSelectedTableIds(IReadOnlyList<int>? tableIds)
    {
        var result = new List<int>();
        if (tableIds is null)
        {
            return result;
        }

        foreach (var tableId in tableIds)
        {
            if (tableId <= 0 || result.Contains(tableId))
            {
                continue;
            }

            result.Add(tableId);
        }

        return result;
    }

    private async Task OccupyAssignedTablesAsync(IReadOnlyList<int> tableIds, int? primaryTableIdToSkip, CancellationToken cancellationToken)
    {
        foreach (var assignedTableId in tableIds)
        {
            if (primaryTableIdToSkip == assignedTableId)
            {
                continue;
            }

            await _ordersClient.OccupyTableAsync(assignedTableId, cancellationToken);
        }
    }

    private static string ResolveDishImage(string? rawImage, string? dishName)
    {
        var normalized = NormalizeImagePath(rawImage);
        var slug = SlugifyDishName(dishName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            if (normalized.Contains("/images/dish_", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(slug))
            {
                return $"/images/{slug}.jpg";
            }

            return normalized;
        }

        return string.IsNullOrWhiteSpace(slug) ? "/images/placeholder-dish.svg" : $"/images/{slug}.jpg";
    }

    private static string? NormalizeImagePath(string? rawImage)
    {
        if (string.IsNullOrWhiteSpace(rawImage)) return null;
        var image = rawImage.Trim().Replace('\\', '/');
        if (image.StartsWith("~/", StringComparison.Ordinal)) image = "/" + image[2..];
        if (!image.StartsWith("/", StringComparison.Ordinal) &&
            !image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !image.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            image = "/" + image;
        }
        if (image.StartsWith("/Images/", StringComparison.OrdinalIgnoreCase)) image = "/images/" + image["/Images/".Length..];
        return image;
    }

    private static string? SlugifyDishName(string? dishName)
    {
        if (string.IsNullOrWhiteSpace(dishName)) return null;
        var normalized = dishName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            var output = ch switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => char.ToLowerInvariant(ch)
            };
            if (char.IsLetterOrDigit(output))
            {
                builder.Append(output);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }
        return builder.ToString().Trim('-');
    }

    private void ApplyStaffSession(StaffLoginResponse login)
    {
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeId, login.EmployeeId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeUsername, login.Username);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeName, login.Name);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeePhone, login.Phone ?? string.Empty);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeEmail, login.Email ?? string.Empty);
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeRoleId, login.RoleId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeRoleCode, login.RoleCode);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeRoleName, login.RoleName);
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeBranchId, login.BranchId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeBranchName, login.BranchName);
    }

    private StaffSessionDto BuildSessionDto() => new(RequireStaff() is not null, RequireStaff(), "/Staff/Account/Login");
    private StaffSessionUserDto? RequireCashier() => RequireStaff(CashierRoles);

    private StaffSessionUserDto? RequireStaff(params string[] allowedRoles)
    {
        var employeeId = HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeId);
        var roleCode = HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeRoleCode);
        var branchId = HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeBranchId);
        if (employeeId is null || employeeId <= 0 || string.IsNullOrWhiteSpace(roleCode) || branchId is null || branchId <= 0)
        {
            return null;
        }
        if (allowedRoles.Length > 0 && !allowedRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return new StaffSessionUserDto(
            employeeId.Value,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeUsername) ?? string.Empty,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeName) ?? string.Empty,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeePhone),
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeEmail),
            HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeRoleId) ?? 0,
            roleCode,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeRoleName) ?? string.Empty,
            branchId.Value,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeBranchName) ?? string.Empty);
    }

    private void ClearStaffSession()
    {
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeUsername);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeName);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeePhone);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeEmail);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleCode);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleName);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeBranchId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeBranchName);
    }

    private ActionResult Error(string code, string message, int statusCode, object? details = null)
        => StatusCode(statusCode, new ApiErrorResponse(false, code, message, details));
}
