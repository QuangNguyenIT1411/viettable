using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

public enum MobileSettlementStep { CreateQuote, CreatePayment, ConfirmPending, AlreadyPaid }

public sealed record MobileSettlementResult(int PaymentId, int CustomerId, int DiningSessionId, string InvoiceCode,
    decimal TotalAmount, decimal ChangeAmount, int PointsUsed);

/// <summary>
/// Lets the cashier dashboard "Thanh toán" button settle an app dining session through the SAME mobile
/// quote → ONSITE payment → <see cref="PaymentFinalizationService.ConfirmAsync"/> path as the onsite panel,
/// so it issues the mobile invoice, closes the session and never creates a second payment or legacy bill.
/// </summary>
public sealed class CashierMobileSettlementService(BillingDbContext db, OrdersApiClient orders, PaymentFinalizationService finalization)
{
    // Pure rule (unit-tested). quoteStatus is the session's latest PAYMENT_REQUESTED/PAID quote, or null if none.
    public static MobileSettlementStep Decide(string? quoteStatus, bool hasSplitBill, IReadOnlyCollection<(string Channel, string Status)> payments)
    {
        if (quoteStatus is null) return MobileSettlementStep.CreateQuote;
        if (payments.Any(x => x.Status == "SUCCEEDED") && !hasSplitBill) return MobileSettlementStep.AlreadyPaid;
        if (quoteStatus == "PAID" && !hasSplitBill)
            throw new PaymentDomainException("payment_state_conflict", "Phiên đã thanh toán nhưng không tìm thấy giao dịch thành công.");
        if (hasSplitBill)
            throw new PaymentDomainException("split_bill_in_app", "Khách đã chia hóa đơn trong ứng dụng. Hãy xác nhận từng phần ở mục Thanh toán ứng dụng tại chỗ.");
        if (quoteStatus != "PAYMENT_REQUESTED")
            throw new PaymentDomainException("state_conflict", "Trạng thái thanh toán của phiên không cho phép thao tác này.");
        var pending = payments.Where(x => x.Status is "PENDING" or "PROCESSING").ToArray();
        if (pending.Any(x => x.Channel != "ONSITE"))
            throw new PaymentDomainException("online_payment_in_progress", "Khách đang thanh toán trực tuyến trong ứng dụng. Hãy chờ giao dịch đó kết thúc.");
        if (pending.Length > 1)
            throw new PaymentDomainException("payment_state_conflict", "Phiên có nhiều giao dịch đang chờ. Hãy xác nhận ở mục Thanh toán ứng dụng tại chỗ.");
        return pending.Length == 1 ? MobileSettlementStep.ConfirmPending : MobileSettlementStep.CreatePayment;
    }

