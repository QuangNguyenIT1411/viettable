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
    CustomersApiClient customers, ILogger<PaymentFinalizationService> logger)
{
    public static Task LockQuoteAsync(BillingDbContext db, int quoteId, CancellationToken ct) =>
        SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(db, "mobile-settlement:" + quoteId, ct);

    public async Task<Payment> ConfirmAsync(int paymentId, int? employeeId, int? branchId,
        string? providerTransactionId, CancellationToken ct)
    {
        var quoteId = await db.Payments.Where(x => x.PaymentId == paymentId).Select(x => (int?)x.CheckoutQuoteId).SingleOrDefaultAsync(ct)
            ?? throw new PaymentDomainException("payment_not_found", "Không tìm thấy giao dịch.", 404);
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
                var split = await db.SplitBills.Include(x => x.Parts).SingleOrDefaultAsync(x => x.CheckoutQuoteId == quoteId, ct);
                var part = ValidatePendingTransition(payment, quote, split);
                payment.Status = "SUCCEEDED";
                payment.ConfirmedByEmployeeId = employeeId;
                payment.ProviderTransactionId = providerTransactionId;
                payment.CompletedAtUtc = payment.UpdatedAtUtc = DateTime.UtcNow;
                if (part is not null) part.Status = "PAID";
                if (split is null || split.Parts.All(x => x.Status == "PAID"))
                {
                    if (split is not null) { split.Status = "PAID"; split.UpdatedAtUtc = DateTime.UtcNow; }
                    quote.Status = "PAID"; quote.UpdatedAtUtc = DateTime.UtcNow;
                    if (!await db.Invoices.AnyAsync(x => x.CheckoutQuoteId == quoteId, ct))
                        db.Invoices.Add(new Invoice { InvoiceCode = $"INV-{Guid.NewGuid():N}", CheckoutQuoteId = quoteId,
                            PaymentId = paymentId, DiningSessionId = quote.DiningSessionId, CustomerId = quote.CustomerId,
                            Subtotal = quote.Subtotal, Discount = quote.VoucherDiscount + quote.PointsDiscount,
                            TotalAmount = quote.TotalAmount, ItemsJson = quote.ItemsJson, IssuedAtUtc = DateTime.UtcNow });
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                logger.LogInformation("Payment {PaymentId} collected via {Channel}, cashier {EmployeeId}, split part {PartId}", paymentId, payment.Channel, employeeId, part?.SplitBillPartId);
            }
        }
        await ReconcileSafelyAsync(quoteId, ct);
        return await db.Payments.SingleAsync(x => x.PaymentId == paymentId, ct);
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
                new CustomersApiClient.LoyaltySettlementRequest(quote.PointsApplied, invoice.TotalAmount, $"mobile-invoice:{invoice.InvoiceCode}"), ct);
            if (settled is null) throw new HttpRequestException("Loyalty settlement unavailable");
            invoice.SettlementCompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Payment remains SUCCEEDED. The persisted invoice is retried by the worker.
            db.ChangeTracker.Clear();
            logger.LogWarning("Invoice reconciliation pending for quote {QuoteId}; {ErrorType}", quoteId, ex.GetType().Name);
        }
    }
}

public sealed class PaymentReconciliationWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            try
            {
                var ids = await db.Invoices.AsNoTracking().Where(x => x.SettlementCompletedAtUtc == null)
                    .OrderBy(x => x.InvoiceId).Select(x => x.CheckoutQuoteId).Take(50).ToListAsync(stoppingToken);
                foreach (var id in ids) await scope.ServiceProvider.GetRequiredService<PaymentFinalizationService>().ReconcileSafelyAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            { scope.ServiceProvider.GetRequiredService<ILogger<PaymentReconciliationWorker>>().LogWarning("Payment reconciliation deferred: {ErrorType}", ex.GetType().Name); }
        }
    }
}
