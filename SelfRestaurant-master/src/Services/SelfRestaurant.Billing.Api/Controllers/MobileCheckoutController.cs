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
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapQuote(replay)) : IdempotencyConflict();
        OrdersApiClient.DiningSessionCheckoutContext context;
        try { context = await _orders.GetDiningSessionCheckoutContextAsync(sessionId, request.CustomerId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy phiên phục vụ."); }
        catch (HttpRequestException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = "checkout_context_failed", message = ex.Message }); }
        var now = DateTime.UtcNow;
        var quote = new CheckoutQuote
        {
            QuoteCode = Code("QUO"), DiningSessionId = sessionId, DiningSessionCode = context.DiningSessionCode,
            CustomerId = request.CustomerId, BranchId = context.BranchId, TableId = context.TableId, Status = "OPEN",
            Subtotal = context.Subtotal, TotalAmount = context.Subtotal, VoucherDiscount = 0, PointsApplied = 0,
            PointsDiscount = 0, IdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = request.RequestHash,
            ItemsJson = JsonSerializer.Serialize(context.Items), ExpiresAtUtc = now.AddMinutes(15), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _db.CheckoutQuotes.Add(quote);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { code = "command_in_progress", message = "Yêu cầu đang được xử lý." }); }
        return Ok(MapQuote(quote));
    }

    [HttpGet("me/vouchers")]
    public async Task<ActionResult<object>> Vouchers([FromQuery] int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return Validation();
        var now = DateTime.UtcNow;
        var rows = await _db.Vouchers.AsNoTracking().Where(x => x.IsActive && x.StartsAtUtc <= now && x.EndsAtUtc >= now).OrderBy(x => x.VoucherId).ToListAsync(ct);
        return Ok(rows.Select(x => new { x.VoucherId, x.Code, x.Name, x.DiscountType, discountValue = Money(x.DiscountValue), minSubtotal = Money(x.MinSubtotal), maxDiscount = x.MaxDiscount == null ? (long?)null : Money(x.MaxDiscount.Value), x.EndsAtUtc }));
    }

    [HttpPost("checkout/quotes/{quoteId:int}/voucher")]
    public async Task<ActionResult<object>> ApplyVoucher(int quoteId, VoucherRequest request, CancellationToken ct)
    {
        var quote = await OwnedOpenQuote(quoteId, request.CustomerId, ct); if (quote.Result is not null) return quote.Result;
        var entity = quote.Value!;
        if (request.VoucherId is null) { entity.VoucherId = null; entity.VoucherDiscount = 0; }
        else
        {
            var now = DateTime.UtcNow;
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(x => x.VoucherId == request.VoucherId && x.IsActive && x.StartsAtUtc <= now && x.EndsAtUtc >= now, ct);
            if (voucher is null) return NotFound(new { code = "voucher_not_found", message = "Voucher không tồn tại hoặc đã hết hạn." });
            if (entity.Subtotal < voucher.MinSubtotal) return Conflict(new { code = "voucher_minimum_not_met", message = "Đơn chưa đạt giá trị tối thiểu của voucher." });
            entity.VoucherId = voucher.VoucherId;
            entity.VoucherDiscount = voucher.DiscountType == "PERCENT" ? entity.Subtotal * voucher.DiscountValue / 100 : voucher.DiscountValue;
            if (voucher.MaxDiscount is not null) entity.VoucherDiscount = Math.Min(entity.VoucherDiscount, voucher.MaxDiscount.Value);
            entity.VoucherDiscount = Math.Min(entity.Subtotal, Math.Floor(entity.VoucherDiscount));
        }
        Recalculate(entity); await _db.SaveChangesAsync(ct); return Ok(MapQuote(entity));
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
        var payableAfterVoucher = Math.Max(0, entity.Subtotal - entity.VoucherDiscount);
        entity.PointsApplied = Math.Min(request.Points, (int)Math.Floor(payableAfterVoucher / 1000m));
        entity.PointsDiscount = entity.PointsApplied * 1000m;
        Recalculate(entity); await _db.SaveChangesAsync(ct); return Ok(MapQuote(entity));
    }

    [HttpPost("dining-sessions/{sessionId:int}/payment-request")]
    public async Task<ActionResult<object>> RequestPayment(int sessionId, PaymentRequestCommand request, CancellationToken ct)
    {
        var quote = await _db.CheckoutQuotes.FirstOrDefaultAsync(x => x.CheckoutQuoteId == request.CheckoutQuoteId, ct);
        if (quote is null) return NotFound(new { code = "quote_not_found", message = "Không tìm thấy báo giá." });
        if (quote.CustomerId != request.CustomerId || quote.DiningSessionId != sessionId) return ForbidResult();
        if (quote.ExpiresAtUtc <= DateTime.UtcNow) { quote.Status = "EXPIRED"; await _db.SaveChangesAsync(ct); return Conflict(new { code = "quote_expired", message = "Báo giá đã hết hạn." }); }
        if (quote.Status == "PAYMENT_REQUESTED") return Ok(MapQuote(quote));
        if (quote.Status != "OPEN") return StateConflict();
        try { await _orders.RequestPaymentAsync(sessionId, request.CustomerId, quote.CheckoutQuoteId, ct); }
        catch (HttpRequestException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = "payment_lock_failed", message = ex.Message }); }
        quote.Status = "PAYMENT_REQUESTED"; quote.UpdatedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(ct);
        return Ok(MapQuote(quote));
    }

    [HttpPost("dining-sessions/{sessionId:int}/payment-request/cancel")]
    public async Task<ActionResult<object>> CancelPaymentRequest(int sessionId, CustomerRequest request, CancellationToken ct)
    {
        var quote = await _db.CheckoutQuotes.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(x => x.DiningSessionId == sessionId && x.CustomerId == request.CustomerId && x.Status == "PAYMENT_REQUESTED", ct);
        if (quote is null) return NotFound(new { code = "payment_request_not_found", message = "Không có yêu cầu thanh toán đang hoạt động." });
        if (await _db.Payments.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId && (x.Status == "PENDING" || x.Status == "PROCESSING" || x.Status == "SUCCEEDED"), ct))
            return Conflict(new { code = "payment_in_progress", message = "Không thể hủy khi giao dịch đang xử lý." });
        await _orders.CancelPaymentRequestAsync(sessionId, request.CustomerId, ct);
        quote.Status = "CANCELLED"; quote.UpdatedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(ct); return Ok(MapQuote(quote));
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
        var validation = ValidatePaymentMethod(request.Channel, request.Method, request.Location); if (validation is not null) return validation;
        var payment = NewPayment(quote, request.Channel, request.Method, request.Location, request.IdempotencyKey, request.RequestHash, 1, quote.TotalAmount);
        var providerError = await InitializeProviderAsync(payment, ct); if (providerError is not null) return providerError;
        _db.Payments.Add(payment); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(MapPayment(payment));
    }

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
        var invoices = await _db.Invoices.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IssuedAtUtc).ThenByDescending(x => x.InvoiceId).Take(limit).ToArrayAsync(ct);
        var quoteIds = invoices.Select(x => x.CheckoutQuoteId).Distinct().ToArray();
        var paymentIds = invoices.Select(x => x.PaymentId).Distinct().ToArray();
        var quotes = quoteIds.Length == 0 ? new Dictionary<int, CheckoutQuote>()
            : await _db.CheckoutQuotes.AsNoTracking().Where(x => quoteIds.Contains(x.CheckoutQuoteId))
                .ToDictionaryAsync(x => x.CheckoutQuoteId, ct);
        var payments = paymentIds.Length == 0 ? new Dictionary<int, Payment>()
            : await _db.Payments.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId))
                .ToDictionaryAsync(x => x.PaymentId, ct);
        var items = invoices.Select(invoice => MapCustomerInvoice(invoice,
            quotes.GetValueOrDefault(invoice.CheckoutQuoteId), payments.GetValueOrDefault(invoice.PaymentId))).ToArray();
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
        if (quote.Status != "OPEN") return (null, StateConflict());
        if (quote.ExpiresAtUtc <= DateTime.UtcNow) { quote.Status = "EXPIRED"; await _db.SaveChangesAsync(ct); return (null, Conflict(new { code = "quote_expired", message = "Báo giá đã hết hạn." })); }
        return (quote, null);
    }

    private static Payment NewPayment(CheckoutQuote q, string channel, string method, string? location, string key, string hash, int attempt, decimal amount)
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
    private static void Recalculate(CheckoutQuote q) { q.TotalAmount = Math.Max(0, q.Subtotal - q.VoucherDiscount - q.PointsDiscount); q.UpdatedAtUtc = DateTime.UtcNow; }
    private static object MapQuote(CheckoutQuote q) => new { q.CheckoutQuoteId, q.QuoteCode, q.DiningSessionId, q.Status, subtotal = Money(q.Subtotal), voucherDiscount = Money(q.VoucherDiscount), q.PointsApplied, pointsDiscount = Money(q.PointsDiscount), totalAmount = Money(q.TotalAmount), q.ExpiresAtUtc, items = JsonSerializer.Deserialize<JsonElement>(q.ItemsJson) };
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
        subtotal = Money(invoice.Subtotal), discount = Money(invoice.Discount), totalAmount = Money(invoice.TotalAmount),
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
    private static string Code(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(40, prefix.Length + 1 + 14 + 1 + 32)].ToUpperInvariant();
    private static bool ValidCommand(int customerId, string? key, string? hash) => customerId > 0 && !string.IsNullOrWhiteSpace(key) && key.Length <= 140 && !string.IsNullOrWhiteSpace(hash) && hash.Length == 64;
    private ActionResult Validation() => BadRequest(new { code = "validation_failed", message = "Dữ liệu không hợp lệ." });
    private ActionResult IdempotencyConflict() => Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
    private ActionResult StateConflict() => Conflict(new { code = "state_conflict", message = "Trạng thái hiện tại không cho phép thao tác này." });
    private ActionResult ForbidResult() => StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu dữ liệu này." });

    public sealed record QuoteRequest(int CustomerId, string IdempotencyKey, string RequestHash);
    public sealed record VoucherRequest(int CustomerId, int? VoucherId);
    public sealed record LoyaltyRequest(int CustomerId, int Points);
    public sealed record PaymentRequestCommand(int CustomerId, int CheckoutQuoteId);
    public sealed record CustomerRequest(int CustomerId);
    public sealed record CreatePaymentRequest(int CustomerId, string Channel, string Method, string? Location, string IdempotencyKey, string RequestHash);
    public sealed record RetryPaymentRequest(int CustomerId, string IdempotencyKey, string RequestHash);
    public sealed record CreateSplitBillRequest(int CustomerId, int CheckoutQuoteId, IReadOnlyList<decimal>? Amounts, string IdempotencyKey, string RequestHash);
    public sealed record SplitPaymentRequest(int CustomerId, int SplitBillPartId, string Channel, string Method, string? Location, string IdempotencyKey, string RequestHash);
}
