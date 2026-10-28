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
public sealed class MobileBillingGatewayController : ControllerBase
{
    private readonly BillingClient _billing;
    private readonly IHubContext<MobileNotificationsHub> _hub;
    public MobileBillingGatewayController(BillingClient billing, IHubContext<MobileNotificationsHub> hub) { _billing = billing; _hub = hub; }

    [HttpPost("dining-sessions/{sessionId:int}/checkout/quote")]
    public Task<ActionResult<object>> Quote(int sessionId, CancellationToken ct) => Command("quote", new { sessionId }, (actor, key, hash) => _billing.CreateMobileQuoteAsync(sessionId, new { customerId = actor, idempotencyKey = $"c{actor}:quote:{key}", requestHash = hash }, ct));

    [HttpGet("payment-options")]
    public Task<ActionResult<object>> PaymentOptions(CancellationToken ct) => Get(() => _billing.GetMobilePaymentOptionsAsync(ct));

    [HttpGet("me/vouchers")]
    public async Task<ActionResult<object>> Vouchers([FromQuery] int? quoteId, CancellationToken ct) { var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { return Ok(await _billing.GetMobileVouchersAsync(actor.Value, quoteId, ct)); } catch (ApiClientException ex) { return Downstream(ex); } }

    [HttpPost("checkout/quotes/{quoteId:int}/voucher")]
    public async Task<ActionResult<object>> Voucher(int quoteId, ApplyVoucherRequest request, CancellationToken ct) => await Simple(() => _billing.ApplyVoucherAsync(quoteId, new { customerId = Actor(), request.VoucherId }, ct));

    /// <summary>"Không dùng khuyến mãi" ({ use: false }) and "Dùng khuyến mãi" ({ use: true }) on an open quote.</summary>
    [HttpPost("checkout/quotes/{quoteId:int}/promotion")]
    public async Task<ActionResult<object>> Promotion(int quoteId, PromotionChoiceRequest request, CancellationToken ct) => await Simple(() => _billing.ChoosePromotionAsync(quoteId, new { customerId = Actor(), request.Use }, ct));

    /// <summary>The stored quote (after Back the app reloads it instead of trusting its own copy).</summary>
    [HttpGet("checkout/quotes/{quoteId:int}")]
    public Task<ActionResult<object>> GetQuote(int quoteId, CancellationToken ct) => Get(() => _billing.GetMobileQuoteAsync(quoteId, Actor()!.Value, ct));

