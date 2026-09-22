using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Route("api/gateway/customer")]
public sealed class CustomerGatewayController : ControllerBase
{
    private static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CatalogClient _catalogClient;
    private readonly OrdersClient _ordersClient;
    private readonly CustomersClient _customersClient;
    private readonly IdentityClient _identityClient;
    private readonly BillingClient _billingClient;
    private readonly CustomerDishRecommendationService _recommendationService;
    private readonly IHubContext<CustomerNotificationsHub> _customerNotificationsHub;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CustomerGatewayController> _logger;

    public CustomerGatewayController(
        CatalogClient catalogClient,
        OrdersClient ordersClient,
        CustomersClient customersClient,
        IdentityClient identityClient,
        BillingClient billingClient,
        CustomerDishRecommendationService recommendationService,
        IHubContext<CustomerNotificationsHub> customerNotificationsHub,
        IHostEnvironment environment,
        ILogger<CustomerGatewayController> logger)
    {
        _catalogClient = catalogClient;
        _ordersClient = ordersClient;
        _customersClient = customersClient;
        _identityClient = identityClient;
        _billingClient = billingClient;
        _recommendationService = recommendationService;
        _customerNotificationsHub = customerNotificationsHub;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("session")]
    public ActionResult<CustomerSessionDto> GetSession() => Ok(BuildSessionDto());

