using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Controllers;

[ApiController]
[Route("api/internal/mobile")]
public sealed class MobileCheckoutController : ControllerBase
{
    private readonly BillingDbContext _db;
    private readonly OrdersApiClient _orders;
    private readonly CustomersApiClient _customers;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ILogger<MobileCheckoutController> _logger;
    private readonly PaymentFinalizationService _finalization;

    public MobileCheckoutController(BillingDbContext db, OrdersApiClient orders, CustomersApiClient customers,
        IPaymentProvider paymentProvider, ILogger<MobileCheckoutController> logger, PaymentFinalizationService finalization)
    {
        _db = db; _orders = orders; _customers = customers; _paymentProvider = paymentProvider; _logger = logger;
        _finalization = finalization;
    }

    [HttpPost("dining-sessions/{sessionId:int}/checkout/quote")]
    public async Task<ActionResult<object>> CreateQuote(int sessionId, QuoteRequest request, CancellationToken ct)
    {
        if (!ValidCommand(request.CustomerId, request.IdempotencyKey, request.RequestHash)) return Validation();
        var replay = await _db.CheckoutQuotes.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(await QuoteBodyAsync(replay, ct)) : IdempotencyConflict();
        var blocking = await BlockingPaymentAsync(sessionId, null, ct);
        if (blocking is not null) return PaymentInProgress(blocking);
        OrdersApiClient.DiningSessionCheckoutContext context;
        try { context = await _orders.GetDiningSessionCheckoutContextAsync(sessionId, request.CustomerId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy phiên phục vụ."); }
        catch (HttpRequestException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = "checkout_context_failed",
            message = "Không lấy được món của phiên để tính tiền. Phiên có thể đã kết thúc hoặc chưa có món." }); }
        var now = DateTime.UtcNow;
        var quote = new CheckoutQuote
        {
            QuoteCode = Code("QUO"), DiningSessionId = sessionId, DiningSessionCode = context.DiningSessionCode,
            CustomerId = request.CustomerId, BranchId = context.BranchId, TableId = context.TableId, Status = "OPEN",
            Subtotal = context.Subtotal, TotalAmount = context.Subtotal, VoucherDiscount = 0, PointsApplied = 0,
            PointsDiscount = 0, IdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = request.RequestHash,
            ItemsJson = JsonSerializer.Serialize(context.Items), ExpiresAtUtc = now.AddMinutes(15), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        // The best running promotion of the branch, then a booking's paid deposit ("Đã đặt cọc −X đ").
        CheckoutPricing.AttachPromotion(quote, await InternalPromotionsController.OffersAsync(_db, now, ct), now);
        CheckoutPricing.Apply(quote);
        ReservationDepositService.AttachToQuote(quote, await ReservationDepositService.PaidForReservationAsync(_db, context.ReservationId, ct));
        _db.CheckoutQuotes.Add(quote);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { code = "command_in_progress", message = "Yêu cầu đang được xử lý." }); }
        return Ok(await QuoteBodyAsync(quote, ct));
    }

    // What the app may offer. Online gateways stay listed for later use, but the app currently shows only
    // cash and bank transfer; transfer needs the receiving account in Payments:BankTransfer.
    [HttpGet("payment-options")]
    public ActionResult<object> PaymentOptions([FromServices] BankTransferQrService transfer) =>
        Ok(new
        {
            onlineMethods = _paymentProvider.IsConfigured ? new[] { "VNPAY" } : Array.Empty<string>(),
            transferAvailable = transfer.IsConfigured,
            // The app shows the points conversion only from these (it never assumes a rate).
            pointValueVnd = SelfRestaurant.Loyalty.LoyaltyRules.PointValueVnd,
            pointsStep = SelfRestaurant.Loyalty.LoyaltyRules.RedeemStep,
            maxPointsDiscountPercent = SelfRestaurant.Loyalty.LoyaltyRules.MaxDiscountPercent,
        });

    // VietQR for an app payment the customer chose to pay by bank transfer; the cashier confirms receipt.
    [HttpGet("payments/{paymentId:int}/transfer-qr")]
    public async Task<ActionResult<object>> TransferQr(int paymentId, [FromQuery] int customerId,
        [FromServices] BankTransferQrService transfer, [FromServices] Microsoft.Extensions.Options.IOptions<SePayOptions> sepay, CancellationToken ct)
    {
        if (customerId <= 0) return Validation();
        var row = await (from p in _db.Payments.AsNoTracking()
                         join q in _db.CheckoutQuotes.AsNoTracking() on p.CheckoutQuoteId equals q.CheckoutQuoteId
                         where p.PaymentId == paymentId
                         select new { Payment = p, q.BranchId }).FirstOrDefaultAsync(ct);
        if (row is null) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch." });
        if (row.Payment.CustomerId != customerId) return ForbidResult();
        if (!IsTransferPayment(row.Payment.Channel, row.Payment.Method))
            return Conflict(new { code = "not_bank_transfer", message = "Giao dịch này không thanh toán bằng chuyển khoản." });
        if (row.Payment.Status is not ("PENDING" or "CREATED"))
            return Conflict(new { code = "payment_not_pending", message = "Giao dịch không còn chờ thanh toán." });
        if (!transfer.IsConfigured)
            return Conflict(new { code = "bank_transfer_unavailable", message = "Nhà hàng chưa cấu hình tài khoản nhận chuyển khoản." });
        return Ok(transfer.Create(row.Payment.Amount, BankTransferQrService.PaymentContent(row.BranchId, row.Payment.PaymentId))
            with { AutoConfirm = sepay.Value.Enabled });
    }

    internal static bool IsTransferPayment(string? channel, string? method) =>
        string.Equals(channel, "ONSITE", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(method, "TRANSFER", StringComparison.OrdinalIgnoreCase) || string.Equals(method, "QR", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Vouchers the guest may see: running ones, plus those that ended or start within 30 days (shown greyed with the
    /// reason). With quoteId, each row says whether it fits THIS bill and how much it takes off; usable ones come first.
    /// </summary>
    [HttpGet("me/vouchers")]
    public async Task<ActionResult<object>> Vouchers([FromQuery] int customerId, [FromQuery] int? quoteId, CancellationToken ct)
    {
        if (customerId <= 0) return Validation();
        CheckoutQuote? quote = null;
        if (quoteId is > 0)
        {
            quote = await _db.CheckoutQuotes.AsNoTracking().FirstOrDefaultAsync(x => x.CheckoutQuoteId == quoteId, ct);
            if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
            if (quote.CustomerId != customerId) return ForbidResult();
        }
        var now = DateTime.UtcNow;
        var from = now.AddDays(-30); var until = now.AddDays(30);
        var rows = await _db.Vouchers.AsNoTracking()
            .Where(x => x.IsActive && x.StartsAtUtc <= until && x.EndsAtUtc >= from).OrderBy(x => x.VoucherId).Take(200).ToListAsync(ct);
        var uses = await VoucherUsage.CountAsync(_db, rows.Select(x => x.VoucherId).ToArray(), customerId, quote?.CheckoutQuoteId, ct);
        var items = rows.Select(x =>
        {
            var use = uses.TryGetValue(x.VoucherId, out var u) ? u : VoucherUse.None;
            return new { Voucher = x, Check = VoucherRules.Check(x, now, quote?.Subtotal, use), Used = use.UsedByCustomer };
        }).OrderByDescending(x => x.Check.Usable).ThenByDescending(x => x.Check.Discount).ThenBy(x => x.Voucher.EndsAtUtc).ToArray();
        return Ok(items.Select(x => new
        {
            x.Voucher.VoucherId, x.Voucher.Code, x.Voucher.Name, x.Voucher.DiscountType, discountValue = Money(x.Voucher.DiscountValue),
            minSubtotal = Money(x.Voucher.MinSubtotal), maxDiscount = x.Voucher.MaxDiscount == null ? (long?)null : Money(x.Voucher.MaxDiscount.Value),
            x.Voucher.StartsAtUtc, x.Voucher.EndsAtUtc, x.Voucher.MaxUsesPerCustomer, x.Voucher.MaxTotalUses,
            usedByMe = x.Used, remainingForMe = x.Check.RemainingForCustomer, remainingTotal = x.Check.RemainingTotal,
            usable = x.Check.Usable, reasonCode = x.Check.Code, reason = x.Check.Reason, remainingLabel = VoucherRules.RemainingLabel(x.Check),
            discount = quote is null ? (long?)null : Money(x.Check.Discount), applied = quote?.VoucherId == x.Voucher.VoucherId,
        }));
    }

    /// <summary>The quote as stored (the app reloads it after Back instead of trusting its own copy).</summary>
    [HttpGet("checkout/quotes/{quoteId:int}")]
    public async Task<ActionResult<object>> GetQuote(int quoteId, [FromQuery] int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return Validation();
        var quote = await _db.CheckoutQuotes.AsNoTracking().FirstOrDefaultAsync(x => x.CheckoutQuoteId == quoteId, ct);
        if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
        return quote.CustomerId == customerId ? Ok(await QuoteBodyAsync(quote, ct)) : ForbidResult();
    }

    /// <summary>Picks a voucher for the bill (VoucherId null = "Bỏ voucher"). The better of voucher and promotion is used.</summary>
    [HttpPost("checkout/quotes/{quoteId:int}/voucher")]
    public async Task<ActionResult<object>> ApplyVoucher(int quoteId, VoucherRequest request, CancellationToken ct)
    {
        var quote = await OwnedOpenQuote(quoteId, request.CustomerId, ct); if (quote.Result is not null) return quote.Result;
        var entity = quote.Value!;
        if (request.VoucherId is null) { entity.VoucherId = null; entity.VoucherOfferDiscount = 0; }
        else
        {
            var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherId == request.VoucherId, ct);
            if (voucher is null) return NotFound(new { code = VoucherRules.NotFound, message = "Voucher không tồn tại." });
            var check = VoucherRules.Check(voucher, DateTime.UtcNow, entity.Subtotal,
                await VoucherUsage.CountAsync(_db, voucher.VoucherId, entity.CustomerId, entity.CheckoutQuoteId, ct));
            if (!check.Usable) return VoucherUnavailable(check);
            entity.VoucherId = voucher.VoucherId;
            // Kept as an offer: CheckoutPricing uses it only when better than the promotion (never both).
            entity.VoucherOfferDiscount = check.Discount;
        }
        Recalculate(entity); await _db.SaveChangesAsync(ct); return Ok(await QuoteBodyAsync(entity, ct));
    }

    /// <summary>"Không dùng khuyến mãi" (Use = false) and back (Use = true). The promotion found for the quote stays known.</summary>
    [HttpPost("checkout/quotes/{quoteId:int}/promotion")]
    public async Task<ActionResult<object>> UsePromotion(int quoteId, PromotionChoiceRequest request, CancellationToken ct)
    {
        var quote = await OwnedOpenQuote(quoteId, request.CustomerId, ct); if (quote.Result is not null) return quote.Result;
        var entity = quote.Value!;
        if (entity.PromotionId is null || entity.PromotionOfferDiscount <= 0)
            return Conflict(new { code = "promotion_not_available", message = "Hoá đơn này không có khuyến mãi tự động." });
        entity.PromotionDeclined = !request.Use;
        Recalculate(entity); await _db.SaveChangesAsync(ct); return Ok(await QuoteBodyAsync(entity, ct));
    }

    [HttpPost("checkout/quotes/{quoteId:int}/loyalty")]
    public async Task<ActionResult<object>> ApplyLoyalty(int quoteId, LoyaltyRequest request, CancellationToken ct)
    {
        var quote = await OwnedOpenQuote(quoteId, request.CustomerId, ct); if (quote.Result is not null) return quote.Result;
        var entity = quote.Value!;
        if (request.Points < 0) return Validation();
        var customer = await _customers.GetCustomerAsync(request.CustomerId, ct);
        if (customer is null) return NotFound(new { code = "customer_not_found", message = "Không tìm thấy khách hàng." });
        if (request.Points > customer.LoyaltyPoints) return Conflict(new { code = "insufficient_points", message = "Điểm khả dụng không đủ." });
        // Shared rules (CheckoutPricing): 1 point = 10đ, whole 1.000-point steps, capped by the balance and by 10% of the
        // bill after the promotion/voucher.
        entity.PointsApplied = request.Points;
        CheckoutPricing.Apply(entity, customer.LoyaltyPoints);
        await _db.SaveChangesAsync(ct); return Ok(await QuoteBodyAsync(entity, ct));
    }

    [HttpPost("dining-sessions/{sessionId:int}/payment-request")]
    public async Task<ActionResult<object>> RequestPayment(int sessionId, PaymentRequestCommand request, CancellationToken ct)
    {
        var quote = await _db.CheckoutQuotes.FirstOrDefaultAsync(x => x.CheckoutQuoteId == request.CheckoutQuoteId, ct);
        if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
        if (quote.CustomerId != request.CustomerId || quote.DiningSessionId != sessionId) return ForbidResult();
        if (quote.ExpiresAtUtc <= DateTime.UtcNow) { quote.Status = "EXPIRED"; await _db.SaveChangesAsync(ct); return Conflict(new { code = "quote_expired", message = "Báo giá đã hết hạn." }); }
        if (quote.Status == "PAYMENT_REQUESTED") return Ok(await QuoteBodyAsync(quote, ct));
        if (quote.Status != "OPEN") return StateConflict();
        var blocking = await BlockingPaymentAsync(sessionId, quote.CheckoutQuoteId, ct);
        if (blocking is not null) return PaymentInProgress(blocking);
        // A picked voucher takes one of its uses here. One guest at a time per voucher (lock held until commit), so two
        // guests racing for the last use cannot both get it; the other one is told and can remove the voucher.
        await using var voucherTx = quote.VoucherId is not null && quote.VoucherDiscount > 0
            ? await _db.Database.BeginTransactionAsync(ct) : null;
        if (voucherTx is not null)
        {
            await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(_db, VoucherUsage.LockName(quote.VoucherId!.Value), ct);
            var check = await VoucherUsage.BlockingCheckAsync(_db, quote, DateTime.UtcNow, ct);
            if (check is not null) return VoucherUnavailable(check, " Vui lòng bỏ voucher rồi thanh toán lại.");
        }
        try { await _orders.RequestPaymentAsync(sessionId, request.CustomerId, quote.CheckoutQuoteId, ct); }
        catch (HttpRequestException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = "payment_lock_failed",
            message = "Phiên đang chờ thanh toán ở một yêu cầu khác hoặc đã kết thúc. Vui lòng mở lại màn thanh toán." }); }
        quote.Status = "PAYMENT_REQUESTED"; quote.UpdatedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(ct);
        if (voucherTx is not null) await voucherTx.CommitAsync(ct);
        return Ok(await QuoteBodyAsync(quote, ct));
    }

    [HttpPost("dining-sessions/{sessionId:int}/payment-request/cancel")]
    public async Task<ActionResult<object>> CancelPaymentRequest(int sessionId, CustomerRequest request, CancellationToken ct)
    {
        // The ONLY way to unlock a session: a payment waiting or done anywhere in the session keeps it locked, since
        // money for it may still arrive (incident 25/10: the app unlocked through Orders while TT28 was pending).
        var blocking = await BlockingPaymentAsync(sessionId, null, ct);
        if (blocking is not null) return PaymentInProgress(blocking);
        var quotes = await _db.CheckoutQuotes.Where(x => x.DiningSessionId == sessionId && x.CustomerId == request.CustomerId && x.Status == "PAYMENT_REQUESTED").ToListAsync(ct);
        try { await _orders.CancelPaymentRequestAsync(sessionId, request.CustomerId, ct); }
        catch (HttpRequestException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = "cancel_payment_request_failed",
            message = "Không hủy được yêu cầu thanh toán. Vui lòng thử lại." }); }
        foreach (var quote in quotes) { quote.Status = "CANCELLED"; quote.UpdatedAtUtc = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return Ok(new { sessionId, cancelledQuotes = quotes.Count });
    }

    [HttpPost("checkout/quotes/{quoteId:int}/payments")]
    public async Task<ActionResult<object>> CreatePayment(int quoteId, CreatePaymentRequest request, CancellationToken ct)
    {
        if (!ValidCommand(request.CustomerId, request.IdempotencyKey, request.RequestHash)) return Validation();
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, quoteId, ct);
        var replay = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapPayment(replay)) : IdempotencyConflict();
        var quote = await _db.CheckoutQuotes.FirstOrDefaultAsync(x => x.CheckoutQuoteId == quoteId, ct);
        if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
        if (quote.CustomerId != request.CustomerId) return ForbidResult();
        if (quote.Status != "PAYMENT_REQUESTED") return Conflict(new { code = "payment_request_required", message = "Hãy yêu cầu thanh toán trước." });
        if (await _db.SplitBills.AnyAsync(x => x.CheckoutQuoteId == quoteId, ct)
            || await _db.Payments.AnyAsync(x => x.CheckoutQuoteId == quoteId && (x.Status == "PENDING" || x.Status == "SUCCEEDED"), ct))
            return Conflict(new { code = "payment_in_progress", message = "Báo giá đã có giao dịch hoặc chia hóa đơn." });
        var blocking = await BlockingPaymentAsync(quote.DiningSessionId, quoteId, ct);
        if (blocking is not null) return PaymentInProgress(blocking);
        var validation = ValidatePaymentMethod(request.Channel, request.Method, request.Location); if (validation is not null) return validation;
        var payment = NewPayment(quote, request.Channel, request.Method, request.Location, request.IdempotencyKey, request.RequestHash, 1, quote.TotalAmount);
        var providerError = await InitializeProviderAsync(payment, ct); if (providerError is not null) return providerError;
        _db.Payments.Add(payment); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(MapPayment(payment));
    }

    /// <summary>
    /// The payment the app must return to: the session's latest waiting (PENDING/PROCESSING) payment, with its quote.
    /// 404 when nothing is waiting. Used after Back / reopen so the guest never starts a second payment.
    /// </summary>
    [HttpGet("dining-sessions/{sessionId:int}/active-payment")]
    public async Task<ActionResult<object>> ActivePayment(int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var payment = await _db.Payments.AsNoTracking()
            .Where(x => x.DiningSessionId == sessionId && (x.Status == "PENDING" || x.Status == "PROCESSING"))
            .OrderByDescending(x => x.PaymentId).FirstOrDefaultAsync(ct);
        if (payment is null) return NotFound(new { code = "no_active_payment", message = "Phiên không có giao dịch đang chờ." });
        if (payment.CustomerId != customerId) return ForbidResult();
        var quote = await _db.CheckoutQuotes.AsNoTracking().FirstAsync(x => x.CheckoutQuoteId == payment.CheckoutQuoteId, ct);
        return Ok(new { payment = MapPayment(payment), quote = await QuoteBodyAsync(quote, ct) });
    }

    /// <summary>A payment waiting or done in the session (outside exceptQuoteId) that forbids new quotes/requests/payments.</summary>
    private Task<Payment?> BlockingPaymentAsync(int sessionId, int? exceptQuoteId, CancellationToken ct) =>
        _db.Payments.AsNoTracking()
            .Where(x => x.DiningSessionId == sessionId && (exceptQuoteId == null || x.CheckoutQuoteId != exceptQuoteId)
                && (x.Status == "PENDING" || x.Status == "PROCESSING" || x.Status == "SUCCEEDED"))
            .OrderByDescending(x => x.PaymentId).FirstOrDefaultAsync(ct);

    private ObjectResult PaymentInProgress(Payment payment) => Conflict(payment.Status == "SUCCEEDED"
        ? new { code = "session_already_paid", message = "Phiên này đã được thanh toán.", paymentId = payment.PaymentId }
        : new { code = "payment_in_progress", message = "Bạn đang có một giao dịch chờ xác nhận. Vui lòng chờ hoặc liên hệ thu ngân.", paymentId = payment.PaymentId });

    [HttpGet("payments/{paymentId:int}")]
    public async Task<ActionResult<object>> GetPayment(int paymentId, [FromQuery] int customerId, CancellationToken ct)
    {
        var quoteId = await _db.Payments.Where(x => x.PaymentId == paymentId).Select(x => (int?)x.CheckoutQuoteId).FirstOrDefaultAsync(ct);
        if (quoteId is null) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch." });
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, quoteId.Value, ct);
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.PaymentId == paymentId, ct);
        if (payment is null) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch." });
        if (payment.CustomerId != customerId) return ForbidResult();
        if (payment.Status == "PENDING" && payment.ProviderExpiresAtUtc <= DateTime.UtcNow)
        {
            payment.Status = "EXPIRED"; payment.FailureCode = "provider_session_expired"; payment.UpdatedAtUtc = DateTime.UtcNow; payment.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(MapPayment(payment));
    }

    [HttpPost("payments/{paymentId:int}/retry")]
    public async Task<ActionResult<object>> Retry(int paymentId, RetryPaymentRequest request, CancellationToken ct)
    {
        if (!ValidCommand(request.CustomerId, request.IdempotencyKey, request.RequestHash)) return Validation();
        var quoteId = await _db.Payments.Where(x => x.PaymentId == paymentId).Select(x => (int?)x.CheckoutQuoteId).FirstOrDefaultAsync(ct);
        if (quoteId is null) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch." });
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, quoteId.Value, ct);
        var replay = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapPayment(replay)) : IdempotencyConflict();
        var old = await _db.Payments.FirstOrDefaultAsync(x => x.PaymentId == paymentId, ct);
        if (old is null) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch." });
        if (old.CustomerId != request.CustomerId) return ForbidResult();
        if (old.Status is not ("FAILED" or "CANCELLED" or "EXPIRED")) return Conflict(new { code = "payment_not_retryable", message = "Chỉ giao dịch thất bại, hủy hoặc hết hạn mới được thử lại." });
        var quote = await _db.CheckoutQuotes.FirstAsync(x => x.CheckoutQuoteId == old.CheckoutQuoteId, ct);
        if (quote.Status != "PAYMENT_REQUESTED") return StateConflict();
        var splitPart = await _db.SplitBillParts.FirstOrDefaultAsync(x => x.PaymentId == old.PaymentId, ct);
        if (splitPart is null && await _db.SplitBills.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct))
            return Conflict(new { code = "payment_attempt_replaced", message = "Giao dịch đã được thay thế." });
        if (splitPart is null && await _db.Payments.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId && (x.Status == "PENDING" || x.Status == "SUCCEEDED"), ct))
            return Conflict(new { code = "payment_in_progress", message = "Đã có giao dịch đang thanh toán." });
        var blocking = await BlockingPaymentAsync(quote.DiningSessionId, quote.CheckoutQuoteId, ct);
        if (blocking is not null) return PaymentInProgress(blocking);
        var payment = NewPayment(quote, old.Channel, old.Method, old.Location, request.IdempotencyKey, request.RequestHash, old.AttemptNumber + 1, old.Amount);
        var providerError = await InitializeProviderAsync(payment, ct); if (providerError is not null) return providerError;
        _db.Payments.Add(payment); await _db.SaveChangesAsync(ct);
        if (splitPart is not null)
        {
            splitPart.PaymentId = payment.PaymentId;
            splitPart.Status = "PENDING";
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(MapPayment(payment));
    }

    [HttpGet("~/api/payments/providers/vnpay/ipn")]
    public async Task<ActionResult<object>> VnPayIpn(CancellationToken ct)
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
        var webhook = _paymentProvider.VerifyWebhook(values);
        if (!webhook.SignatureValid)
        {
            _logger.LogWarning("Rejected {Provider} webhook because signature validation failed", _paymentProvider.Name);
            return Ok(new { RspCode = "97", Message = "Invalid signature" });
        }
        if (string.IsNullOrWhiteSpace(webhook.ProviderReference)) return Ok(new { RspCode = "01", Message = "Order not found" });
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.ProviderReference == webhook.ProviderReference, ct);
        if (payment is null) return Ok(new { RspCode = "01", Message = "Order not found" });
        if (webhook.Amount != payment.Amount || !string.Equals(webhook.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected {Provider} webhook for payment {PaymentId}: amount or currency mismatch", _paymentProvider.Name, payment.PaymentId);
            return Ok(new { RspCode = "04", Message = "Invalid amount" });
        }
        if (payment.Status == "SUCCEEDED" || payment.Status is "FAILED" or "CANCELLED" or "EXPIRED")
            return Ok(new { RspCode = "02", Message = "Order already confirmed" });
        var status = webhook.PaymentStatus!;
        if (status == "SUCCEEDED")
        {
            try { await _finalization.ConfirmAsync(payment.PaymentId, null, null, webhook.ProviderTransactionId, ct); }
            catch (PaymentDomainException) { return Ok(new { RspCode = "02", Message = "Order already confirmed or replaced" }); }
            return Ok(new { RspCode = "00", Message = "Confirm success" });
        }
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, payment.CheckoutQuoteId, ct);
        payment = await _db.Payments.SingleAsync(x => x.PaymentId == payment.PaymentId, ct);
        if (payment.Status != "PENDING") return Ok(new { RspCode = "02", Message = "Order already confirmed" });
        payment.Status = status; payment.ProviderTransactionId = webhook.ProviderTransactionId; payment.FailureCode = webhook.FailureCode;
        payment.UpdatedAtUtc = DateTime.UtcNow; payment.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _logger.LogInformation("Processed {Provider} webhook for payment {PaymentId}, provider transaction {ProviderTransactionId}, new state {PaymentStatus}",
            _paymentProvider.Name, payment.PaymentId, webhook.ProviderTransactionId, status);
        return Ok(new { RspCode = "00", Message = "Confirm success" });
    }

    [HttpGet("~/api/payments/providers/vnpay/return")]
    public ContentResult VnPayReturn()
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
        var result = _paymentProvider.VerifyWebhook(values);
        var message = result.SignatureValid ? "VietTable đã nhận kết quả trả về. Vui lòng quay lại ứng dụng để kiểm tra trạng thái thanh toán."
            : "Kết quả trả về không hợp lệ. Giao dịch chưa được xác nhận.";
        return Content($"<!doctype html><html><head><meta charset=\"utf-8\"><title>VietTable</title></head><body><h2>{message}</h2></body></html>", "text/html; charset=utf-8");
    }

    [HttpGet("dining-sessions/{sessionId:int}/invoice")]
    public async Task<ActionResult<object>> InvoiceForSession(int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (invoice is null) return NotFound(new { code = "invoice_not_found", message = "Hóa đơn chưa được phát hành." });
        if (invoice.CustomerId != customerId) return ForbidResult();
        return Ok(await MapCustomerInvoiceAsync(invoice, ct));
    }

    [HttpGet("invoices/{invoiceId:int}")]
    public async Task<ActionResult<object>> InvoiceById(int invoiceId, [FromQuery] int customerId, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
        if (invoice is null) return NotFound(new { code = "invoice_not_found", message = "Không tìm thấy hóa đơn." });
        if (invoice.CustomerId != customerId) return ForbidResult();
        return Ok(await MapCustomerInvoiceAsync(invoice, ct));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<object>> CustomerInvoices([FromQuery] int customerId,
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (customerId <= 0 || limit is < 1 or > 100)
            return BadRequest(new { code = "validation_failed", message = "Thông tin lịch sử hóa đơn không hợp lệ." });
        // One Billing-owned read rather than three sequential database round trips.
        // Apply ownership/page first; left joins preserve legacy missing metadata.
        var page = _db.Invoices.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IssuedAtUtc).ThenByDescending(x => x.InvoiceId).Take(limit);
        var invoices = await (
            from invoice in page
            join quote in _db.CheckoutQuotes.AsNoTracking() on invoice.CheckoutQuoteId equals quote.CheckoutQuoteId into quotes
            from quote in quotes.DefaultIfEmpty()
            join payment in _db.Payments.AsNoTracking() on invoice.PaymentId equals payment.PaymentId into payments
            from payment in payments.DefaultIfEmpty()
            orderby invoice.IssuedAtUtc descending, invoice.InvoiceId descending
            select new { invoice, quote, payment }).ToArrayAsync(ct);
        var items = invoices.Select(row => MapCustomerInvoice(row.invoice, row.quote, row.payment)).ToArray();
        return Ok(new { items, nextCursor = (string?)null });
    }

    [HttpPost("dining-sessions/{sessionId:int}/split-bill")]
    public async Task<ActionResult<object>> CreateSplitBill(int sessionId, CreateSplitBillRequest request, CancellationToken ct)
    {
        if (!ValidCommand(request.CustomerId, request.IdempotencyKey, request.RequestHash) || request.Amounts is null || request.Amounts.Count < 2 || request.Amounts.Any(x => x <= 0)) return Validation();
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, request.CheckoutQuoteId, ct);
        var replay = await _db.SplitBills.Include(x => x.Parts).FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(await MapSplitAsync(replay, ct)) : IdempotencyConflict();
        var quote = await _db.CheckoutQuotes.FirstOrDefaultAsync(x => x.CheckoutQuoteId == request.CheckoutQuoteId && x.DiningSessionId == sessionId, ct);
        if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
        if (quote.CustomerId != request.CustomerId) return ForbidResult();
        if (quote.Status != "PAYMENT_REQUESTED") return StateConflict();
        if (await _db.Payments.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId && (x.Status == "PENDING" || x.Status == "SUCCEEDED"), ct))
            return Conflict(new { code = "payment_in_progress", message = "Báo giá đã có giao dịch đang thanh toán." });
        if (await _db.SplitBills.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct))
            return Conflict(new { code = "split_bill_exists", message = "Báo giá này đã được chia hóa đơn." });
        if (request.Amounts.Sum() != quote.TotalAmount) return Conflict(new { code = "split_total_mismatch", message = "Tổng các phần phải bằng tổng tiền báo giá." });
        var now = DateTime.UtcNow;
        var split = new SplitBill { CheckoutQuoteId = quote.CheckoutQuoteId, DiningSessionId = sessionId, CustomerId = request.CustomerId,
            TotalAmount = quote.TotalAmount, Status = "OPEN", IdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = request.RequestHash, CreatedAtUtc = now, UpdatedAtUtc = now };
        var index = 0; foreach (var amount in request.Amounts) split.Parts.Add(new SplitBillPart { Label = $"Phần {++index}", Amount = amount, Status = "UNPAID" });
        _db.SplitBills.Add(split); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(await MapSplitAsync(split, ct));
    }

    [HttpGet("dining-sessions/{sessionId:int}/split-bill")]
    public async Task<ActionResult<object>> GetSplitBillForSession(int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var split = await _db.SplitBills.AsNoTracking().Include(x => x.Parts)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (split is null) return NotFound(new { code = "split_bill_not_found", message = "Phiên này chưa có cấu hình chia hóa đơn." });
        return split.CustomerId == customerId ? Ok(await MapSplitAsync(split, ct)) : ForbidResult();
    }

    [HttpGet("split-bills/{splitBillId:int}")]
    public async Task<ActionResult<object>> GetSplitBill(int splitBillId, [FromQuery] int customerId, CancellationToken ct)
    {
        var split = await _db.SplitBills.AsNoTracking().Include(x => x.Parts)
            .FirstOrDefaultAsync(x => x.SplitBillId == splitBillId, ct);
        if (split is null) return NotFound(new { code = "split_bill_not_found", message = "Không tìm thấy yêu cầu chia hóa đơn." });
        return split.CustomerId == customerId ? Ok(await MapSplitAsync(split, ct)) : ForbidResult();
    }

    [HttpPost("split-bills/{splitBillId:int}/payments")]
    public async Task<ActionResult<object>> PaySplitPart(int splitBillId, SplitPaymentRequest request, CancellationToken ct)
    {
        if (!ValidCommand(request.CustomerId, request.IdempotencyKey, request.RequestHash)) return Validation();
        var quoteId = await _db.SplitBills.Where(x => x.SplitBillId == splitBillId).Select(x => (int?)x.CheckoutQuoteId).FirstOrDefaultAsync(ct);
        if (quoteId is null) return NotFound(new { code = "split_bill_not_found", message = "Không tìm thấy yêu cầu chia hóa đơn." });
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await PaymentFinalizationService.LockQuoteAsync(_db, quoteId.Value, ct);
        var split = await _db.SplitBills.Include(x => x.Parts).FirstOrDefaultAsync(x => x.SplitBillId == splitBillId, ct);
        if (split is null) return NotFound(new { code = "split_bill_not_found", message = "Không tìm thấy yêu cầu chia hóa đơn." });
        if (split.CustomerId != request.CustomerId) return ForbidResult();
        var replay = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.CustomerId == request.CustomerId && replay.RequestHash == request.RequestHash
            ? Ok(new { payment = MapPayment(replay), splitBill = await MapSplitAsync(split, ct) }) : IdempotencyConflict();
        var part = split.Parts.FirstOrDefault(x => x.SplitBillPartId == request.SplitBillPartId);
        if (part is null) return NotFound(new { code = "split_part_not_found", message = "Không tìm thấy phần thanh toán." });
        if (part.Status != "UNPAID") return Conflict(new { code = "split_part_already_paid", message = "Phần này đã có giao dịch." });
        var validation = ValidatePaymentMethod(request.Channel, request.Method, request.Location); if (validation is not null) return validation;
        var quote = await _db.CheckoutQuotes.FirstAsync(x => x.CheckoutQuoteId == split.CheckoutQuoteId, ct);
        if (quote.Status != "PAYMENT_REQUESTED") return StateConflict();
        var payment = NewPayment(quote, request.Channel, request.Method, request.Location, request.IdempotencyKey, request.RequestHash, 1, part.Amount);
        var providerError = await InitializeProviderAsync(payment, ct); if (providerError is not null) return providerError;
        _db.Payments.Add(payment); await _db.SaveChangesAsync(ct); part.PaymentId = payment.PaymentId; part.Status = "PENDING"; await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { payment = MapPayment(payment), splitBill = await MapSplitAsync(split, ct) });
    }

    private async Task<(CheckoutQuote? Value, ActionResult? Result)> OwnedOpenQuote(int id, int customerId, CancellationToken ct)
    {
        var quote = await _db.CheckoutQuotes.FirstOrDefaultAsync(x => x.CheckoutQuoteId == id, ct);
        if (quote is null) return (null, NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." }));
        if (quote.CustomerId != customerId) return (null, ForbidResult());
        // Voucher/points can change only before the payment request locks the quote (CLAUDE.md 6b).
        if (quote.Status != "OPEN") return (null, QuoteLocked());
        if (quote.ExpiresAtUtc <= DateTime.UtcNow) { quote.Status = "EXPIRED"; await _db.SaveChangesAsync(ct); return (null, Conflict(new { code = "quote_expired", message = "Báo giá đã hết hạn." })); }
        return (quote, null);
    }

    internal static Payment NewPayment(CheckoutQuote q, string channel, string method, string? location, string key, string hash, int attempt, decimal amount)
    { var now = DateTime.UtcNow; return new Payment { PaymentCode = Code("PAY"), CheckoutQuoteId = q.CheckoutQuoteId, DiningSessionId = q.DiningSessionId,
        CustomerId = q.CustomerId, Amount = amount, Channel = channel.Trim().ToUpperInvariant(), Method = method.Trim().ToUpperInvariant(),
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim().ToUpperInvariant(), Status = "PENDING", AttemptNumber = attempt,
        IdempotencyKey = key.Trim(), RequestHash = hash, CreatedAtUtc = now, UpdatedAtUtc = now }; }
    private async Task<ObjectResult?> InitializeProviderAsync(Payment payment, CancellationToken ct)
    {
        if (payment.Channel != "ONLINE") return null;
        try
        {
            var session = await _paymentProvider.CreateAsync(payment, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", ct);
            payment.ProviderName = session.ProviderName; payment.ProviderReference = session.ProviderReference;
            payment.ProviderPaymentUrl = session.PaymentUrl; payment.Currency = session.Currency; payment.ProviderExpiresAtUtc = session.ExpiresAtUtc;
            _logger.LogInformation("Created {Provider} session for internal payment code {PaymentCode}, reference {ProviderReference}",
                session.ProviderName, payment.PaymentCode, session.ProviderReference);
            return null;
        }
        catch (PaymentProviderUnavailableException ex)
        {
            _logger.LogWarning("Online payment provider is unavailable: {Reason}", ex.Message);
            return StatusCode(503, new { code = "payment_provider_unavailable", message = ex.Message });
        }
    }
    private static ObjectResult? ValidatePaymentMethod(string? channelValue, string? methodValue, string? locationValue)
    {
        var channel = channelValue?.Trim().ToUpperInvariant(); var method = methodValue?.Trim().ToUpperInvariant(); var location = locationValue?.Trim().ToUpperInvariant();
        if (channel == "ONLINE" && method == "QR" && location is null) return null;
        if (channel == "ONSITE" && method is "CASH" or "QR" or "TRANSFER" && location is "TABLE" or "CASHIER") return null;
        return new BadRequestObjectResult(new { code = "invalid_payment_method", message = "ONLINE chỉ hỗ trợ QR không kèm location; ONSITE cần TABLE/CASHIER và CASH/QR/TRANSFER." });
    }
    private static void Recalculate(CheckoutQuote q) => ReservationDepositService.Recalculate(q);
    private async Task<object> QuoteBodyAsync(CheckoutQuote q, CancellationToken ct) => MapQuote(q,
        q.VoucherId is null ? null : await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherId == q.VoucherId, ct));
    /// <summary>
    /// promotionId/Name/OfferDiscount = the automatic promotion found for the bill (also when declined);
    /// promotionDiscount = what it takes off now. voucher* = the voucher the guest picked (voucherDiscount 0 when the
    /// promotion is better).
    /// </summary>
    internal static object MapQuote(CheckoutQuote q, Voucher? voucher) => new { q.CheckoutQuoteId, q.QuoteCode, q.DiningSessionId, q.Status,
        subtotal = Money(q.Subtotal), q.VoucherId, voucherCode = voucher?.Code, voucherName = voucher?.Name,
        voucherOfferDiscount = Money(q.VoucherOfferDiscount), voucherDiscount = Money(q.VoucherDiscount), q.PointsApplied,
        pointsDiscount = Money(q.PointsDiscount), depositApplied = Money(q.DepositApplied), q.PromotionId, q.PromotionName,
        promotionOfferDiscount = Money(q.PromotionOfferDiscount), q.PromotionDeclined, promotionDiscount = Money(q.PromotionDiscount),
        q.DiscountNote, totalAmount = Money(q.TotalAmount), q.ExpiresAtUtc, items = JsonSerializer.Deserialize<JsonElement>(q.ItemsJson) };
    private static object MapPayment(Payment p) => new { p.PaymentId, p.PaymentCode, p.CheckoutQuoteId, p.DiningSessionId, amount = Money(p.Amount), p.Currency, p.Channel, p.Method, p.Location, p.Status, p.AttemptNumber, p.ProviderName, p.ProviderReference, p.ProviderTransactionId, paymentUrl = p.ProviderPaymentUrl, providerExpiresAtUtc = p.ProviderExpiresAtUtc, p.FailureCode, p.CreatedAtUtc, p.UpdatedAtUtc, p.CompletedAtUtc };
    private static object MapInvoice(Invoice x) => new { x.InvoiceId, x.InvoiceCode, x.CheckoutQuoteId, x.PaymentId, x.DiningSessionId, subtotal = Money(x.Subtotal), discount = Money(x.Discount), totalAmount = Money(x.TotalAmount), items = JsonSerializer.Deserialize<JsonElement>(x.ItemsJson), x.IssuedAtUtc };
    private async Task<object> MapCustomerInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        var quote = await _db.CheckoutQuotes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CheckoutQuoteId == invoice.CheckoutQuoteId, ct);
        var payment = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentId == invoice.PaymentId, ct);
        return MapCustomerInvoice(invoice, quote, payment);
    }
    private static object MapCustomerInvoice(Invoice invoice, CheckoutQuote? quote, Payment? payment) => new
    {
        invoice.InvoiceId, invoice.InvoiceCode, invoice.CheckoutQuoteId, invoice.PaymentId, invoice.DiningSessionId,
        subtotal = Money(invoice.Subtotal), discount = Money(invoice.Discount), depositApplied = Money(invoice.DepositApplied),
        promotionDiscount = Money(invoice.PromotionDiscount), promotionName = quote?.PromotionName,
        totalAmount = Money(invoice.TotalAmount),
        items = JsonSerializer.Deserialize<JsonElement>(invoice.ItemsJson), invoice.IssuedAtUtc,
        branchId = quote?.BranchId, tableId = quote?.TableId, diningSessionCode = quote?.DiningSessionCode,
        paymentCode = payment?.PaymentCode, paymentChannel = payment?.Channel, paymentMethod = payment?.Method,
        paymentLocation = payment?.Location, paymentStatus = payment?.Status, paymentCompletedAtUtc = payment?.CompletedAtUtc
    };
    private async Task<object> MapSplitAsync(SplitBill x, CancellationToken ct)
    {
        var paymentIds = x.Parts.Where(p => p.PaymentId.HasValue).Select(p => p.PaymentId!.Value).ToArray();
        var paymentStatuses = paymentIds.Length == 0
            ? new Dictionary<int, string>()
            : await _db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.PaymentId))
                .ToDictionaryAsync(p => p.PaymentId, p => p.Status, ct);
        return new
        {
            x.SplitBillId,
            x.CheckoutQuoteId,
            x.DiningSessionId,
            totalAmount = Money(x.TotalAmount),
            x.Status,
            parts = x.Parts.OrderBy(p => p.SplitBillPartId).Select(p => new
            {
                p.SplitBillPartId,
                p.Label,
                amount = Money(p.Amount),
                p.Status,
                p.PaymentId,
                paymentStatus = p.PaymentId.HasValue && paymentStatuses.TryGetValue(p.PaymentId.Value, out var value) ? value : null
            })
        };
    }
    private static long Money(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
    internal static string Code(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(40, prefix.Length + 1 + 14 + 1 + 32)].ToUpperInvariant();
    private static bool ValidCommand(int customerId, string? key, string? hash) => customerId > 0 && !string.IsNullOrWhiteSpace(key) && key.Length <= 140 && !string.IsNullOrWhiteSpace(hash) && hash.Length == 64;
    private ActionResult Validation() => BadRequest(new { code = "validation_failed", message = "Dữ liệu không hợp lệ." });
    private ActionResult IdempotencyConflict() => Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
    private ActionResult StateConflict() => Conflict(new { code = "state_conflict", message = "Trạng thái hiện tại không cho phép thao tác này." });
    internal const string QuoteLockedCode = "quote_locked";
    private ActionResult QuoteLocked() => Conflict(new { code = QuoteLockedCode, message = "Đã yêu cầu thanh toán, không đổi được voucher/điểm." });
    private ObjectResult VoucherUnavailable(VoucherCheck check, string suffix = "") => Conflict(new
    {
        code = check.Code ?? VoucherRules.NotFound,
        message = check.Code switch
        {
            VoucherRules.UsedUp => "Bạn đã dùng hết lượt của voucher này.",
            VoucherRules.SoldOut => "Voucher đã hết lượt sử dụng.",
            _ => (check.Reason ?? "Voucher không dùng được") + ".",
        } + suffix,
    });
    private ActionResult ForbidResult() => StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu dữ liệu này." });

    public sealed record QuoteRequest(int CustomerId, string IdempotencyKey, string RequestHash);
    public sealed record VoucherRequest(int CustomerId, int? VoucherId);
    public sealed record PromotionChoiceRequest(int CustomerId, bool Use);
    public sealed record LoyaltyRequest(int CustomerId, int Points);
    public sealed record PaymentRequestCommand(int CustomerId, int CheckoutQuoteId);
    public sealed record CustomerRequest(int CustomerId);
    public sealed record CreatePaymentRequest(int CustomerId, string Channel, string Method, string? Location, string IdempotencyKey, string RequestHash);
    public sealed record RetryPaymentRequest(int CustomerId, string IdempotencyKey, string RequestHash);
    public sealed record CreateSplitBillRequest(int CustomerId, int CheckoutQuoteId, IReadOnlyList<decimal>? Amounts, string IdempotencyKey, string RequestHash);
    public sealed record SplitPaymentRequest(int CustomerId, int SplitBillPartId, string Channel, string Method, string? Location, string IdempotencyKey, string RequestHash);
}
