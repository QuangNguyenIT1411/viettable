using System.Globalization;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>A promotion as the pricing sees it.</summary>
public sealed record PromotionOffer(int PromotionId, string Name, string DiscountType, decimal DiscountValue, decimal? MaxDiscount,
    decimal MinSubtotal, IReadOnlySet<int> BranchIds, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsActive)
{
    public static PromotionOffer From(Promotion p) => new(p.PromotionId, p.Name, p.DiscountType, p.DiscountValue, p.MaxDiscount,
        p.MinSubtotal, CheckoutPricing.ParseBranches(p.BranchIds), p.StartsAtUtc, p.EndsAtUtc, p.IsActive);
}

public sealed record PricingInput(decimal Subtotal, int? PromotionId, string? PromotionName, decimal PromotionOffer,
    decimal VoucherOffer, int PointsRequested, int PointsBalance, decimal DepositAmount, string VoucherLabel = "voucher",
    decimal ServiceFeePercent = 0, decimal VatPercent = 0);

public sealed record PricingResult(decimal Subtotal, int? PromotionId, string? PromotionName, decimal PromotionDiscount,
    decimal VoucherDiscount, string? DiscountNote, int PointsApplied, decimal PointsDiscount, decimal DepositApplied, decimal Total,
    decimal ServiceFeePercent = 0, decimal ServiceFee = 0, decimal VatPercent = 0, decimal Vat = 0)
{
    public decimal AfterDiscount => Subtotal - PromotionDiscount - VoucherDiscount;
    /// <summary>What the bill costs before the deposit comes off: after discount and points, plus service fee and VAT.</summary>
    public decimal GrossTotal => Total + DepositApplied;
}

/// <summary>
/// The ONE checkout formula (owner 27/10 + 05/11, docs/CHECKOUT_FORMULA.md), used by the app quote, the cashier settling
/// an app session and the cashier checking out an order:
///   subtotal → best promotion OR voucher OR the cashier's discount (the better one, never both)
///   → points (10% cap on the amount after the discount)
///   → service fee = round(after points × fee%)
///   → VAT = round((after points + service fee) × VAT%)
///   → reservation deposit comes off what is left to pay.
/// The two percentages are per branch (billing."BranchChargeSettings", 0 = off) and frozen on the quote/bill.
/// Pure, so every rule is unit tested.
/// </summary>
public static class CheckoutPricing
{
    public const string Percent = "PERCENT";
    public const string Fixed = "FIXED";

    /// <summary>Running now, for this branch (or every branch) and a bill at least the minimum.</summary>
    public static bool Eligible(PromotionOffer offer, int branchId, decimal subtotal, DateTime nowUtc) =>
        offer.IsActive && nowUtc >= offer.StartsAtUtc && nowUtc <= offer.EndsAtUtc
        && (offer.BranchIds.Count == 0 || offer.BranchIds.Contains(branchId))
        && subtotal > 0 && subtotal >= offer.MinSubtotal;

    /// <summary>Percent: floor(subtotal × %), capped; fixed: the amount. Never more than the subtotal.</summary>
    public static decimal Discount(PromotionOffer offer, decimal subtotal)
    {
        if (subtotal <= 0 || offer.DiscountValue <= 0) return 0;
        var value = string.Equals(offer.DiscountType, Percent, StringComparison.OrdinalIgnoreCase)
            ? Math.Floor(subtotal * Math.Min(offer.DiscountValue, 100) / 100)
            : Math.Floor(offer.DiscountValue);
        if (string.Equals(offer.DiscountType, Percent, StringComparison.OrdinalIgnoreCase) && offer.MaxDiscount is > 0)
            value = Math.Min(value, Math.Floor(offer.MaxDiscount.Value));
        return Math.Max(0, Math.Min(value, subtotal));
    }

    /// <summary>The single best promotion for this bill (largest discount; ties: the one ending first, then lowest id).</summary>
    public static (PromotionOffer Offer, decimal Discount)? Best(IEnumerable<PromotionOffer> offers, int branchId, decimal subtotal, DateTime nowUtc)
    {
        var best = offers.Where(x => Eligible(x, branchId, subtotal, nowUtc))
            .Select(x => (Offer: x, Discount: Discount(x, subtotal)))
            .Where(x => x.Discount > 0)
            .OrderByDescending(x => x.Discount).ThenBy(x => x.Offer.EndsAtUtc).ThenBy(x => x.Offer.PromotionId)
            .FirstOrDefault();
        return best.Offer is null ? null : best;
    }

