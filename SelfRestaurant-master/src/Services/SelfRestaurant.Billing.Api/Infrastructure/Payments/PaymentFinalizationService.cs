using System.Data;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

public sealed class PaymentDomainException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

// Invoice is also the durable reconciliation work item. External calls occur only AFTER
// payment/part/invoice commit. Both receivers must be idempotent on their persisted key.
public sealed class PaymentFinalizationService(BillingDbContext db, OrdersApiClient orders,
    CustomersApiClient customers, ILogger<PaymentFinalizationService> logger,
    IConfiguration? configuration = null, IHostEnvironment? environment = null, CustomerInboxClient? inbox = null)
{
    public static Task LockQuoteAsync(BillingDbContext db, int quoteId, CancellationToken ct) =>
        SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(db, "mobile-settlement:" + quoteId, ct);

    public async Task<Payment> ConfirmAsync(int paymentId, int? employeeId, int? branchId,
        string? providerTransactionId, CancellationToken ct, string? providerName = null)
    {
        var known = await db.Payments.AsNoTracking().Where(x => x.PaymentId == paymentId)
            .Select(x => new { x.CheckoutQuoteId, x.DiningSessionId, x.Status }).SingleOrDefaultAsync(ct)
            ?? throw new PaymentDomainException("payment_not_found", "Không tìm thấy giao dịch.", 404);
        var quoteId = known.CheckoutQuoteId;
        // Money may only be confirmed while Orders still waits for exactly this quote; otherwise the session could
        // never be closed (incident 25/10: paid quote 35 while the session was locked for quote 36).
        if (known.Status == "PENDING") await EnsureSessionAwaitsQuoteAsync(known.DiningSessionId, quoteId, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            await LockQuoteAsync(db, quoteId, ct);
            var payment = await db.Payments.SingleAsync(x => x.PaymentId == paymentId, ct);
            var quote = await db.CheckoutQuotes.SingleAsync(x => x.CheckoutQuoteId == quoteId, ct);
            if (employeeId.HasValue && (payment.Channel != "ONSITE" || quote.BranchId != branchId))
                throw new PaymentDomainException("payment_not_collectible", "Giao dịch không thuộc thanh toán tại chỗ của chi nhánh này.", 403);
            if (payment.Status == "SUCCEEDED") { await tx.CommitAsync(ct); }
            else
            {
                await ApplySuccessAsync(payment, quote, employeeId, providerTransactionId, providerName, ct);
                await tx.CommitAsync(ct);
            }
        }
        if (environment?.IsDevelopment() == true && configuration?.GetValue<bool>("Recovery:TestFailAfterPaymentCommit") == true)
            throw new InvalidOperationException("Injected failure after durable payment commit.");
        await ReconcileSafelyAsync(quoteId, ct);
        return await db.Payments.SingleAsync(x => x.PaymentId == paymentId, ct);
    }

    public const string CashierCancelledCode = "cashier_cancelled";

    /// <summary>
    /// Cashier cancels a waiting ONSITE payment (e.g. the guest pays cash instead of the transfer) and unlocks the session
    /// so the guest or cashier can start a new payment. Holds the same quote lock as <see cref="ConfirmAsync"/>: a cancel
    /// and a confirmation (cashier or SePay) of the same payment never both win. Orders is unlocked inside the lock,
    /// before the Billing commit; a failed commit leaves the payment PENDING and a retry finishes it (Orders is idempotent).
    /// </summary>
    public async Task<Payment> CancelPendingAsync(int paymentId, int employeeId, int branchId, CancellationToken ct)
    {
        var quoteId = await db.Payments.AsNoTracking().Where(x => x.PaymentId == paymentId)
            .Select(x => (int?)x.CheckoutQuoteId).SingleOrDefaultAsync(ct)
            ?? throw new PaymentDomainException("payment_not_found", "Không tìm thấy giao dịch.", 404);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockQuoteAsync(db, quoteId, ct);
        var payment = await db.Payments.SingleAsync(x => x.PaymentId == paymentId, ct);
        var quote = await db.CheckoutQuotes.SingleAsync(x => x.CheckoutQuoteId == quoteId, ct);
        if (payment.Channel != "ONSITE" || quote.BranchId != branchId)
            throw new PaymentDomainException("payment_not_collectible", "Giao dịch không thuộc thanh toán tại chỗ của chi nhánh này.", 403);
        var decision = CancelDecision(payment.Status, payment.FailureCode,
            await db.SplitBills.AnyAsync(x => x.CheckoutQuoteId == quoteId, ct));
        if (decision == CancelStep.AlreadyCancelled) { await tx.CommitAsync(ct); return payment; }
        try { await orders.CancelPaymentRequestForQuoteAsync(payment.DiningSessionId, payment.CustomerId, quoteId, ct); }
        catch (HttpRequestException ex)
        {
            throw new PaymentDomainException("session_unlock_failed",
                "Không mở khóa được phiên để khách chọn lại cách thanh toán. Giao dịch vẫn đang chờ, vui lòng thử lại.", (int?)ex.StatusCode is 409 ? 409 : 503);
        }
        var now = DateTime.UtcNow;
        payment.Status = "CANCELLED";
        payment.FailureCode = $"{CashierCancelledCode}:{employeeId}";
        payment.CompletedAtUtc = payment.UpdatedAtUtc = now;
        quote.Status = "CANCELLED"; quote.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Payment {PaymentId} ({Method}) cancelled by cashier {EmployeeId}; session {SessionId} unlocked",
            payment.PaymentId, payment.Method, employeeId, payment.DiningSessionId);
        return payment;
    }

    public enum CancelStep { Cancel, AlreadyCancelled }

    /// <summary>Pure rule (unit-tested): only a waiting, unsplit payment can be cancelled; a repeated cancel is a no-op.</summary>
    public static CancelStep CancelDecision(string status, string? failureCode, bool hasSplitBill)
    {
        if (status == "CANCELLED" && failureCode?.StartsWith(CashierCancelledCode + ":", StringComparison.Ordinal) == true)
            return CancelStep.AlreadyCancelled;
        if (status == "SUCCEEDED")
            throw new PaymentDomainException("payment_already_succeeded", "Giao dịch đã được thanh toán nên không thể hủy.");
        if (status != "PENDING")
            throw new PaymentDomainException("payment_state_conflict", "Giao dịch đã kết thúc nên không thể hủy.");
        if (hasSplitBill)
            throw new PaymentDomainException("split_bill_in_app", "Khách đã chia hóa đơn trong ứng dụng; chưa hỗ trợ hủy từng phần.");
        return CancelStep.Cancel;
    }

    /// <summary>True when Orders still waits for payment of exactly this quote (pure rule, unit-tested).</summary>
    public static bool SessionAwaitsQuote(OrdersApiClient.SessionPaymentState? state, int quoteId) =>
        state is { Ended: false, Status: "PAYMENT_REQUESTED" } && state.CheckoutQuoteId == quoteId;

    /// <summary>Throws session_not_payable when Orders no longer waits for this quote. Orders unreachable → the HTTP error escapes.</summary>
    public async Task EnsureSessionAwaitsQuoteAsync(int sessionId, int quoteId, CancellationToken ct)
    {
        var state = await orders.GetPaymentStateAsync(sessionId, ct);
        if (!SessionAwaitsQuote(state, quoteId))
            throw new PaymentDomainException("session_not_payable",
                "Phiên không còn chờ thanh toán cho giao dịch này, nên không thể xác nhận. Hãy kiểm tra lại với khách.");
    }

    /// <summary>
    /// Marks a PENDING payment SUCCEEDED and issues the invoice. Caller owns the transaction and the quote lock, so a
    /// payment can be created and confirmed atomically (cashier checkout) — any failure rolls both back.
    /// </summary>
    public async Task ApplySuccessAsync(Payment payment, CheckoutQuote quote, int? employeeId,
        string? providerTransactionId, string? providerName, CancellationToken ct)
    {
        var split = await db.SplitBills.Include(x => x.Parts).SingleOrDefaultAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct);
        var part = ValidatePendingTransition(payment, quote, split);
        payment.Status = "SUCCEEDED";
        payment.ConfirmedByEmployeeId = employeeId;
        payment.ProviderTransactionId = providerTransactionId;
        if (providerName is not null) payment.ProviderName = providerName; // e.g. SEPAY: "Đã thanh toán tự động"
        payment.CompletedAtUtc = payment.UpdatedAtUtc = DateTime.UtcNow;
        if (part is not null) part.Status = "PAID";
        if (split is null || split.Parts.All(x => x.Status == "PAID"))
        {
            if (split is not null) { split.Status = "PAID"; split.UpdatedAtUtc = DateTime.UtcNow; }
            quote.Status = "PAID"; quote.UpdatedAtUtc = DateTime.UtcNow;
            if (await db.Invoices.AnyAsync(x => x.DiningSessionId == quote.DiningSessionId && x.CheckoutQuoteId != quote.CheckoutQuoteId, ct))
                throw new PaymentDomainException("session_already_invoiced", "Phiên này đã có hóa đơn từ một lần thanh toán khác. Không thu thêm.");
            if (!await db.Invoices.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct))
                db.Invoices.Add(new Invoice { InvoiceCode = $"INV-{Guid.NewGuid():N}", CheckoutQuoteId = quote.CheckoutQuoteId,
                    PaymentId = payment.PaymentId, DiningSessionId = quote.DiningSessionId, CustomerId = quote.CustomerId,
                    Subtotal = quote.Subtotal, Discount = quote.VoucherDiscount + quote.PointsDiscount, DepositApplied = quote.DepositApplied,
                    TotalAmount = quote.TotalAmount, ItemsJson = quote.ItemsJson, IssuedAtUtc = DateTime.UtcNow });
            await ReservationDepositService.MarkAppliedAsync(db, quote, ct);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Payment {PaymentId} collected via {Channel}, cashier {EmployeeId}, split part {PartId}", payment.PaymentId, payment.Channel, employeeId, part?.SplitBillPartId);
    }

    public static SplitBillPart? ValidatePendingTransition(Payment payment, CheckoutQuote quote, SplitBill? split)
    {
        if (payment.Status != "PENDING") throw new PaymentDomainException("payment_state_conflict", "Giao dịch đã kết thúc hoặc đã được thay thế.");
        if (quote.Status != "PAYMENT_REQUESTED" || payment.CheckoutQuoteId != quote.CheckoutQuoteId
            || payment.CustomerId != quote.CustomerId || payment.DiningSessionId != quote.DiningSessionId || payment.Currency != "VND")
            throw new PaymentDomainException("payment_session_mismatch", "Giao dịch không khớp báo giá đang thanh toán.");
        var part = split?.Parts.SingleOrDefault(x => x.PaymentId == payment.PaymentId);
        if (split is not null && (part is null || part.Status != "PENDING"))
            throw new PaymentDomainException("payment_attempt_replaced", "Giao dịch không còn là lần thanh toán hiện tại của phần hóa đơn.");
        if (payment.Amount != (part?.Amount ?? quote.TotalAmount)) throw new PaymentDomainException("payment_amount_mismatch", "Số tiền giao dịch không khớp nghĩa vụ thanh toán.");
        return part;
    }

    public async Task ReconcileSafelyAsync(int quoteId, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockQuoteAsync(db, quoteId, ct);
            var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.CheckoutQuoteId == quoteId, ct);
            if (invoice is null || invoice.SettlementCompletedAtUtc.HasValue) return;
            var quote = await db.CheckoutQuotes.SingleAsync(x => x.CheckoutQuoteId == quoteId, ct);
            await orders.CompletePaymentAsync(invoice.DiningSessionId, invoice.CustomerId, quoteId, invoice.PaymentId, ct);
            var settled = await customers.SettleLoyaltyAsync(invoice.CustomerId,
                // Points are earned on money paid: what is paid now plus the deposit paid ahead.
                new CustomersApiClient.LoyaltySettlementRequest(quote.PointsApplied, invoice.TotalAmount + invoice.DepositApplied, $"mobile-invoice:{invoice.InvoiceCode}"), ct);
            if (settled is null) throw new HttpRequestException("Loyalty settlement unavailable");
            invoice.RecoveryAttemptCount = Math.Min(invoice.RecoveryAttemptCount + 1, 1000000);
            invoice.RecoveryLastAttemptAtUtc = DateTime.UtcNow;
            invoice.SettlementCompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            // Best effort, once (a settled invoice is never reconciled again): the guest sees the points earned.
            if (inbox is not null)
            {
                try
                {
                    await inbox.LoyaltySettledAsync(invoice.CustomerId, invoice.PaymentId, settled.PointsBefore,
                        settled.PointsEarned, settled.CustomerPoints, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                { logger.LogWarning("Loyalty notification skipped for quote {QuoteId}; {ErrorType}", quoteId, ex.GetType().Name); }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Payment remains SUCCEEDED. The persisted invoice is retried by the worker.
            db.ChangeTracker.Clear();
            logger.LogWarning("Invoice reconciliation pending for quote {QuoteId}; {ErrorType}", quoteId, ex.GetType().Name);
            try
            {
                var pending = await db.Invoices.FirstOrDefaultAsync(x => x.CheckoutQuoteId == quoteId && x.SettlementCompletedAtUtc == null, ct);
                if (pending is not null)
                {
                    pending.RecoveryAttemptCount = Math.Min(pending.RecoveryAttemptCount + 1, 1000000);
                    pending.RecoveryLastAttemptAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception markerError) when (markerError is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Invoice retry marker deferred for quote {QuoteId}; {ErrorType}", quoteId, markerError.GetType().Name);
            }
        }
    }
}

public sealed class PaymentReconciliationWorker(IServiceScopeFactory scopes) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var ids = await db.Invoices.AsNoTracking().Where(x => x.SettlementCompletedAtUtc == null)
            .OrderBy(x => x.RecoveryLastAttemptAtUtc.HasValue)
            .ThenBy(x => x.RecoveryLastAttemptAtUtc).ThenBy(x => x.InvoiceId)
            .Select(x => x.CheckoutQuoteId).Take(50).ToListAsync(cancellationToken);
        foreach (var id in ids)
            await scope.ServiceProvider.GetRequiredService<PaymentFinalizationService>().ReconcileSafelyAsync(id, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            { using var scope = scopes.CreateScope(); scope.ServiceProvider.GetRequiredService<ILogger<PaymentReconciliationWorker>>().LogWarning("Payment reconciliation deferred: {ErrorType}", ex.GetType().Name); }
        }
    }
}
