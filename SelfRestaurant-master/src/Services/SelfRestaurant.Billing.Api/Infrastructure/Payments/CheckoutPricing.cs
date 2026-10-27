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
    decimal VoucherOffer, int PointsRequested, int PointsBalance, decimal DepositAmount, string VoucherLabel = "voucher");

public sealed record PricingResult(decimal Subtotal, int? PromotionId, string? PromotionName, decimal PromotionDiscount,
    decimal VoucherDiscount, string? DiscountNote, int PointsApplied, decimal PointsDiscount, decimal DepositApplied, decimal Total)
{
    public decimal AfterDiscount => Subtotal - PromotionDiscount - VoucherDiscount;
}

/// <summary>
/// The ONE checkout formula (owner 27/10, docs/PROMO_PLAN.md), used by the app quote, the cashier settling an app
/// session and the cashier checking out an order: subtotal → best promotion OR voucher (the better one, never both) →
/// points (10% cap on the amount after the discount) → reservation deposit. Pure, so every rule is unit tested.
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
        var deposit = SelfRestaurant.Deposits.DepositRules.Applicable(input.DepositAmount, afterPoints);
        return new PricingResult(subtotal, promoApplied > 0 ? input.PromotionId : null, promoApplied > 0 ? input.PromotionName : null,
            promoApplied, voucherApplied, note, points, pointsDiscount, deposit, afterPoints - deposit);
    }

    /// <summary>Re-prices a quote from what it holds: the promotion and voucher offers, the points chosen, the deposit.</summary>
    public static void Apply(CheckoutQuote quote, int? pointsBalance = null)
    {
        var result = Compute(new PricingInput(quote.Subtotal, quote.PromotionId, quote.PromotionName, quote.PromotionOfferDiscount,
            quote.VoucherId is null ? 0 : quote.VoucherOfferDiscount, quote.PointsApplied, pointsBalance ?? quote.PointsApplied,
            quote.DepositAmount));
        quote.PromotionDiscount = result.PromotionDiscount;
        quote.VoucherDiscount = result.VoucherDiscount;
        quote.DiscountNote = result.DiscountNote;
        quote.PointsApplied = result.PointsApplied;
        quote.PointsDiscount = result.PointsDiscount;
        quote.DepositApplied = result.DepositApplied;
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

    public static IReadOnlySet<int> ParseBranches(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(x => x > 0).ToHashSet();

    public static string Money(decimal value) =>
        string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0}đ", value);
}
