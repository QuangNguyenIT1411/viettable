using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>Uses of one voucher: by the customer asking, and by everyone.</summary>
public sealed record VoucherUse(int UsedByCustomer, int UsedTotal)
{
    public static readonly VoucherUse None = new(0, 0);
}

/// <summary>
/// Can this customer use the voucher on this bill? Code and Reason say why not (shown greyed in the app);
/// Remaining* are null when there is no limit.
/// </summary>
public sealed record VoucherCheck(bool Usable, string? Code, string? Reason, int? RemainingForCustomer, int? RemainingTotal, decimal Discount);

/// <summary>
/// Voucher rules (owner 28/10/2026). A voucher is never applied by itself: only when the guest picks it. Limits per
/// customer (1 = one-time, N, or none) and in total (optional). A use is a bill that is paid or waiting for the money
/// (quote PAYMENT_REQUESTED/PAID with the voucher actually taken off); cancelling the payment request gives it back.
/// </summary>
public static class VoucherRules
{
    public const string NotFound = "voucher_not_found";
    public const string NotStarted = "voucher_not_started";
    public const string Expired = "voucher_expired";
    public const string UsedUp = "voucher_used_up";
    public const string SoldOut = "voucher_sold_out";
    public const string MinimumNotMet = "voucher_minimum_not_met";

    /// <summary>Quote statuses that hold a use of their voucher.</summary>
    public static readonly string[] HoldingStatuses = { "PAYMENT_REQUESTED", "PAID" };

    public static decimal Discount(Voucher voucher, decimal subtotal)
    {
        if (subtotal <= 0 || voucher.DiscountValue <= 0) return 0;
        var percent = string.Equals(voucher.DiscountType, CheckoutPricing.Percent, StringComparison.OrdinalIgnoreCase);
        var value = percent ? subtotal * Math.Min(voucher.DiscountValue, 100) / 100 : voucher.DiscountValue;
        if (voucher.MaxDiscount is > 0) value = Math.Min(value, voucher.MaxDiscount.Value);
        return Math.Max(0, Math.Min(subtotal, Math.Floor(value)));
    }

    /// <summary>The first reason that stops the voucher, in the order a guest understands best. subtotal null = no bill yet.</summary>
    public static VoucherCheck Check(Voucher voucher, DateTime nowUtc, decimal? subtotal, VoucherUse use)
    {
        int? mine = voucher.MaxUsesPerCustomer is > 0 ? Math.Max(0, voucher.MaxUsesPerCustomer.Value - use.UsedByCustomer) : null;
        int? total = voucher.MaxTotalUses is > 0 ? Math.Max(0, voucher.MaxTotalUses.Value - use.UsedTotal) : null;
        var discount = subtotal is > 0 ? Discount(voucher, subtotal.Value) : 0;
        VoucherCheck No(string code, string reason) => new(false, code, reason, mine, total, discount);
        if (!voucher.IsActive) return No(NotFound, "Voucher đã ngừng áp dụng");
        if (nowUtc < voucher.StartsAtUtc) return No(NotStarted, $"Áp dụng từ {VietnamDate(voucher.StartsAtUtc)}");
        if (nowUtc > voucher.EndsAtUtc) return No(Expired, "Đã hết hạn");
        if (mine == 0) return No(UsedUp, voucher.MaxUsesPerCustomer == 1 ? "Đã dùng" : $"Đã dùng hết {voucher.MaxUsesPerCustomer} lượt");
        if (total == 0) return No(SoldOut, "Đã hết lượt");
        if (subtotal is not null && subtotal < voucher.MinSubtotal)
            return No(MinimumNotMet, $"Chưa đủ đơn tối thiểu {CheckoutPricing.Money(voucher.MinSubtotal)} (còn thiếu {CheckoutPricing.Money(voucher.MinSubtotal - subtotal.Value)})");
        return new VoucherCheck(true, null, null, mine, total, discount);
    }

    /// <summary>"Còn 2 lượt" / "Còn 1 lượt" / null (no per-customer limit).</summary>
    public static string? RemainingLabel(VoucherCheck check) =>
        check.Usable && check.RemainingForCustomer is > 0 ? $"Còn {check.RemainingForCustomer} lượt" : null;

    private static string VietnamDate(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>Counting uses in the database (see <see cref="VoucherRules"/>).</summary>
public static class VoucherUsage
{
    /// <summary>Uses per voucher, leaving out one quote (the one being priced, so it never counts against itself).</summary>
    public static async Task<IReadOnlyDictionary<int, VoucherUse>> CountAsync(BillingDbContext db, IReadOnlyCollection<int> voucherIds,
        int customerId, int? exceptQuoteId, CancellationToken ct)
    {
        if (voucherIds.Count == 0) return new Dictionary<int, VoucherUse>();
        var rows = await db.CheckoutQuotes.AsNoTracking()
            .Where(x => x.VoucherId != null && voucherIds.Contains(x.VoucherId.Value) && x.VoucherDiscount > 0
                && VoucherRules.HoldingStatuses.Contains(x.Status) && (exceptQuoteId == null || x.CheckoutQuoteId != exceptQuoteId))
            .Select(x => new { VoucherId = x.VoucherId!.Value, x.CustomerId }).ToListAsync(ct);
        return rows.GroupBy(x => x.VoucherId).ToDictionary(g => g.Key,
            g => new VoucherUse(g.Count(x => x.CustomerId == customerId), g.Count()));
    }

    public static async Task<VoucherUse> CountAsync(BillingDbContext db, int voucherId, int customerId, int? exceptQuoteId, CancellationToken ct) =>
        (await CountAsync(db, new[] { voucherId }, customerId, exceptQuoteId, ct)).TryGetValue(voucherId, out var use) ? use : VoucherUse.None;

    /// <summary>
    /// Last check before the quote holds a use (payment request). Callers serialize per voucher (transaction lock
    /// "voucher:{id}") and save inside it, so two guests racing for the last use cannot both get it.
    /// Returns null when the voucher may be used, else the failed check.
    /// </summary>
    public static async Task<VoucherCheck?> BlockingCheckAsync(BillingDbContext db, CheckoutQuote quote, DateTime nowUtc, CancellationToken ct)
    {
        if (quote.VoucherId is null || quote.VoucherDiscount <= 0) return null;
        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherId == quote.VoucherId, ct);
        if (voucher is null) return new VoucherCheck(false, VoucherRules.NotFound, "Voucher không còn tồn tại", null, null, 0);
        var check = VoucherRules.Check(voucher, nowUtc, quote.Subtotal,
            await CountAsync(db, voucher.VoucherId, quote.CustomerId, quote.CheckoutQuoteId, ct));
        return check.Usable ? null : check;
    }

    public static string LockName(int voucherId) => "voucher:" + voucherId;
}