    [HttpPost("checkout/quotes/{quoteId:int}/loyalty")]
    public async Task<ActionResult<object>> Loyalty(int quoteId, ApplyLoyaltyRequest request, CancellationToken ct) => await Simple(() => _billing.ApplyLoyaltyAsync(quoteId, new { customerId = Actor(), request.Points }, ct));

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("dining-sessions/{sessionId:int}/payment-request")]
    public async Task<ActionResult<object>> RequestPayment(int sessionId, PaymentRequestBody request, CancellationToken ct)
    { var result = await Simple(() => _billing.RequestMobilePaymentAsync(sessionId, new { customerId = Actor(), request.CheckoutQuoteId }, ct)); if (result.Result is OkObjectResult) await Notify("paymentUpdated", new { sessionId, status = "PAYMENT_REQUESTED" }, ct); return result; }

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("dining-sessions/{sessionId:int}/payment-request/cancel")]
    public Task<ActionResult<object>> CancelPayment(int sessionId, CancellationToken ct) => Simple(() => _billing.CancelMobilePaymentRequestAsync(sessionId, new { customerId = Actor() }, ct));

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("checkout/quotes/{quoteId:int}/payments")]
    public Task<ActionResult<object>> Payment(int quoteId, CreateMobilePaymentBody request, CancellationToken ct) => Command("payment", new { quoteId, request.Channel, request.Method, request.Location },
        (actor, key, hash) => _billing.CreateMobilePaymentAsync(quoteId, new { customerId = actor, request.Channel, request.Method, request.Location, idempotencyKey = $"c{actor}:payment:{key}", requestHash = hash }, ct), true, ct);

    [HttpGet("payments/{paymentId:int}/transfer-qr")]
    public Task<ActionResult<object>> TransferQr(int paymentId, CancellationToken ct) => Get(() => _billing.GetMobileTransferQrAsync(paymentId, Actor()!.Value, ct));

    /// <summary>The waiting payment (with its quote) the app returns to instead of starting a second one; 404 when none.</summary>
    [HttpGet("dining-sessions/{sessionId:int}/active-payment")]
    public async Task<ActionResult<object>> ActivePayment(int sessionId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try { return Ok(await _billing.GetMobileActivePaymentAsync(sessionId, actor.Value, ct)); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpGet("payments/{paymentId:int}")]
    public async Task<ActionResult<object>> Payment(int paymentId, CancellationToken ct) { var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { return Ok(await _billing.GetMobilePaymentAsync(paymentId, actor.Value, ct)); } catch (ApiClientException ex) { return Downstream(ex); } }

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("payments/{paymentId:int}/retry")]
    public Task<ActionResult<object>> Retry(int paymentId, CancellationToken ct) => Command("payment-retry", new { paymentId },
        (actor, key, hash) => _billing.RetryMobilePaymentAsync(paymentId, new { customerId = actor, idempotencyKey = $"c{actor}:payment-retry:{key}", requestHash = hash }, ct), true, ct);

    [HttpGet("dining-sessions/{sessionId:int}/invoice")]
    public Task<ActionResult<object>> SessionInvoice(int sessionId, CancellationToken ct) => Get(() => _billing.GetSessionInvoiceAsync(sessionId, Actor()!.Value, ct));
    [HttpGet("invoices/{invoiceId:int}")]
    public Task<ActionResult<object>> Invoice(int invoiceId, CancellationToken ct) => Get(() => _billing.GetInvoiceAsync(invoiceId, Actor()!.Value, ct));
    [HttpGet("invoices")]
    public async Task<ActionResult<object>> Invoices([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        if (limit is < 1 or > 100) return Error("validation_failed", "limit phải từ 1 đến 100.", 400);
        try { return Ok(await _billing.GetCustomerInvoicesAsync(actor.Value, limit, ct)); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpPost("dining-sessions/{sessionId:int}/split-bill")]
    public Task<ActionResult<object>> Split(int sessionId, CreateSplitBillBody request, CancellationToken ct) => Command("split", new { sessionId, request.CheckoutQuoteId, request.Amounts },
        (actor, key, hash) => _billing.CreateSplitBillAsync(sessionId, new { customerId = actor, request.CheckoutQuoteId, amounts = request.Amounts, idempotencyKey = $"c{actor}:split:{key}", requestHash = hash }, ct));

    [HttpGet("dining-sessions/{sessionId:int}/split-bill")]
    public Task<ActionResult<object>> SessionSplit(int sessionId, CancellationToken ct) => Get(() => _billing.GetSessionSplitBillAsync(sessionId, Actor()!.Value, ct));

    [HttpGet("split-bills/{splitBillId:int}")]
    public Task<ActionResult<object>> Split(int splitBillId, CancellationToken ct) => Get(() => _billing.GetSplitBillAsync(splitBillId, Actor()!.Value, ct));

    [StaffBranchEvent(StaffEventSource.Mobile, StaffBranchEvents.OnsitePayments)]
    [HttpPost("split-bills/{splitBillId:int}/payments")]
    public Task<ActionResult<object>> SplitPayment(int splitBillId, PaySplitBillBody request, CancellationToken ct) => Command("split-payment", new { splitBillId, request.SplitBillPartId, request.Channel, request.Method, request.Location },
        (actor, key, hash) => _billing.PaySplitBillAsync(splitBillId, new { customerId = actor, request.SplitBillPartId, request.Channel, request.Method, request.Location, idempotencyKey = $"c{actor}:split-payment:{key}", requestHash = hash }, ct), true, ct);

    private async Task<ActionResult<object>> Command(string scope, object body, Func<int, string, string, Task<JsonElement>> action, bool notify = false, CancellationToken ct = default)
    {
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim(); if (string.IsNullOrWhiteSpace(key) || key.Length > 64) return Error("idempotency_key_required", "Idempotency-Key là bắt buộc và tối đa 64 ký tự.", 400);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)))).ToLowerInvariant();
        try { var value = await action(actor.Value, key, hash); if (notify) await Notify("paymentUpdated", value, ct); return Ok(value); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }
    private async Task<ActionResult<object>> Simple(Func<Task<JsonElement>> action) { if (Actor() is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { return Ok(await action()); } catch (ApiClientException ex) { return Downstream(ex); } }
    private async Task<ActionResult<object>> Get(Func<Task<JsonElement>> action) => await Simple(action);
    private async Task Notify(string name, object value, CancellationToken ct) { var actor = Actor(); if (actor is not null) await _hub.Clients.Group(MobileNotificationsHub.CustomerGroup(actor.Value)).SendAsync(name, value, ct); }
    private int? Actor() => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;
    private ObjectResult Downstream(ApiClientException ex) => Error(ex.Code ?? "billing_failed", ex.Message, ex.StatusCode);
    private ObjectResult Error(string code, string message, int status) => StatusCode(status, MobileApiError.Create(code, message, Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier));
}
