using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>Service fee / VAT of a branch (owner 05/11). No row = 0% and 0% (off).</summary>
public static class BranchCharges
{
    public static async Task<(decimal ServiceFeePercent, decimal VatPercent)> ForBranchAsync(BillingDbContext db, int branchId, CancellationToken ct)
    {
        var row = await db.BranchChargeSettings.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        return row is null ? (0m, 0m) : (row.ServiceFeePercent, row.VatPercent);
    }

    /// <summary>
    /// Freezes the branch's percentages and the where/when of the session on a new quote; the amounts follow from
    /// <see cref="CheckoutPricing.Apply"/>. Changing the branch's settings later never changes this quote or its invoice.
    /// </summary>
    public static void Stamp(CheckoutQuote quote, (decimal ServiceFeePercent, decimal VatPercent) charges,
        OrdersApiClient.DiningSessionCheckoutContext context)
    {
        quote.ServiceFeePercent = charges.ServiceFeePercent;
        quote.VatPercent = charges.VatPercent;
        quote.BranchNameSnapshot = Trim(context.BranchName, 200);
        quote.BranchAddressSnapshot = Trim(context.BranchAddress, 500);
        quote.TableNameSnapshot = Trim(context.TableName, 100);
        quote.SessionStartedAtUtc = context.StartedAtUtc;
    }

    internal static string? Trim(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
    }
}

/// <summary>
/// The invoice as issued (owner 05/11): every amount and percentage copied from the paid quote, plus where, when and
/// who. Pure, so "an old invoice never changes" is unit tested: nothing here reads the menu or the branch settings.
/// </summary>
public static class InvoiceSnapshot
{
    public const int CurrentVersion = 1;

    public static Invoice Create(CheckoutQuote quote, Payment payment, int? cashierEmployeeId, string? cashierName, DateTime nowUtc) => new()
    {
        InvoiceCode = $"INV-{Guid.NewGuid():N}",
        CheckoutQuoteId = quote.CheckoutQuoteId,
        PaymentId = payment.PaymentId,
        DiningSessionId = quote.DiningSessionId,
        CustomerId = quote.CustomerId,
        Subtotal = quote.Subtotal,
        Discount = quote.PromotionDiscount + quote.VoucherDiscount + quote.PointsDiscount,
        PromotionDiscount = quote.PromotionDiscount,
        VoucherDiscount = quote.VoucherDiscount,
        PointsUsed = quote.PointsApplied,
        PointsDiscount = quote.PointsDiscount,
        ServiceFeePercent = quote.ServiceFeePercent,
        ServiceFeeAmount = quote.ServiceFeeAmount,
        VatPercent = quote.VatPercent,
        VatAmount = quote.VatAmount,
        DepositApplied = quote.DepositApplied,
        TotalAmount = quote.TotalAmount,
        // Points are earned on money paid: what is paid now plus the deposit paid ahead (same as the loyalty settlement).
        PointsEarned = SelfRestaurant.Loyalty.LoyaltyRules.PointsEarned(quote.TotalAmount + quote.DepositApplied),
        ItemsJson = quote.ItemsJson,
        BranchId = quote.BranchId,
        BranchNameSnapshot = quote.BranchNameSnapshot,
        BranchAddressSnapshot = quote.BranchAddressSnapshot,
        TableNameSnapshot = quote.TableNameSnapshot,
        SessionStartedAtUtc = quote.SessionStartedAtUtc,
        PaidAtUtc = payment.CompletedAtUtc ?? nowUtc,
        PaymentMethodSnapshot = payment.Method,
        BankTransactionRef = BranchCharges.Trim(payment.ProviderTransactionId, 100),
        CashierEmployeeId = cashierEmployeeId,
        CashierNameSnapshot = BranchCharges.Trim(cashierName, 200),
        SnapshotVersion = CurrentVersion,
        IssuedAtUtc = nowUtc
    };
}