    public static PricingResult Compute(PricingInput input)
    {
        var subtotal = Math.Max(0, input.Subtotal);
        var promo = Math.Max(0, Math.Min(input.PromotionOffer, subtotal));
        var voucher = Math.Max(0, Math.Min(input.VoucherOffer, subtotal));
        // Never both: the better one for the guest (a tie keeps the promotion, it needs no code).
        var useVoucher = voucher > promo;
        var promoApplied = useVoucher ? 0 : promo;
        var voucherApplied = useVoucher ? voucher : 0;
        string? note = null;
        if (promo > 0 && voucher > 0)
            note = useVoucher
                ? $"Áp dụng {input.VoucherLabel} (−{Money(voucher)}) vì lợi hơn khuyến mãi \"{input.PromotionName}\" (−{Money(promo)}); không cộng dồn."
                : $"Áp dụng khuyến mãi \"{input.PromotionName}\" (−{Money(promo)}) vì lợi hơn {input.VoucherLabel} (−{Money(voucher)}); không cộng dồn.";
        var afterDiscount = subtotal - promoApplied - voucherApplied;
        var points = input.PointsRequested > 0
            ? SelfRestaurant.Loyalty.LoyaltyRules.RedeemablePoints(input.PointsRequested, Math.Max(0, input.PointsBalance), afterDiscount)
            : 0;
        var pointsDiscount = SelfRestaurant.Loyalty.LoyaltyRules.Discount(points);
        var afterPoints = Math.Max(0, afterDiscount - pointsDiscount);
        var feePercent = ClampPercent(input.ServiceFeePercent);
        var vatPercent = ClampPercent(input.VatPercent);
        var serviceFee = Charge(afterPoints, feePercent);
        var vat = Charge(afterPoints + serviceFee, vatPercent);
        var gross = afterPoints + serviceFee + vat;
        var deposit = SelfRestaurant.Deposits.DepositRules.Applicable(input.DepositAmount, gross);
        return new PricingResult(subtotal, promoApplied > 0 ? input.PromotionId : null, promoApplied > 0 ? input.PromotionName : null,
            promoApplied, voucherApplied, note, points, pointsDiscount, deposit, gross - deposit,
            feePercent, serviceFee, vatPercent, vat);
    }

    /// <summary>Highest percentages an admin may set (owner 05/11: 0 = off).</summary>
    public const decimal MaxServiceFeePercent = 30m;
    public const decimal MaxVatPercent = 20m;

    /// <summary>An amount × percent, rounded to the nearest đồng (half up). 0% or nothing to charge on → 0.</summary>
    public static decimal Charge(decimal amount, decimal percent) =>
        amount <= 0 || percent <= 0 ? 0 : Math.Round(amount * percent / 100m, 0, MidpointRounding.AwayFromZero);

    private static decimal ClampPercent(decimal percent) => percent <= 0 ? 0 : Math.Min(percent, 100m);

    /// <summary>Admin form check for one branch's percentages; null = valid.</summary>
    public static string? ChargeSettingsError(decimal serviceFeePercent, decimal vatPercent)
    {
        if (serviceFeePercent < 0 || serviceFeePercent > MaxServiceFeePercent)
            return $"Phí phục vụ phải từ 0 đến {MaxServiceFeePercent:0.##}% (0 = tắt).";
        if (vatPercent < 0 || vatPercent > MaxVatPercent)
            return $"VAT phải từ 0 đến {MaxVatPercent:0.##}% (0 = tắt).";
        if (serviceFeePercent != Math.Round(serviceFeePercent, 2) || vatPercent != Math.Round(vatPercent, 2))
            return "Phần trăm chỉ lấy tối đa 2 chữ số thập phân.";
        return null;
    }

    /// <summary>
    /// Re-prices a quote from what it holds: the promotion (unless the guest declined it) and voucher offers, the points
    /// chosen, the deposit. A voucher counts only when the guest picked one (VoucherId), never by itself.
    /// </summary>
    public static void Apply(CheckoutQuote quote, int? pointsBalance = null)
    {
        var result = Compute(new PricingInput(quote.Subtotal, quote.PromotionId, quote.PromotionName,
            quote.PromotionDeclined ? 0 : quote.PromotionOfferDiscount,
            quote.VoucherId is null ? 0 : quote.VoucherOfferDiscount, quote.PointsApplied, pointsBalance ?? quote.PointsApplied,
            quote.DepositAmount, ServiceFeePercent: quote.ServiceFeePercent, VatPercent: quote.VatPercent));
        quote.PromotionDiscount = result.PromotionDiscount;
        quote.VoucherDiscount = result.VoucherDiscount;
        quote.DiscountNote = result.DiscountNote;
        quote.PointsApplied = result.PointsApplied;
        quote.PointsDiscount = result.PointsDiscount;
        quote.DepositApplied = result.DepositApplied;
        quote.ServiceFeeAmount = result.ServiceFee;
        quote.VatAmount = result.Vat;
        quote.TotalAmount = result.Total;
        quote.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Puts the best running promotion of the quote's branch on a new quote (its amount is fixed for the quote).</summary>
    public static void AttachPromotion(CheckoutQuote quote, IEnumerable<PromotionOffer> offers, DateTime nowUtc)
    {
        var best = Best(offers, quote.BranchId, quote.Subtotal, nowUtc);
        quote.PromotionId = best?.Offer.PromotionId;
        quote.PromotionName = best?.Offer.Name;
        quote.PromotionOfferDiscount = best?.Discount ?? 0;
    }

    /// <summary>Smallest amount of money an admin may type (0 = none).</summary>
    public const decimal MinMoney = 1_000m;

    /// <summary>
    /// Money typed on the admin forms (owner 29/10): 0 or whole đồng from 1.000đ. "300" is "300.000" typed into a number
    /// box (the dot read as a decimal point), which made a 300.000đ minimum one of 300đ.
    /// </summary>
    public static string? MoneyError(string label, decimal? value) =>
        value is null or 0 ? null
        : value < MinMoney ? $"{label} phải từ {Money(MinMoney)} (đang là {value:0.##}đ). Nhập đủ số, ví dụ 300000 cho 300.000đ."
        : value != Math.Floor(value.Value) ? $"{label} phải là số đồng chẵn."
        : null;

    public static IReadOnlySet<int> ParseBranches(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(x => x > 0).ToHashSet();

    public static string Money(decimal value) =>
        string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0}đ", value);
}