    public async Task<MobileSettlementResult> SettleAsync(int sessionId, int customerId, int branchId, int employeeId,
        string method, decimal tendered, string commandKey, CancellationToken ct)
    {
        var quote = await db.CheckoutQuotes
            .Where(x => x.DiningSessionId == sessionId && (x.Status == "PAYMENT_REQUESTED" || x.Status == "PAID"))
            .OrderByDescending(x => x.CheckoutQuoteId).FirstOrDefaultAsync(ct)
            ?? await CreateRequestedQuoteAsync(sessionId, customerId, commandKey, ct);
        if (quote.CustomerId != customerId || quote.BranchId != branchId)
            throw new PaymentDomainException("payment_not_collectible", "Phiên không thuộc chi nhánh hoặc khách hàng này.", 403);
        if (method == "CASH" && tendered < quote.TotalAmount)
            throw new PaymentDomainException("insufficient_cash", "Số tiền khách đưa không đủ để thanh toán hóa đơn.", 400);
        // A payment waiting or done on ANOTHER quote of this session wins: never collect twice (incident 25/10).
        var other = await db.Payments.AsNoTracking()
            .Where(x => x.DiningSessionId == sessionId && x.CheckoutQuoteId != quote.CheckoutQuoteId
                && (x.Status == "PENDING" || x.Status == "PROCESSING" || x.Status == "SUCCEEDED"))
            .OrderByDescending(x => x.PaymentId).FirstOrDefaultAsync(ct);
        if (other is not null)
            throw other.Status == "SUCCEEDED"
                ? new PaymentDomainException("session_already_paid", $"Phiên đã được thanh toán (giao dịch {other.PaymentCode}). Không thu thêm.")
                : new PaymentDomainException("payment_in_progress", $"Phiên đang có giao dịch {other.PaymentCode} chờ xác nhận. Hãy xác nhận giao dịch đó ở mục Thanh toán ứng dụng tại chỗ.");

        int paymentId;
        var createdAndConfirmed = false;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await PaymentFinalizationService.LockQuoteAsync(db, quote.CheckoutQuoteId, ct);
            await db.Entry(quote).ReloadAsync(ct);
            var payments = await db.Payments.Where(x => x.CheckoutQuoteId == quote.CheckoutQuoteId).ToListAsync(ct);
            var hasSplit = await db.SplitBills.AnyAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct);
            switch (Decide(quote.Status, hasSplit, payments.Select(x => (x.Channel, x.Status)).ToArray()))
            {
                case MobileSettlementStep.AlreadyPaid:
                    paymentId = payments.Where(x => x.Status == "SUCCEEDED").OrderBy(x => x.PaymentId).First().PaymentId;
                    break;
                case MobileSettlementStep.ConfirmPending:
                    paymentId = payments.Single(x => x.Status is "PENDING" or "PROCESSING").PaymentId;
                    break;
                default:
                    // Create and confirm in ONE transaction: a failure (e.g. Orders no longer waits for this quote,
                    // or the session already has an invoice) rolls the new payment back instead of leaving it PENDING.
                    await finalization.EnsureSessionAwaitsQuoteAsync(sessionId, quote.CheckoutQuoteId, ct);
                    var key = $"cashier:{commandKey}:pay";
                    var payment = MobileCheckoutController.NewPayment(quote, "ONSITE", method, "CASHIER", key, Hash(key), 1, quote.TotalAmount);
                    db.Payments.Add(payment);
                    await db.SaveChangesAsync(ct);
                    await finalization.ApplySuccessAsync(payment, quote, employeeId, null, null, ct);
                    paymentId = payment.PaymentId;
                    createdAndConfirmed = true;
                    break;
            }
            await tx.CommitAsync(ct);
        }

        // Idempotent: an already SUCCEEDED payment only re-runs the durable reconciliation.
        Payment confirmed;
        if (createdAndConfirmed)
        {
            await finalization.ReconcileSafelyAsync(quote.CheckoutQuoteId, ct);
            confirmed = await db.Payments.AsNoTracking().SingleAsync(x => x.PaymentId == paymentId, ct);
        }
        else confirmed = await finalization.ConfirmAsync(paymentId, employeeId, branchId, null, ct);
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.CheckoutQuoteId == quote.CheckoutQuoteId, ct);
        var change = method == "CASH" ? Math.Max(0, tendered - quote.TotalAmount) : 0;
        return new MobileSettlementResult(confirmed.PaymentId, customerId, sessionId, invoice?.InvoiceCode ?? string.Empty,
            quote.TotalAmount, change, quote.PointsApplied);
    }

    // The customer never asked to pay: open and lock a quote on their behalf, exactly as the app would.
    private async Task<CheckoutQuote> CreateRequestedQuoteAsync(int sessionId, int customerId, string commandKey, CancellationToken ct)
    {
        var key = $"cashier:{commandKey}:quote";
        var quote = await db.CheckoutQuotes.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (quote is null)
        {
            OrdersApiClient.DiningSessionCheckoutContext context;
            try
            {
                context = await orders.GetDiningSessionCheckoutContextAsync(sessionId, customerId, ct)
                    ?? throw new PaymentDomainException("session_not_found", "Không tìm thấy phiên phục vụ.", 404);
            }
            catch (HttpRequestException ex)
            { throw new PaymentDomainException("checkout_context_failed", "Phiên không còn món chờ thanh toán hoặc đã kết thúc.", (int?)ex.StatusCode ?? 503); }
            var now = DateTime.UtcNow;
            quote = new CheckoutQuote
            {
                QuoteCode = MobileCheckoutController.Code("QUO"), DiningSessionId = sessionId, DiningSessionCode = context.DiningSessionCode,
                CustomerId = customerId, BranchId = context.BranchId, TableId = context.TableId, Status = "OPEN",
                Subtotal = context.Subtotal, TotalAmount = context.Subtotal, VoucherDiscount = 0, PointsApplied = 0, PointsDiscount = 0,
                IdempotencyKey = key, RequestHash = Hash(key), ItemsJson = JsonSerializer.Serialize(context.Items),
                ExpiresAtUtc = now.AddMinutes(15), CreatedAtUtc = now, UpdatedAtUtc = now
            };
            ReservationDepositService.AttachToQuote(quote, await ReservationDepositService.PaidForReservationAsync(db, context.ReservationId, ct));
            db.CheckoutQuotes.Add(quote);
            await db.SaveChangesAsync(ct);
        }
        if (quote.Status == "OPEN")
        {
            try { await orders.RequestPaymentAsync(sessionId, customerId, quote.CheckoutQuoteId, ct); }
            catch (HttpRequestException ex)
            { throw new PaymentDomainException("payment_request_conflict", "Khách vừa yêu cầu thanh toán trong ứng dụng. Hãy bấm Thanh toán lại.", (int?)ex.StatusCode ?? 409); }
            quote.Status = "PAYMENT_REQUESTED";
            quote.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return quote;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