    [HttpPost("/api/customer/reservations")]
    public async Task<ActionResult<ReservationDto>> CreateReservation(
        [FromBody] CreateReservationApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = RequireCustomer();
            var enrichedRequest = customer is null
                ? request
                : request with
                {
                    CustomerId = request.CustomerId is > 0 ? request.CustomerId : customer.CustomerId,
                    CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? customer.Name : request.CustomerName,
                    PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? customer.PhoneNumber : request.PhoneNumber
                };

            var reservation = await _customersClient.CreateReservationAsync(enrichedRequest, cancellationToken);
            return reservation is null
                ? Error("create_reservation_failed", "Không thể tạo đặt bàn.", 400)
                : Ok(reservation);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "create_reservation_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("/api/customer/reservations/{code}")]
    public async Task<ActionResult<ReservationDto>> GetReservation(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error("invalid_reservation_code", "Mã đặt bàn không hợp lệ.", 400);
        }

        try
        {
            var customer = RequireCustomer();
            if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
            var reservation = await _customersClient.GetReservationByCodeAsync(code.Trim(), cancellationToken);
            if (reservation is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            if (!IsOwnedByCustomer(reservation, customer)) return Error("forbidden", "Bạn không có quyền xem đặt bàn này.", 403);
            return Ok(reservation);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "get_reservation_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("/api/customer/reservations/my")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetMyReservations(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);

        try
        {
            var reservations = await _customersClient.GetCustomerReservationsAsync(customer.CustomerId, cancellationToken);
            return Ok(reservations);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "get_reservations_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("/api/customer/reservations/{reservationId:int}/preorder-items")]
    public async Task<ActionResult<ReservationDto>> ReplaceReservationPreOrderItems(
        int reservationId,
        [FromBody] ReplaceReservationPreOrderItemsApiRequest request,
        CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return Error("invalid_reservation", "Đặt bàn không hợp lệ.", 400);
        }

        try
        {
            var customer = RequireCustomer();
            if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
            var existing = await _customersClient.GetReservationByIdAsync(reservationId, cancellationToken);
            if (existing is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            if (!IsOwnedByCustomer(existing, customer)) return Error("forbidden", "Bạn không có quyền cập nhật đặt bàn này.", 403);
            var reservation = await _customersClient.ReplaceReservationPreOrderItemsAsync(reservationId, request, cancellationToken);
            return reservation is null
                ? Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404)
                : Ok(reservation);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "update_preorder_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("/api/customer/reservations/{reservationId:int}/cancel")]
    public async Task<ActionResult<ReservationDto>> CancelReservation(int reservationId, CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return Error("invalid_reservation", "Đặt bàn không hợp lệ.", 400);
        }

        try
        {
            var customer = RequireCustomer();
            if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
            var existing = await _customersClient.GetReservationByIdAsync(reservationId, cancellationToken);
            if (existing is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            if (!IsOwnedByCustomer(existing, customer)) return Error("forbidden", "Bạn không có quyền hủy đặt bàn này.", 403);
            var reservation = await _customersClient.CancelReservationAsync(reservationId, cancellationToken);
            return reservation is null
                ? Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404)
                : Ok(reservation);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "cancel_reservation_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("/api/customer/reservations/{code}/menu")]
    public async Task<ActionResult<MenuResponse>> GetReservationMenu(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error("invalid_reservation_code", "Mã đặt bàn không hợp lệ.", 400);
        }

        try
        {
            var customer = RequireCustomer();
            if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
            var reservation = await _customersClient.GetReservationByCodeAsync(code.Trim(), cancellationToken);
            if (reservation is null)
            {
                return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            }
            if (!IsOwnedByCustomer(reservation, customer)) return Error("forbidden", "Bạn không có quyền xem đặt bàn này.", 403);
            if (reservation.BranchId <= 0)
            {
                return Error("missing_branch", "Không tìm thấy chi nhánh của đặt bàn.", 400);
            }

            var menu = await _catalogClient.GetMenuAsync(reservation.BranchId, cancellationToken: cancellationToken);
            return menu is null
                ? Error("menu_not_found", "Không tìm thấy thực đơn cho chi nhánh của đặt bàn.", 404)
                : Ok(menu);
        }
        catch (ApiClientException ex)
        {
            return Error(string.IsNullOrWhiteSpace(ex.Code) ? "get_reservation_menu_failed" : ex.Code!, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("session/sync-active-order")]
    public async Task<ActionResult<CustomerSessionDto>> SyncSessionFromActiveOrder(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Ok(BuildSessionDto());

        await SyncTableContextFromActiveOrderAsync(customer.CustomerId, cancellationToken);
        return Ok(BuildSessionDto());
    }

    [HttpPost("auth/login")]
    public async Task<ActionResult<object>> Login([FromBody] CustomerLoginApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ thông tin.", 400);
        }

        try
        {
            var login = await _identityClient.LoginAsync(new LoginRequest(request.Username.Trim(), request.Password), cancellationToken);
            if (login is null)
            {
                return Error("invalid_credentials", "Sai tài khoản hoặc mật khẩu.", 401);
            }

            ApplyLoginSession(login);
            return Ok(new { success = true, session = BuildSessionDto(), nextPath = BuildTableContextDto() is null ? "/Home/Index" : "/Menu/Index" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer login failed.");
            return Error("login_failed", ex.Message, 400);
        }
    }

    [HttpPost("auth/google")]
    public async Task<ActionResult<object>> GoogleLogin([FromBody] CustomerGoogleLoginApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Error("invalid_request", "Thiếu mã đăng nhập Google.", 400);
        }

        try
        {
            var login = await _identityClient.GoogleLoginAsync(new GoogleLoginRequest(request.IdToken.Trim()), cancellationToken);
            if (login is null)
            {
                return Error("invalid_google_login", "Không thể đăng nhập bằng Google.", 401);
            }

            ApplyLoginSession(login);
            return Ok(new { success = true, session = BuildSessionDto(), nextPath = BuildTableContextDto() is null ? "/Home/Index" : "/Menu/Index" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer Google login failed.");
            return Error("google_login_failed", ex.Message, 400);
        }
    }

    [HttpPost("auth/register")]
    public async Task<ActionResult<object>> Register([FromBody] CustomerRegisterApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ thông tin.", 400);
        }

        if (request.Password.Trim().Length < 6)
        {
            return Error("invalid_password", "Mật khẩu phải có ít nhất 6 ký tự.", 400);
        }

        try
        {
            await _identityClient.RegisterAsync(new RegisterRequest(
                request.Name.Trim(), request.Username.Trim(), request.Password, request.PhoneNumber.Trim(),
                request.Email.Trim(),
                string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
                request.DateOfBirth, string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim()), cancellationToken);
            return Ok(new { success = true, message = "Đăng ký thành công! Vui lòng đăng nhập.", nextPath = "/Customer/Login" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer register failed.");
            return Error("register_failed", ex.Message, 400);
        }
    }

    [HttpPost("auth/logout")]
    public ActionResult<object> Logout()
    {
        ClearCustomerSession(false);
        return Ok(new { success = true, nextPath = "/Home/Index" });
    }

    [HttpPost("auth/forgot-password")]
    public async Task<ActionResult<CustomerForgotPasswordResultDto>> ForgotPassword([FromBody] CustomerForgotPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmailOrPhone))
        {
            return Error("invalid_request", "Vui lòng nhập email.", 400);
        }

        try
        {
            var result = await _identityClient.ForgotPasswordAsync(new ForgotPasswordRequest(request.UsernameOrEmailOrPhone.Trim()), cancellationToken);
            if (result is null)
            {
                return Error("forgot_password_failed", "Không nhận được phản hồi từ dịch vụ xác thực.", 502);
            }

            var resetPath = string.IsNullOrWhiteSpace(result.ResetToken)
                ? null
                : $"/Customer/ResetPassword?token={Uri.EscapeDataString(result.ResetToken)}";
            return Ok(new CustomerForgotPasswordResultDto(
                "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu.",
                result.ResetToken,
                result.ExpiresAt,
                resetPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer forgot password failed.");
            return Error("forgot_password_failed", "Có lỗi xảy ra. Vui lòng thử lại sau.", 400);
        }
    }

    [HttpGet("auth/reset-password/validate")]
    public async Task<ActionResult<object>> ValidateResetPasswordToken([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Error("missing_token", "Link không hợp lệ.", 400);
        }

        try
        {
            await _identityClient.ValidateResetPasswordTokenAsync(token.Trim(), cancellationToken);
            return Ok(new { valid = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer reset password token validation failed.");
            return Error("invalid_token", ex.Message, 400);
        }
    }

    [HttpPost("auth/reset-password")]
    public async Task<ActionResult<object>> ResetPassword([FromBody] CustomerResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Error("invalid_request", "Vui lòng điền đầy đủ thông tin.", 400);
        }
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Error("password_mismatch", "Mật khẩu xác nhận không khớp.", 400);
        }
        if (request.NewPassword.Trim().Length < 6)
        {
            return Error("invalid_password", "Mật khẩu phải có ít nhất 6 ký tự.", 400);
        }

        try
        {
            await _identityClient.ResetPasswordAsync(new ResetPasswordRequest(request.Token.Trim(), request.NewPassword), cancellationToken);
            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập với mật khẩu mới.", nextPath = "/Customer/Login" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer reset password failed.");
            return Error("reset_password_failed", ex.Message, 400);
        }
    }

    [HttpPost("auth/change-password")]
    public async Task<ActionResult<object>> ChangePassword([FromBody] CustomerChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Error("invalid_request", "Vui lòng điền đầy đủ thông tin mật khẩu.", 400);
        }
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Error("password_mismatch", "Mật khẩu mới và xác nhận không khớp.", 400);
        }
        if (request.NewPassword.Trim().Length < 6)
        {
            return Error("invalid_password", "Mật khẩu mới phải có ít nhất 6 ký tự.", 400);
        }

        try
        {
            await _identityClient.ChangePasswordAsync(new ChangePasswordRequest(customer.CustomerId, request.CurrentPassword, request.NewPassword), cancellationToken);
            return Ok(new { success = true, message = "Đổi mật khẩu thành công!" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer change password failed.");
            return Error("change_password_failed", ex.Message, 400);
        }
    }

    [HttpGet("branches")]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches(CancellationToken cancellationToken)
        => Ok(await _catalogClient.GetBranchesAsync(cancellationToken) ?? Array.Empty<BranchDto>());

    [HttpGet("branches/{branchId:int}/tables")]
    public async Task<ActionResult<object>> GetBranchTables(int branchId, CancellationToken cancellationToken)
    {
        if (branchId <= 0) return Error("invalid_branch", "Chi nhánh không hợp lệ.", 400);
        var response = await _catalogClient.GetBranchTablesAsync(branchId, cancellationToken);
        return response is null ? Error("branch_not_found", "Không tìm thấy bàn cho chi nhánh này.", 404) : Ok(response);
    }

    [HttpGet("tables/qr/{code}")]
    public async Task<ActionResult<BranchTableDto>> GetTableByQr(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return Error("invalid_qr", "Mã QR không hợp lệ.", 400);
        var table = await _catalogClient.GetTableByQrAsync(code.Trim(), cancellationToken);
        return table is null ? Error("table_not_found", "Không tìm thấy bàn tương ứng với mã QR.", 404) : Ok(table);
    }

    [HttpPost("context/table")]
    public async Task<ActionResult<CustomerTableContextDto>> SetTableContext([FromBody] SetCustomerTableContextRequest request, CancellationToken cancellationToken)
    {
        if (request.TableId <= 0 || request.BranchId <= 0) return Error("invalid_context", "Bàn hoặc chi nhánh không hợp lệ.", 400);
        try
        {
            var branch = await _catalogClient.GetBranchTablesAsync(request.BranchId, cancellationToken);
            if (branch is null) return Error("branch_not_found", "Không tìm thấy chi nhánh.", 404);
            var table = branch.Tables.FirstOrDefault(t => t.TableId == request.TableId && t.BranchId == request.BranchId);
            if (table is null) return Error("table_branch_mismatch", "Bàn không thuộc chi nhánh đã chọn.", 400);

            await _ordersClient.OccupyTableAsync(request.TableId, cancellationToken);
            SetCurrentTableContext(table.TableId, table.BranchId, branch.BranchName, table.DisplayTableNumber);
            SaveCustomerTableContext(RequireCustomer()?.CustomerId, table.TableId, table.BranchId, branch.BranchName, table.DisplayTableNumber);
            return Ok(new CustomerTableContextDto(table.TableId, table.BranchId, branch.BranchName, table.DisplayTableNumber));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetTableContext failed.");
            return Error("set_context_failed", ex.Message, 400);
        }
    }

    [HttpDelete("context/table")]
    public ActionResult<object> ClearTableContext()
    {
        ClearCurrentTableContextSession();
        return Ok(new { success = true });
    }

    [HttpPost("context/table/reset")]
    public async Task<ActionResult<object>> ResetCurrentTable(CancellationToken cancellationToken)
    {
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);

        if (RequireCustomer() is null)
        {
            await _ordersClient.ResetAllTablesAsync(cancellationToken);
            ClearCurrentTableContextSession();
            ClearSavedCustomerTableContext();
            return Ok(new { success = true, message = "Đã đặt lại toàn bộ trạng thái bàn về sẵn sàng." });
        }

        await _ordersClient.ResetTableAsync(tableContext.TableId, cancellationToken);
        ClearCurrentTableContextSession();
        ClearSavedCustomerTableContext();
        return Ok(new { success = true, message = "Đã đặt lại bàn hiện tại." });
    }

    [HttpGet("context")]
    public ActionResult<CustomerTableContextDto?> GetCurrentTableContext() => Ok(BuildTableContextDto());

    [HttpGet("profile")]
    public async Task<ActionResult<CustomerProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var profile = await _identityClient.GetCustomerAsync(customer.CustomerId, cancellationToken);
        if (profile is null)
        {
            ClearCustomerSession(false);
            return Error("session_expired", "Không tìm thấy hồ sơ khách hàng.", 401);
        }

        return Ok(MapProfile(profile));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<CustomerProfileDto>> UpdateProfile([FromBody] UpdateCustomerProfileApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return Error("invalid_request", "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.", 400);
        }

        try
        {
            await _identityClient.UpdateCustomerProfileAsync(customer.CustomerId,
                new UpdateCustomerProfileRequest(request.Username.Trim(), request.Name.Trim(), request.PhoneNumber.Trim(),
                    string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
                    request.DateOfBirth,
                    string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim()),
                cancellationToken);

            HttpContext.Session.SetString(SessionKeys.CustomerUsername, request.Username.Trim());
            HttpContext.Session.SetString(SessionKeys.CustomerName, request.Name.Trim());
            HttpContext.Session.SetString(SessionKeys.CustomerPhoneNumber, request.PhoneNumber.Trim());
            if (!string.IsNullOrWhiteSpace(request.Email)) HttpContext.Session.SetString(SessionKeys.CustomerEmail, request.Email.Trim());
            else HttpContext.Session.Remove(SessionKeys.CustomerEmail);

            var profile = await _identityClient.GetCustomerAsync(customer.CustomerId, cancellationToken);
            if (profile is null) return Error("profile_missing", "Không thể tải lại hồ sơ sau khi cập nhật.", 500);
            return Ok(MapProfile(profile));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer update profile failed.");
            return Error("update_profile_failed", ex.Message, 400);
        }
    }

    [HttpGet("menu")]
    public async Task<ActionResult<CustomerMenuScreenDto>> GetMenu(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);

        var menu = await _catalogClient.GetMenuAsync(tableContext.BranchId, cancellationToken: cancellationToken);
        if (menu is null) return Error("menu_not_found", "Không tìm thấy thực đơn.", 404);

        IReadOnlyList<int> topDishIds;
        try { topDishIds = await _ordersClient.GetTopDishIdsAsync(tableContext.BranchId, 5, cancellationToken) ?? Array.Empty<int>(); }
        catch { topDishIds = Array.Empty<int>(); }

        ActiveOrderResponse? activeOrder = null;
        if (customer is not null)
        {
            try { activeOrder = await _ordersClient.GetActiveOrderAsync(tableContext.TableId, cancellationToken); }
            catch { activeOrder = null; }
        }

        return Ok(new CustomerMenuScreenDto(
            tableContext,
            customer,
            menu,
            topDishIds,
            Array.Empty<CustomerDishRecommendationDto>(),
            activeOrder?.OrderId ?? 0));
    }

    [HttpGet("menu/recommendations")]
    public async Task<ActionResult<CustomerMenuRecommendationsDto>> GetMenuRecommendations([FromQuery] int[]? cartDishIds, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);

        var menu = await _catalogClient.GetMenuAsync(tableContext.BranchId, cancellationToken: cancellationToken);
        if (menu is null) return Error("menu_not_found", "Không tìm thấy thực đơn.", 404);

        IReadOnlyList<int> topDishIds;
        try { topDishIds = await _ordersClient.GetTopDishIdsAsync(tableContext.BranchId, 5, cancellationToken) ?? Array.Empty<int>(); }
        catch { topDishIds = Array.Empty<int>(); }

        ActiveOrderResponse? activeOrder = null;
        if (customer is not null)
        {
            try { activeOrder = await _ordersClient.GetActiveOrderAsync(tableContext.TableId, cancellationToken); }
            catch { activeOrder = null; }
        }

        var recommendations = await _recommendationService.GetRecommendationsAsync(
            customer,
            tableContext,
            menu,
            topDishIds,
            activeOrder,
            cartDishIds,
            cancellationToken);

        return Ok(new CustomerMenuRecommendationsDto(recommendations));
    }

    [HttpPost("dev/reset-test-state")]
    public async Task<ActionResult<object>> ResetDevTestState(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        await _customersClient.ResetDevTestStateAsync(cancellationToken);
        await _billingClient.ResetDevTestStateAsync(cancellationToken);
        await _ordersClient.ResetDevTestStateAsync(cancellationToken);
        await _catalogClient.ResetDevTestStateAsync(cancellationToken);

        HttpContext.Session.Clear();

        return Ok(new
        {
            success = true,
            message = "Đã reset dữ liệu test local.",
            nextPath = "/Home/Index"
        });
    }

    [HttpGet("orders/history")]
    public async Task<ActionResult<IReadOnlyList<CustomerOrderHistoryDto>>> GetOrderHistory([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var items = await _customersClient.GetOrdersAsync(customer.CustomerId, Math.Clamp(take, 1, 100), cancellationToken);
        return Ok(items);
    }

    [HttpGet("ready-notifications")]
    public async Task<ActionResult<CustomerReadyNotificationsDto>> GetReadyNotifications(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableId = HttpContext.Session.GetInt32(SessionKeys.CurrentTableId);
        if (tableId is null || tableId <= 0)
        {
            return Ok(new CustomerReadyNotificationsDto(null, Array.Empty<ReadyDishNotificationDto>()));
        }

        var activeOrder = await _ordersClient.GetActiveOrderAsync(tableId.Value, cancellationToken);
        if (activeOrder is null || activeOrder.OrderId <= 0)
        {
            return Ok(new CustomerReadyNotificationsDto(tableId, Array.Empty<ReadyDishNotificationDto>()));
        }

        var activeOrderIds = (activeOrder.ActiveOrderIds is { Count: > 0 } ? activeOrder.ActiveOrderIds : [activeOrder.OrderId])
            .Distinct()
            .ToArray();
        var hasReadyRound = activeOrder.Items.Any(item => string.Equals(item.Status, "READY", StringComparison.OrdinalIgnoreCase));
        if (!hasReadyRound)
        {
            return Ok(new CustomerReadyNotificationsDto(tableId, Array.Empty<ReadyDishNotificationDto>()));
        }

        var items = await _customersClient.GetReadyNotificationsAsync(customer.CustomerId, tableId, cancellationToken);
        items = items
            .Where(item => activeOrderIds.Contains(item.OrderId))
            .ToArray();
        return Ok(new CustomerReadyNotificationsDto(tableId, items));
    }

    [HttpPost("ready-notifications/{notificationId:long}/resolve")]
    public async Task<ActionResult<object>> ResolveReadyNotification(long notificationId, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        if (notificationId <= 0) return Error("invalid_notification", "Thông báo không hợp lệ.", 400);
        var tableId = HttpContext.Session.GetInt32(SessionKeys.CurrentTableId);
        await _customersClient.ResolveReadyNotificationAsync(notificationId, customer.CustomerId, tableId, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet("order")]
    public async Task<ActionResult<ActiveOrderResponse?>> GetActiveOrder(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        return Ok(await _ordersClient.GetActiveOrderAsync(tableContext.TableId, cancellationToken));
    }

    [HttpGet("order/items")]
    public async Task<ActionResult<object>> GetOrderItems(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        var order = await _ordersClient.GetActiveOrderAsync(tableContext.TableId, cancellationToken);
        return Ok(new { success = true, orderId = order?.OrderId, items = order?.Items ?? Array.Empty<ActiveOrderItemDto>(), subtotal = order?.Subtotal ?? 0m });
    }

    [HttpPost("order/items")]
    public async Task<ActionResult<ActiveOrderResponse?>> AddItem([FromBody] AddOrderItemApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (request.DishId <= 0 || request.Quantity <= 0) return Error("invalid_item", "Món ăn hoặc số lượng không hợp lệ.", 400);
        try { return Ok(await _ordersClient.AddItemAsync(tableContext.TableId, request.DishId, request.Quantity, request.Note, request.ExpectedDiningSessionCode, cancellationToken)); }
        catch (Exception ex) { return Error("add_item_failed", ex.Message, 400); }
    }

    [HttpPatch("order/items/{itemId:int}/quantity")]
    public async Task<ActionResult<object>> UpdateQuantity(int itemId, [FromBody] UpdateOrderItemQuantityApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (itemId <= 0 || request.Quantity <= 0) return Error("invalid_item", "Dòng món hoặc số lượng không hợp lệ.", 400);
        try
        {
            await _ordersClient.UpdateQuantityAsync(tableContext.TableId, itemId, request.Quantity, cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Error("update_item_failed", ex.Message, 409);
        }
    }

    [HttpPatch("order/items/{itemId:int}/note")]
    public async Task<ActionResult<object>> UpdateItemNote(int itemId, [FromBody] UpdateOrderItemNoteApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (itemId <= 0) return Error("invalid_item", "Dòng món không hợp lệ.", 400);
        try
        {
            await _ordersClient.UpdateItemNoteAsync(tableContext.TableId, itemId, request.Note, cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Error("update_item_failed", ex.Message, 409);
        }
    }

    [HttpDelete("order/items/{itemId:int}")]
    public async Task<ActionResult<object>> RemoveItem(int itemId, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (itemId <= 0) return Error("invalid_item", "Dòng món không hợp lệ.", 400);
        try
        {
            await _ordersClient.RemoveItemAsync(tableContext.TableId, itemId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Error("remove_item_failed", ex.Message, 409);
        }
    }

    [HttpPost("order/submit")]
    public async Task<ActionResult<object>> SubmitOrder([FromBody] SubmitOrderApiRequest? request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (string.IsNullOrWhiteSpace(request?.IdempotencyKey)) return Error("invalid_submit", "Thiếu khóa xác nhận gửi món.", 400);
        try
        {
            await _ordersClient.SubmitOrderAsync(tableContext.TableId, request.IdempotencyKey, request.ExpectedDiningSessionCode, cancellationToken);
            return Ok(new { success = true, message = "Đã gửi yêu cầu đến bếp" });
        }
        catch (Exception ex)
        {
            return Error("submit_failed", ex.Message, 409);
        }
    }

    [HttpPost("menu/send-order-to-kitchen")]
    public async Task<ActionResult<object>> SubmitMenuOrder([FromBody] SubmitMenuOrderApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);

        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (request.TableId <= 0 || request.BranchId <= 0) return Error("invalid_context", "Bàn hoặc chi nhánh không hợp lệ.", 400);
        if (tableContext.TableId != request.TableId || tableContext.BranchId != request.BranchId)
        {
            return Error("table_context_mismatch", "Bàn hiện tại không khớp với phiên đặt món.", 400);
        }

        var items = (request.Items ?? Array.Empty<AddOrderItemApiRequest>())
            .Where(item => item.DishId > 0 && item.Quantity > 0)
            .Select(item => new AddOrderItemPayload(item.DishId, item.Quantity, item.Note))
            .ToArray();

        if (items.Length == 0)
        {
            return Error("empty_order", "Đơn hàng trống.", 400);
        }
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Error("invalid_submit", "Thiếu khóa xác nhận gửi món.", 400);
        }

        try
        {
            await _ordersClient.SubmitOrderBatchAsync(
                tableContext.TableId,
                items,
                customer.PhoneNumber,
                request.IdempotencyKey,
                request.ExpectedDiningSessionCode,
                cancellationToken);

            return Ok(new { success = true, message = "Đã gửi yêu cầu đến bếp" });
        }
        catch (Exception ex)
        {
            return Error("submit_failed", ex.Message, 409);
        }
    }

    [HttpPost("order/confirm-received")]
    public async Task<ActionResult<object>> ConfirmOrderReceived([FromQuery] int orderId, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);
        try
        {
            await _ordersClient.ConfirmOrderReceivedAsync(orderId, cancellationToken);
            var tableId = HttpContext.Session.GetInt32(SessionKeys.CurrentTableId);
            if (tableId is > 0)
            {
                await _customerNotificationsHub.Clients
                    .Group(CustomerNotificationsHub.TableGroup(tableId.Value))
                    .SendAsync("orderReceived", new { orderId, tableId }, cancellationToken);
            }
            return Ok(new { success = true, message = "Đã xác nhận nhận món. Chúc ngon miệng!" });
        }
        catch (Exception ex)
        {
            return Error("confirm_received_failed", ex.Message, 409);
        }
    }

    [HttpPost("order/scan-loyalty")]
    public async Task<ActionResult<LoyaltyScanResponse?>> ScanLoyalty([FromBody] ScanLoyaltyApiRequest request, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var tableContext = BuildTableContextDto();
        if (tableContext is null) return Error("missing_table_context", "Bạn chưa chọn bàn.", 400);
        if (string.IsNullOrWhiteSpace(request.PhoneNumber)) return Error("invalid_phone", "Số điện thoại không hợp lệ.", 400);
        try
        {
            return Ok(await _ordersClient.ScanLoyaltyCardAsync(tableContext.TableId, request.PhoneNumber.Trim(), cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer loyalty scan failed for table {TableId}", tableContext.TableId);
            return Error("scan_loyalty_failed", ex.Message, 400);
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<CustomerDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is null) return Error("unauthorized", "Bạn cần đăng nhập.", 401);
        var profile = await _identityClient.GetCustomerAsync(customer.CustomerId, cancellationToken);
        if (profile is null)
        {
            ClearCustomerSession(false);
            return Error("session_expired", "Không tìm thấy hồ sơ khách hàng.", 401);
        }

        var orders = await _customersClient.GetOrdersAsync(customer.CustomerId, 10, cancellationToken);
        var recentOrders = orders.Select(o => new CustomerDashboardOrderDto(o.OrderId, o.OrderCode, o.OrderTime, o.StatusCode, o.OrderStatus, o.TotalAmount, o.ItemCount)).ToArray();
        var summary = new CustomerDashboardSummaryDto(recentOrders.Length, recentOrders.Sum(x => x.TotalAmount), recentOrders.Count(x => string.Equals(x.StatusCode, "PENDING", StringComparison.OrdinalIgnoreCase) || string.Equals(x.StatusCode, "CONFIRMED", StringComparison.OrdinalIgnoreCase)), recentOrders.Count(x => string.Equals(x.StatusCode, "COMPLETED", StringComparison.OrdinalIgnoreCase)));
        return Ok(new CustomerDashboardDto(MapProfile(profile), summary, recentOrders));
    }

    private void ApplyLoginSession(LoginResponse login)
    {
        var currentTableContext = BuildTableContextDto();
        HttpContext.Session.SetInt32(SessionKeys.CustomerId, login.CustomerId);
        HttpContext.Session.SetString(SessionKeys.CustomerUsername, login.Username);
        HttpContext.Session.SetString(SessionKeys.CustomerName, login.Name);
        HttpContext.Session.SetString(SessionKeys.CustomerPhoneNumber, login.PhoneNumber);
        HttpContext.Session.SetInt32(SessionKeys.CustomerLoyaltyPoints, login.LoyaltyPoints);
        if (!string.IsNullOrWhiteSpace(login.Email)) HttpContext.Session.SetString(SessionKeys.CustomerEmail, login.Email);
        else HttpContext.Session.Remove(SessionKeys.CustomerEmail);
        RestoreSavedTableContextForCustomer(login.CustomerId, currentTableContext);
    }


    private static bool IsOwnedByCustomer(ReservationDto reservation, CustomerSessionUserDto customer) => reservation.CustomerId == customer.CustomerId;

    private CustomerProfileDto MapProfile(CustomerProfileResponse profile) => new(
        profile.CustomerId,
        profile.Username,
        profile.Name,
        profile.PhoneNumber,
        profile.Email,
        profile.Address,
        profile.Gender,
        profile.DateOfBirth,
        profile.LoyaltyPoints);

    private CustomerSessionDto BuildSessionDto() => new(RequireCustomer() is not null, RequireCustomer(), BuildTableContextDto());

    private CustomerSessionUserDto? RequireCustomer()
    {
        var customerId = HttpContext.Session.GetInt32(SessionKeys.CustomerId);
        if (customerId is null || customerId <= 0) return null;
        return new CustomerSessionUserDto(customerId.Value, HttpContext.Session.GetString(SessionKeys.CustomerUsername) ?? string.Empty, HttpContext.Session.GetString(SessionKeys.CustomerName) ?? string.Empty, HttpContext.Session.GetString(SessionKeys.CustomerPhoneNumber) ?? string.Empty, HttpContext.Session.GetString(SessionKeys.CustomerEmail), HttpContext.Session.GetInt32(SessionKeys.CustomerLoyaltyPoints) ?? 0);
    }

    private CustomerTableContextDto? BuildTableContextDto()
    {
        var tableId = HttpContext.Session.GetInt32(SessionKeys.CurrentTableId);
        var branchId = HttpContext.Session.GetInt32(SessionKeys.CurrentBranchId);
        if (tableId is null || tableId <= 0 || branchId is null || branchId <= 0) return null;
        return new CustomerTableContextDto(tableId.Value, branchId.Value, HttpContext.Session.GetString(SessionKeys.CurrentBranchName), HttpContext.Session.GetInt32(SessionKeys.CurrentTableNumber));
    }

    private void ClearCustomerSession(bool preserveCurrentTableContext)
    {
        HttpContext.Session.Remove(SessionKeys.CustomerId);
        HttpContext.Session.Remove(SessionKeys.CustomerUsername);
        HttpContext.Session.Remove(SessionKeys.CustomerName);
        HttpContext.Session.Remove(SessionKeys.CustomerPhoneNumber);
        HttpContext.Session.Remove(SessionKeys.CustomerEmail);
        HttpContext.Session.Remove(SessionKeys.CustomerLoyaltyPoints);
        if (!preserveCurrentTableContext) ClearCurrentTableContextSession();
    }

    private void SetCurrentTableContext(int tableId, int branchId, string? branchName, int? tableNumber)
    {
        HttpContext.Session.SetInt32(SessionKeys.CurrentTableId, tableId);
        HttpContext.Session.SetInt32(SessionKeys.CurrentBranchId, branchId);
        if (!string.IsNullOrWhiteSpace(branchName)) HttpContext.Session.SetString(SessionKeys.CurrentBranchName, branchName);
        else HttpContext.Session.Remove(SessionKeys.CurrentBranchName);
        if (tableNumber.HasValue) HttpContext.Session.SetInt32(SessionKeys.CurrentTableNumber, tableNumber.Value);
        else HttpContext.Session.Remove(SessionKeys.CurrentTableNumber);
    }

    private void SaveCustomerTableContext(int? customerId, int tableId, int branchId, string? branchName, int? tableNumber)
    {
        if (!customerId.HasValue || customerId <= 0) return;

        var contexts = GetSavedCustomerTableContexts();
        contexts[customerId.Value] = new SavedCustomerTableContext(tableId, branchId, branchName, tableNumber);
        PersistSavedCustomerTableContexts(contexts);
    }

    private void RestoreSavedTableContextForCustomer(int customerId, CustomerTableContextDto? fallbackTableContext)
    {
        if (fallbackTableContext is not null && fallbackTableContext.TableId > 0 && fallbackTableContext.BranchId > 0)
        {
            SetCurrentTableContext(
                fallbackTableContext.TableId,
                fallbackTableContext.BranchId,
                fallbackTableContext.BranchName,
                fallbackTableContext.TableNumber);
            SaveCustomerTableContext(
                customerId,
                fallbackTableContext.TableId,
                fallbackTableContext.BranchId,
                fallbackTableContext.BranchName,
                fallbackTableContext.TableNumber);
            return;
        }

        var contexts = GetSavedCustomerTableContexts();
        if (!contexts.TryGetValue(customerId, out var savedContext) ||
            savedContext.TableId <= 0 ||
            savedContext.BranchId <= 0)
        {
            ClearCurrentTableContextSession();
            return;
        }

        SetCurrentTableContext(
            savedContext.TableId,
            savedContext.BranchId,
            savedContext.BranchName,
            savedContext.TableNumber);
    }

    private async Task SyncTableContextFromActiveOrderAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            ClearCurrentTableContextSession();
            return;
        }

        try
        {
            var activeOrder = await _ordersClient.GetCustomerActiveOrderContextAsync(customerId, cancellationToken);
            if (activeOrder is null || activeOrder.TableId <= 0 || activeOrder.BranchId <= 0)
            {
                return;
            }

            SetCurrentTableContext(
                activeOrder.TableId,
                activeOrder.BranchId,
                activeOrder.BranchName,
                activeOrder.TableNumber);
            SaveCustomerTableContext(
                customerId,
                activeOrder.TableId,
                activeOrder.BranchId,
                activeOrder.BranchName,
                activeOrder.TableNumber);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not sync active order context for customer {CustomerId}", customerId);
        }
    }

    private void ClearCurrentTableContextSession()
    {
        HttpContext.Session.Remove(SessionKeys.CurrentTableId);
        HttpContext.Session.Remove(SessionKeys.CurrentBranchId);
        HttpContext.Session.Remove(SessionKeys.CurrentBranchName);
        HttpContext.Session.Remove(SessionKeys.CurrentTableNumber);
    }

    private void ClearSavedCustomerTableContext()
    {
        var customerId = HttpContext.Session.GetInt32(SessionKeys.CustomerId);
        if (!customerId.HasValue || customerId <= 0)
        {
            HttpContext.Session.Remove(SessionKeys.SavedCustomerTableContexts);
            return;
        }

        var contexts = GetSavedCustomerTableContexts();
        if (contexts.Remove(customerId.Value))
        {
            PersistSavedCustomerTableContexts(contexts);
        }
    }

    private Dictionary<int, SavedCustomerTableContext> GetSavedCustomerTableContexts()
    {
        var raw = HttpContext.Session.GetString(SessionKeys.SavedCustomerTableContexts);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<int, SavedCustomerTableContext>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, SavedCustomerTableContext>>(raw, SessionJsonOptions)
                ?? new Dictionary<int, SavedCustomerTableContext>();
        }
        catch
        {
            return new Dictionary<int, SavedCustomerTableContext>();
        }
    }

    private void PersistSavedCustomerTableContexts(Dictionary<int, SavedCustomerTableContext> contexts)
    {
        if (contexts.Count == 0)
        {
            HttpContext.Session.Remove(SessionKeys.SavedCustomerTableContexts);
            return;
        }

        HttpContext.Session.SetString(
            SessionKeys.SavedCustomerTableContexts,
            JsonSerializer.Serialize(contexts, SessionJsonOptions));
    }

    private ActionResult Error(string code, string message, int statusCode, object? details = null)
        => StatusCode(statusCode, new ApiErrorResponse(false, code, message, details));

    private sealed record SavedCustomerTableContext(int TableId, int BranchId, string? BranchName, int? TableNumber);
}
