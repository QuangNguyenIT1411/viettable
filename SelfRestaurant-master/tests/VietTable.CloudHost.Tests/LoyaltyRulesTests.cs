using SelfRestaurant.Loyalty;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>The loyalty rules chosen by the owner on 26/10/2026, shared by Identity, Billing (app + cashier) and the Gateway.</summary>
public sealed class LoyaltyRulesTests
{
    [Theory]
    [InlineData(245_000, 245)]   // 1 point per 1.000đ paid
    [InlineData(999, 0)]         // rounded down
    [InlineData(1_000, 1)]
    [InlineData(1_999.99, 1)]
    [InlineData(0, 0)]
    [InlineData(-5_000, 0)]
    public void Earning_is_one_point_per_1000_dong_paid(decimal amountPaid, int points) =>
        Assert.Equal(points, LoyaltyRules.PointsEarned(amountPaid));

    [Fact]
    public void Redeeming_1000_points_takes_10000_dong_off()
    {
        Assert.Equal(10_000m, LoyaltyRules.Discount(1_000));
        Assert.Equal(10, LoyaltyRules.PointValueVnd);
        Assert.Equal(1_000, LoyaltyRules.RedeemStep);
    }

    [Fact]
    public void Redeeming_goes_in_whole_steps_of_1000_points()
    {
        Assert.Equal(1_000, LoyaltyRules.RedeemablePoints(requested: 1_500, balance: 5_000, payable: 500_000));
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 500, balance: 5_000, payable: 500_000));
    }

    [Fact]
    public void Redeeming_never_exceeds_the_balance()
    {
        Assert.Equal(2_000, LoyaltyRules.RedeemablePoints(requested: 9_000, balance: 2_400, payable: 500_000));
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 0, payable: 500_000));
    }

    [Fact]
    public void Redeeming_takes_at_most_10_percent_of_the_bill()
    {
        // 245.000đ -> at most 24.500đ off -> 2.450 points -> 2.000 in whole steps (20.000đ).
        Assert.Equal(24_500m, LoyaltyRules.MaxDiscount(245_000));
        var points = LoyaltyRules.RedeemablePoints(requested: 10_000, balance: 10_000, payable: 245_000);
        Assert.Equal(2_000, points);
        Assert.Equal(20_000m, LoyaltyRules.Discount(points));
        Assert.Equal(10, LoyaltyRules.MaxDiscountPercent);
    }

    [Fact]
    public void Redeeming_never_exceeds_the_bill()
    {
        // 10% of a 99.999đ bill is 9.999đ = 999 points: not even one step.
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 10_000, payable: 99_999));
        Assert.Equal(1_000, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 10_000, payable: 100_000));
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 10_000, payable: 0));
        // Never more off than the bill itself, whatever is requested.
        foreach (var payable in new[] { 1m, 50_000m, 245_000m, 9_999_999m })
            Assert.True(LoyaltyRules.Discount(LoyaltyRules.RedeemablePoints(int.MaxValue, int.MaxValue, payable)) <= payable);
    }

    [Fact]
    public void Settlement_takes_whole_steps_and_never_more_than_the_balance()
    {
        Assert.Equal(2_000, LoyaltyRules.SettledPoints(requested: 2_000, balance: 5_000));
        Assert.Equal(1_000, LoyaltyRules.SettledPoints(requested: 3_000, balance: 1_800));
        Assert.Equal(0, LoyaltyRules.SettledPoints(requested: 500, balance: 5_000));
    }

    [Fact]
    public void Every_service_uses_the_shared_rules_and_no_old_rate_is_left()
    {
        var root = FindRepoRoot();
        string Read(string relative) => File.ReadAllText(Path.Combine(root, relative));
        var identity = Read("src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs");
        var mobile = Read("src/Services/SelfRestaurant.Billing.Api/Controllers/MobileCheckoutController.cs");
        var cashier = Read("src/Services/SelfRestaurant.Billing.Api/Controllers/CashierController.cs");
        var profile = Read("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileProfileController.cs");
        // Since the promotions release both checkouts price through the one formula, which uses the shared rules.
        var pricing = Read("src/Services/SelfRestaurant.Billing.Api/Infrastructure/Payments/CheckoutPricing.cs");
        Assert.Contains("LoyaltyRules.RedeemablePoints", pricing);
        Assert.Contains("LoyaltyRules.PointsEarned", identity);
        Assert.Contains("LoyaltyRules.SettledPoints", identity);
        Assert.DoesNotContain("0.01m", identity);
        Assert.Contains("CheckoutPricing.Apply(entity, customer.LoyaltyPoints)", mobile);
        Assert.Contains("pointValueVnd = SelfRestaurant.Loyalty.LoyaltyRules.PointValueVnd", mobile);
        Assert.Contains("maxPointsDiscountPercent = SelfRestaurant.Loyalty.LoyaltyRules.MaxDiscountPercent", mobile);
        Assert.DoesNotContain("* 1000m", mobile);
        Assert.Contains("CheckoutPricing.Compute(", cashier);
        Assert.DoesNotContain("0.10m", cashier);
        Assert.DoesNotContain("5%", profile);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Shared", "Loyalty"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
