namespace SelfRestaurant.Billing.Api.Persistence.Entities;

/// <summary>
/// A restaurant promotion created by an admin (docs/PROMO_PLAN.md): applied by itself to a bill that qualifies, at most
/// one per bill (the best), never together with a voucher (the better of the two wins).
/// </summary>
public sealed class Promotion
{
    public int PromotionId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>PERCENT (DiscountValue %, capped by MaxDiscount when set) or FIXED (DiscountValue đ).</summary>
    public string DiscountType { get; set; } = "PERCENT";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinSubtotal { get; set; }
    /// <summary>Comma separated branch ids ("1,2"); null or empty = every branch.</summary>
    public string? BranchIds { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>When customers were told ("Ưu đãi mới"); set once, when the promotion first runs.</summary>
    public DateTime? NotifiedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
