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
    StaffBranchEvents staffEvents, ILogger<SePayWebhookGatewayController> logger, CustomersClient customers,
    ReservationDepositCoordinator deposits, CustomerInboxNotifier inbox) : ControllerBase
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
        if (NeedsDepositFollowUp(outcome, Int(result, "reservationId")))
        {
            var reservationId = Int(result, "reservationId")!.Value;
            // Billing recorded the deposit; the booking now waits for the restaurant (or the money becomes a refund).
            // If that fails, answer 5xx: SePay delivers again, Billing sees a duplicate and names the booking again.
            ReservationDepositView? view;
            try { view = await deposits.RecordPaymentAsync(reservationId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Deposit of reservation {ReservationId} not recorded on the booking ({ErrorType}); asking SePay to retry.",
                    reservationId, ex.GetType().Name);
                return StatusCode(503, new { success = false });
            }
            if (view is not null && outcome != "DUPLICATE")
            {
                try
                {
                    await inbox.PushAsync(view.Reservation.CustomerId, ct);
                    await inbox.ReservationChangedAsync(view.Reservation.CustomerId, reservationId, view.Reservation.Status, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { logger.LogWarning("Deposit notification skipped for reservation {ReservationId} ({ErrorType}).", reservationId, ex.GetType().Name); }
            }
        }
        // Auto-confirmed (table freed) or a warning the cashier must handle: refresh that branch's cashier screen.
        if (branchId is > 0 && outcome is not ("DUPLICATE" or "DISABLED" or "IGNORED"))
        {
            try
            {
                await staffEvents.PublishAsync(branchId.Value, outcome.StartsWith("DEPOSIT_", StringComparison.Ordinal)
                    ? [StaffBranchEvents.OnsitePayments, StaffBranchEvents.Reservations]
                    : [StaffBranchEvents.OnsitePayments, StaffBranchEvents.Orders], ct);
            }
            catch (Exception ex) { logger.LogWarning("SePay staff event skipped ({ErrorType}).", ex.GetType().Name); }
        }
        return Ok(new { success = true });
    }

    /// <summary>Deposit money (new, or a re-delivery Billing already recorded) whose booking must be moved on.</summary>
    public static bool NeedsDepositFollowUp(string? outcome, int? reservationId) =>
        reservationId is > 0 && outcome is SelfRestaurant.Deposits.DepositRules.OutcomePaid
            or SelfRestaurant.Deposits.DepositRules.OutcomeLate or "DUPLICATE";

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt32(out var i) ? i : null; // null/absent fields (e.g. no branch) are simply "no value"
}
