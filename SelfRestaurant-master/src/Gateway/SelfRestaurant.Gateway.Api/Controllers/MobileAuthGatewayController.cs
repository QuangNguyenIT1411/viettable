using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Route("api/mobile/v1/auth")]
public sealed class MobileAuthGatewayController : ControllerBase
{
    private readonly IdentityClient _identity;
    private readonly ILogger<MobileAuthGatewayController> _logger;

    public MobileAuthGatewayController(IdentityClient identity, ILogger<MobileAuthGatewayController> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    [HttpPost("register")]
    public Task<ActionResult<MobileTokenResponse>> Register(MobileRegisterRequest request, CancellationToken ct) =>
        ExecuteTokenAsync(() => _identity.MobileRegisterAsync(request, ct), "register_failed");

    [HttpPost("login")]
    public Task<ActionResult<MobileTokenResponse>> Login(MobileLoginRequest request, CancellationToken ct) =>
        ExecuteTokenAsync(() => _identity.MobileLoginAsync(request, ct), "login_failed");

    [HttpPost("google")]
    public Task<ActionResult<MobileTokenResponse>> Google(MobileGoogleRequest request, CancellationToken ct) =>
        ExecuteTokenAsync(() => _identity.MobileGoogleAsync(request, ct), "google_login_failed");

    [HttpPost("refresh")]
    public Task<ActionResult<MobileTokenResponse>> Refresh(MobileRefreshRequest request, CancellationToken ct) =>
        ExecuteTokenAsync(() => _identity.MobileRefreshAsync(request, ct), "refresh_failed");

    [HttpPost("logout")]
    public async Task<ActionResult<object>> Logout(MobileRefreshRequest request, CancellationToken ct)
    {
        try
        {
            await _identity.MobileLogoutAsync(request, ct);
            return Ok(new { success = true });
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? "logout_failed", ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<MobileForgotPasswordResponse>> ForgotPassword(MobileForgotPasswordRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _identity.MobileForgotPasswordAsync(request, ct);
            return response is null
                ? MobileError("identity_unavailable", "Dịch vụ xác thực không phản hồi.", 502)
                : Ok(response);
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? "forgot_password_failed", ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<object>> ResetPassword(MobileResetPasswordRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfirmPassword)
            && !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return MobileError("password_mismatch", "Mật khẩu xác nhận không khớp.", 400);
        try
        {
            await _identity.MobileResetPasswordAsync(request, ct);
            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công." });
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? "reset_password_failed", ex.Message, ex.StatusCode);
        }
    }

    [Authorize(Roles = "customer")]
    [HttpPost("change-password")]
    public async Task<ActionResult<object>> ChangePassword(MobileChangePasswordRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfirmPassword)
            && !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return MobileError("password_mismatch", "Mật khẩu xác nhận không khớp.", 400);
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(subject, out var customerId) || customerId <= 0)
            return MobileError("invalid_actor", "Access token không chứa khách hàng hợp lệ.", 401);
        try
        {
            await _identity.MobileChangePasswordAsync(
                new MobileChangePasswordInternalRequest(customerId, request.CurrentPassword, request.NewPassword), ct);
            return Ok(new { success = true, message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? "change_password_failed", ex.Message, ex.StatusCode);
        }
    }

    private async Task<ActionResult<MobileTokenResponse>> ExecuteTokenAsync(
        Func<Task<MobileTokenResponse?>> action, string fallbackCode)
    {
        try
        {
            var response = await action();
            return response is null
                ? MobileError("identity_unavailable", "Dịch vụ xác thực không phản hồi.", 502)
                : Ok(response);
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? fallbackCode, ex.Message, ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected mobile authentication failure.");
            return MobileError("identity_unavailable", "Dịch vụ xác thực tạm thời không khả dụng.", 503);
        }
    }

    private ObjectResult MobileError(string code, string message, int statusCode)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(statusCode, MobileApiError.Create(code, message, correlationId));
    }
}
