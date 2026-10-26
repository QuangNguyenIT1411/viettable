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
    public void Redeeming_never_exceeds_the_bill()
    {
        // A 25.000đ bill can absorb at most 2.500 points -> 2.000 in whole steps (20.000đ off), never a negative total.
        var points = LoyaltyRules.RedeemablePoints(requested: 10_000, balance: 10_000, payable: 25_000);
        Assert.Equal(2_000, points);
        Assert.True(LoyaltyRules.Discount(points) <= 25_000m);
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 10_000, payable: 9_999));
        Assert.Equal(0, LoyaltyRules.RedeemablePoints(requested: 1_000, balance: 10_000, payable: 0));
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
        Assert.Contains("LoyaltyRules.PointsEarned", identity);
        Assert.Contains("LoyaltyRules.SettledPoints", identity);
        Assert.DoesNotContain("0.01m", identity);
        Assert.Contains("LoyaltyRules.RedeemablePoints", mobile);
        Assert.Contains("pointValueVnd = SelfRestaurant.Loyalty.LoyaltyRules.PointValueVnd", mobile);
        Assert.DoesNotContain("* 1000m", mobile);
        Assert.Contains("LoyaltyRules.RedeemablePoints", cashier);
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
