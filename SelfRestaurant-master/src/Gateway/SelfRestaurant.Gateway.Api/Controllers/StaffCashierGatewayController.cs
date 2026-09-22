using System.Globalization;
using System.Text;
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

    private readonly BillingClient _billingClient;
    private readonly IdentityClient _identityClient;
    private readonly CatalogClient _catalogClient;
    private readonly CustomersClient _customersClient;
    private readonly OrdersClient _ordersClient;
    private readonly IHubContext<CustomerNotificationsHub> _customerNotificationsHub;
    private readonly ILogger<StaffCashierGatewayController> _logger;

    public StaffCashierGatewayController(
        BillingClient billingClient,
        IdentityClient identityClient,
        CatalogClient catalogClient,
        CustomersClient customersClient,
        OrdersClient ordersClient,
        IHubContext<CustomerNotificationsHub> customerNotificationsHub,
        ILogger<StaffCashierGatewayController> logger)
    {
        _billingClient = billingClient;
        _identityClient = identityClient;
        _catalogClient = catalogClient;
        _customersClient = customersClient;
        _ordersClient = ordersClient;
        _customerNotificationsHub = customerNotificationsHub;
        _logger = logger;
    }

    [HttpGet("session")]
    public ActionResult<StaffSessionDto> GetSession() => Ok(BuildSessionDto());

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
        var userName = HttpContext.Session.GetString(SessionKeys.EmployeeName) ?? "Người dùng";
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

    [HttpGet("/api/cashier/reservations/today")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetTodayReservations(
        [FromQuery] int? branchId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var staff = RequireCashier();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản thu ngân.", 401);

        var targetBranchId = branchId is > 0 ? branchId : staff.BranchId;
        try
        {
            var reservations = await _customersClient.GetTodayReservationsAsync(targetBranchId, status, date, cancellationToken);
            return Ok(reservations);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "get_reservations_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("/api/cashier/reservations/{reservationId:int}/check-in")]
    public async Task<ActionResult<CashierReservationCheckInResultDto>> CheckInReservation(
        int reservationId,
        [FromBody] CashierReservationCheckInRequest? request,
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
                return Error("reservation_not_found", "Kh?ng t?m th?y ??t b?n.", 404);
            }
            if (previousState.BranchId != staff.BranchId)
            {
                return Error("branch_mismatch", "??t b?n kh?ng thu?c chi nh?nh c?a thu ng?n hi?n t?i.", 403);
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

            var reservation = await _customersClient.BeginReservationCheckInAsync(reservationId, cancellationToken);
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
            BuildAccountDto(staff)));
    }

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
                IdempotencyKey: request.IdempotencyKey), cancellationToken);

            if (response is null)
            {
                return Error("checkout_failed", "Không nhận được phản hồi thanh toán.", 502);
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
                HttpContext.Session.SetString(SessionKeys.EmployeeName, profile.Name);
                HttpContext.Session.SetString(SessionKeys.EmployeePhone, profile.Phone ?? string.Empty);
                HttpContext.Session.SetString(SessionKeys.EmployeeEmail, profile.Email ?? string.Empty);
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
        var orders = await _billingClient.GetCashierOrdersAsync(branchId, cancellationToken);
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
            o.Items.Select(i => new CashierOrderItemCardDto(i.DishName, i.Quantity, i.UnitPrice, i.LineTotal, ResolveDishImage(i.Image, i.DishName), i.StatusCode)).ToArray())).ToArray();

        var activeOrderByTableId = orders
            .Where(o => o.TableId > 0 && !string.Equals(o.StatusCode, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            .GroupBy(o => o.TableId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OrderTime).First());

        var orderById = orderCards.ToDictionary(o => o.OrderId);
        IReadOnlyList<CashierTableDto> tableCards;

        var tables = await _catalogClient.GetBranchTablesAsync(branchId, cancellationToken);
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
                return new CashierTableDto(
                    t.TableId,
                    $"Bàn {number}",
                    t.NumberOfSeats,
                    order is not null ? "OCCUPIED" : MapTableStatusCode(t.StatusName, t.IsAvailable),
                    order?.OrderId);
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayReport = await _billingClient.GetBranchReportAsync(branchId, today, cancellationToken);
        var todayOrders = todayReport?.BillCount ?? orderCards.Length;
        var todayRevenue = todayReport?.TotalRevenue ?? orderCards.Sum(x => x.Items.Sum(i => i.LineTotal));

        _ = includeBills;
        _ = billsDate;
        return new CashierDashboardDto(staff, tableCards, orderCards, todayOrders, todayRevenue, BuildAccountDto(staff));
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
        bill.CustomerName ?? string.Empty);

    private CashierAccountDto BuildAccountDto(StaffSessionUserDto staff) => new(
        staff.EmployeeId,
        HttpContext.Session.GetString(SessionKeys.EmployeeName) ?? staff.Name,
        HttpContext.Session.GetString(SessionKeys.EmployeeUsername) ?? staff.Username,
        HttpContext.Session.GetString(SessionKeys.EmployeeEmail) ?? string.Empty,
        HttpContext.Session.GetString(SessionKeys.EmployeePhone) ?? string.Empty,
        HttpContext.Session.GetString(SessionKeys.EmployeeBranchName) ?? staff.BranchName,
        HttpContext.Session.GetString(SessionKeys.EmployeeRoleName) ?? staff.RoleName);

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
        HttpContext.Session.SetInt32(SessionKeys.EmployeeId, login.EmployeeId);
        HttpContext.Session.SetString(SessionKeys.EmployeeUsername, login.Username);
        HttpContext.Session.SetString(SessionKeys.EmployeeName, login.Name);
        HttpContext.Session.SetString(SessionKeys.EmployeePhone, login.Phone ?? string.Empty);
        HttpContext.Session.SetString(SessionKeys.EmployeeEmail, login.Email ?? string.Empty);
        HttpContext.Session.SetInt32(SessionKeys.EmployeeRoleId, login.RoleId);
        HttpContext.Session.SetString(SessionKeys.EmployeeRoleCode, login.RoleCode);
        HttpContext.Session.SetString(SessionKeys.EmployeeRoleName, login.RoleName);
        HttpContext.Session.SetInt32(SessionKeys.EmployeeBranchId, login.BranchId);
        HttpContext.Session.SetString(SessionKeys.EmployeeBranchName, login.BranchName);
    }

    private StaffSessionDto BuildSessionDto() => new(RequireStaff() is not null, RequireStaff(), "/Staff/Account/Login");
    private StaffSessionUserDto? RequireCashier() => RequireStaff(CashierRoles);

    private StaffSessionUserDto? RequireStaff(params string[] allowedRoles)
    {
        var employeeId = HttpContext.Session.GetInt32(SessionKeys.EmployeeId);
        var roleCode = HttpContext.Session.GetString(SessionKeys.EmployeeRoleCode);
        var branchId = HttpContext.Session.GetInt32(SessionKeys.EmployeeBranchId);
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
            HttpContext.Session.GetString(SessionKeys.EmployeeUsername) ?? string.Empty,
            HttpContext.Session.GetString(SessionKeys.EmployeeName) ?? string.Empty,
            HttpContext.Session.GetString(SessionKeys.EmployeePhone),
            HttpContext.Session.GetString(SessionKeys.EmployeeEmail),
            HttpContext.Session.GetInt32(SessionKeys.EmployeeRoleId) ?? 0,
            roleCode,
            HttpContext.Session.GetString(SessionKeys.EmployeeRoleName) ?? string.Empty,
            branchId.Value,
            HttpContext.Session.GetString(SessionKeys.EmployeeBranchName) ?? string.Empty);
    }

    private void ClearStaffSession()
    {
        HttpContext.Session.Remove(SessionKeys.EmployeeId);
        HttpContext.Session.Remove(SessionKeys.EmployeeUsername);
        HttpContext.Session.Remove(SessionKeys.EmployeeName);
        HttpContext.Session.Remove(SessionKeys.EmployeePhone);
        HttpContext.Session.Remove(SessionKeys.EmployeeEmail);
        HttpContext.Session.Remove(SessionKeys.EmployeeRoleId);
        HttpContext.Session.Remove(SessionKeys.EmployeeRoleCode);
        HttpContext.Session.Remove(SessionKeys.EmployeeRoleName);
        HttpContext.Session.Remove(SessionKeys.EmployeeBranchId);
        HttpContext.Session.Remove(SessionKeys.EmployeeBranchName);
    }

    private ActionResult Error(string code, string message, int statusCode, object? details = null)
        => StatusCode(statusCode, new ApiErrorResponse(false, code, message, details));
}

