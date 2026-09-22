using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1/me")]
public sealed class MobileProfileGatewayController : ControllerBase
{
    private readonly IdentityClient _identity;

    public MobileProfileGatewayController(IdentityClient identity) => _identity = identity;

    [HttpGet]
    public Task<ActionResult<MobileProfileResponse>> GetProfile(CancellationToken ct) =>
        ExecuteAsync(() => _identity.GetMobileProfileAsync(ct), "profile_not_found");

    [HttpPut]
    public Task<ActionResult<MobileProfileResponse>> UpdateProfile(MobileProfileUpdateRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _identity.UpdateMobileProfileAsync(request, ct), "update_profile_failed");

    [HttpGet("loyalty")]
    public Task<ActionResult<MobileLoyaltyResponse>> GetLoyalty(CancellationToken ct) =>
        ExecuteAsync(() => _identity.GetMobileLoyaltyAsync(ct), "loyalty_not_found");

    [HttpGet("settings")]
    public Task<ActionResult<MobileSettingsResponse>> GetSettings(CancellationToken ct) =>
        ExecuteAsync(() => _identity.GetMobileSettingsAsync(ct), "settings_not_found");

    [HttpPut("settings")]
    public Task<ActionResult<MobileSettingsResponse>> UpdateSettings(MobileSettingsUpdateRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _identity.UpdateMobileSettingsAsync(request, ct), "update_settings_failed");

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T?>> action, string fallbackCode)
        where T : class
    {
        try
        {
            var response = await action();
            return response is null
                ? MobileError(fallbackCode, "Không tìm thấy dữ liệu yêu cầu.", 404)
                : Ok(response);
        }
        catch (ApiClientException ex)
        {
            return MobileError(ex.Code ?? fallbackCode, ex.Message, ex.StatusCode, ParseDetails(ex.ResponseBody));
        }
    }

    private static object? ParseDetails(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("details", out var details) ? details.Clone() : null;
        }
        catch { return null; }
    }

    private ObjectResult MobileError(string code, string message, int status, object? details = null)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, details));
    }
}
