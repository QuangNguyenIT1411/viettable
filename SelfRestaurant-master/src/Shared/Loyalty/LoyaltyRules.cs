namespace SelfRestaurant.Loyalty;

/// <summary>
/// The one loyalty rule set (owner, 26/10/2026), linked into Identity (earning), Billing (redeeming in the app and
/// at the cashier) and the Gateway (sent to the cashier web). The app and the cashier web never assume a rate:
/// they read <see cref="PointValueVnd"/> and <see cref="RedeemStep"/> from the API.
/// </summary>
internal static class LoyaltyRules
{
    /// <summary>Earning: 1 point for every 1.000đ actually paid, rounded down (245.000đ -> 245 points).</summary>
    public const int EarnVndPerPoint = 1000;

    /// <summary>Redeeming: 1 point takes 10đ off.</summary>
    public const int PointValueVnd = 10;

    /// <summary>Points are redeemed in whole steps of 1.000 points (= 10.000đ), in the app and at the cashier alike.</summary>
    public const int RedeemStep = 1000;

    /// <summary>Points may take at most 10% off a bill (owner, 26/10), in the app and at the cashier alike.</summary>
    public const int MaxDiscountPercent = 10;

    public static int PointsEarned(decimal amountPaid) =>
        amountPaid <= 0 ? 0 : (int)Math.Floor(amountPaid / EarnVndPerPoint);

    /// <summary>
    /// The points that may be used: never more than requested, than the balance, or than
    /// <see cref="MaxDiscountPercent"/> of the bill allows, always rounded down to a whole <see cref="RedeemStep"/>.
    /// A 245.000đ bill allows 24.500đ off = 2.450 points -> 2.000 points (20.000đ).
    /// </summary>
    public static int RedeemablePoints(int requested, int balance, decimal payable)
    {
        if (requested <= 0 || balance <= 0 || payable <= 0) return 0;
        var byBill = (int)Math.Min(int.MaxValue, Math.Floor(MaxDiscount(payable) / PointValueVnd));
        var points = Math.Min(requested, Math.Min(balance, byBill));
        return points / RedeemStep * RedeemStep;
    }

    /// <summary>Points actually taken from the balance at settlement: whole steps, never more than the balance.</summary>
    public static int SettledPoints(int requested, int balance) =>
        requested <= 0 || balance <= 0 ? 0 : Math.Min(requested, balance) / RedeemStep * RedeemStep;

    public static decimal Discount(int points) => points <= 0 ? 0 : points * (decimal)PointValueVnd;

    /// <summary>The most a bill may be reduced by points: <see cref="MaxDiscountPercent"/> of it, rounded down to a dong.</summary>
    public static decimal MaxDiscount(decimal payable) =>
        payable <= 0 ? 0 : Math.Floor(payable * MaxDiscountPercent / 100m);
}
