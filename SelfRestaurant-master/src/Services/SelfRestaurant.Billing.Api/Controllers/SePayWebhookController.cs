using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// Internal leg of the SePay webhook. The public endpoint lives on the Gateway, which forwards the raw body and the
/// X-SePay-Signature / X-SePay-Timestamp headers unchanged; the signature is verified here against those raw bytes.
/// </summary>
[ApiController]
[Route("api/internal/payments/sepay/webhook")]
public sealed class SePayWebhookController(SePayWebhookService service) : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (Request.ContentLength is > MaxBodyBytes) return StatusCode(413, new { code = "payload_too_large" });
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        if (raw.Length > MaxBodyBytes) return StatusCode(413, new { code = "payload_too_large" });
        var result = await service.ProcessAsync(raw, Request.Headers["X-SePay-Timestamp"].ToString(),
            Request.Headers["X-SePay-Signature"].ToString(), DateTimeOffset.UtcNow, ct);
        return StatusCode(result.StatusCode, result);
    }
}
