using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>
/// Public SePay webhook (bank transfer received). No login: the HMAC-SHA256 signature is the authentication and is
/// verified by Billing on the raw body. Never logs the body or the secret.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/payments/sepay/webhook")]
public sealed class SePayWebhookGatewayController(BillingClient billing, CustomerPaymentNotifier notifier,
    StaffBranchEvents staffEvents, ILogger<SePayWebhookGatewayController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (Request.ContentLength is > MaxBodyBytes) return StatusCode(413, new { success = false });
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        if (raw.Length > MaxBodyBytes) return StatusCode(413, new { success = false });

        var (status, result) = await billing.ForwardSePayWebhookAsync(raw,
            Request.Headers["X-SePay-Timestamp"].ToString(), Request.Headers["X-SePay-Signature"].ToString(), ct);
        if (status is < 200 or > 299) return StatusCode(status, new { success = false });

        var outcome = Text(result, "outcome");
        var branchId = Int(result, "branchId");
        if (outcome == "AUTO_CONFIRMED" && Text(result, "paymentStatus") == "SUCCEEDED"
            && Int(result, "customerId") is > 0 and var customerId && Int(result, "paymentId") is > 0 and var paymentId
            && Int(result, "diningSessionId") is > 0 and var sessionId)
        {
            var amount = result.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number
                && a.TryGetDecimal(out var parsed) ? parsed : 0m;
            await notifier.NotifyAsync(customerId, paymentId, sessionId, amount, ct);
        }
        // Auto-confirmed (table freed) or a warning the cashier must handle: refresh that branch's cashier screen.
        if (branchId is > 0 && outcome is not ("DUPLICATE" or "DISABLED" or "IGNORED"))
        {
            try { await staffEvents.PublishAsync(branchId.Value, [StaffBranchEvents.OnsitePayments, StaffBranchEvents.Orders], ct); }
            catch (Exception ex) { logger.LogWarning("SePay staff event skipped ({ErrorType}).", ex.GetType().Name); }
        }
        return Ok(new { success = true });
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt32(out var i) ? i : null; // null/absent fields (e.g. no branch) are simply "no value"
}
