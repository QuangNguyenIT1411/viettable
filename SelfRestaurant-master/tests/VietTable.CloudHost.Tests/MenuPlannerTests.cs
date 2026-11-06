using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Catalog.Api.Controllers;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11 "Gợi ý món": tagged dishes only, everyone fed, vegetarians served, never over the budget.</summary>
public sealed class MenuPlannerTests
{
    private static MenuPlanner.Dish D(int id, string name, decimal price, string? course, bool veg = false, bool? spicy = false, int? servings = null) =>
        new(id, name, price, course, veg, spicy, servings);

    private static readonly MenuPlanner.Dish[] Menu =
    {
        D(1, "Bún chả", 65_000, "MAIN"), D(2, "Phở bò", 75_000, "MAIN"), D(3, "Cơm tấm", 55_000, "MAIN"),
        D(4, "Bò lúc lắc", 120_000, "MAIN", spicy: true), D(5, "Đậu hũ sốt cà", 50_000, "MAIN", veg: true),
        D(6, "Rau xào chay", 45_000, "MAIN", veg: true), D(7, "Lẩu thái", 320_000, "MAIN", spicy: true, servings: 4),
        D(8, "Gỏi cuốn", 40_000, "STARTER"), D(9, "Canh chua", 60_000, "SOUP"), D(10, "Trà đá", 5_000, "DRINK"),
        D(11, "Chè khúc bạch", 35_000, "DESSERT"), D(12, "Món chưa gắn", 10_000, null),
    };

    private static int Servings(MenuPlanner.Plan plan, bool? vegOnly = null) => plan.Lines
        .Where(l => l.Dish.Course == "MAIN" && (vegOnly is null || l.ForVegetarians == vegOnly))
        .Sum(l => Math.Max(1, l.Dish.Servings ?? 1) * l.Quantity);

    [Fact]
    public void Four_people_800k_one_vegetarian()
    {
        var plan = MenuPlanner.Suggest(Menu, 4, 800_000, 1, false, 0, 0);
        Assert.True(plan.Feasible);
        Assert.True(Servings(plan) >= 4);
        Assert.True(Servings(plan, vegOnly: true) >= 1);
        Assert.All(plan.Lines.Where(l => l.ForVegetarians), l => Assert.True(l.Dish.IsVegetarian));
        Assert.True(plan.Total <= 800_000);
        Assert.Contains(plan.Lines, l => l.Dish.Course == "STARTER");
        Assert.Contains(plan.Lines, l => l.Dish.Course == "DRINK" && l.Quantity == 4);
        Assert.DoesNotContain(plan.Lines, l => l.Dish.DishId == 12); // not tagged → never suggested
        Assert.Contains(plan.Notes, n => n.Contains("chưa được gắn loại món"));
    }

    [Theory]
    [InlineData(2, 300_000, 0, 0)]
    [InlineData(4, 800_000, 5, 8)]
    [InlineData(6, 1_000_000, 10, 10)]
    [InlineData(10, 2_500_000, 5, 8)]
    public void The_total_with_service_fee_and_vat_never_passes_the_budget(int people, decimal budget, decimal fee, decimal vat)
    {
        var plan = MenuPlanner.Suggest(Menu, people, budget, 0, false, fee, vat);
        Assert.True(plan.Feasible);
        Assert.True(plan.Total <= budget, $"{plan.Total} > {budget}");
        Assert.True(Servings(plan) >= people);
    }

    [Fact]
    public void A_budget_too_small_says_how_much_is_needed()
    {
        var plan = MenuPlanner.Suggest(Menu, 6, 150_000, 0, false, 5, 8);
        Assert.False(plan.Feasible);
        Assert.NotNull(plan.MinimumTotal);
        Assert.True(plan.MinimumTotal > 150_000);
        Assert.StartsWith("Ngân sách chưa đủ cho 6 người", plan.Notes[0]);
    }

    [Fact]
    public void Avoid_spicy_uses_only_dishes_marked_not_spicy()
    {
        var menu = Menu.Append(D(13, "Cá kho (chưa gắn cay)", 70_000, "MAIN", spicy: null)).ToArray();
        var plan = MenuPlanner.Suggest(menu, 4, 1_000_000, 0, true, 0, 0);
        Assert.All(plan.Lines, l => Assert.Equal(false, l.Dish.IsSpicy));
    }

    [Fact]
    public void A_dish_for_four_counts_four_servings()
    {
        var menu = new[] { D(7, "Lẩu thái", 320_000, "MAIN", servings: 4) };
        var plan = MenuPlanner.Suggest(menu, 4, 400_000, 0, false, 0, 0);
        Assert.Single(plan.Lines);
        Assert.Equal(1, plan.Lines[0].Quantity);
    }

    [Fact]
    public void Three_people_never_get_a_dish_for_four_when_single_dishes_fit()
    {
        // The 06/11 Samsung case: 4 people, 1 vegetarian → 3 left; it chose Bò né + a hotpot for 4.
        var menu = new[]
        {
            D(1, "Bò né", 85_000, "MAIN"), D(2, "Phở bò", 75_000, "MAIN"), D(3, "Cơm bò lúc lắc", 75_000, "MAIN"),
            D(4, "Lẩu thái", 250_000, "MAIN", servings: 4), D(5, "Đậu hũ sốt cà chua", 55_000, "MAIN", veg: true),
        };
        var plan = MenuPlanner.Suggest(menu, 4, 800_000, 1, false, 0, 0);
        Assert.DoesNotContain(plan.Lines, l => l.Dish.DishId == 4);
        Assert.Equal(3, Servings(plan, vegOnly: false));
        Assert.Equal(1, Servings(plan, vegOnly: true));
        foreach (var people in new[] { 1, 2, 3 })
            Assert.DoesNotContain(MenuPlanner.Suggest(menu, people, 800_000, 0, false, 0, 0).Lines, l => l.Dish.DishId == 4);
    }

    [Fact]
    public void A_dish_for_more_people_is_used_only_when_nothing_smaller_exists()
    {
        var plan = MenuPlanner.Suggest(new[] { D(4, "Lẩu thái", 250_000, "MAIN", servings: 4) }, 3, 400_000, 0, false, 0, 0);
        Assert.Single(plan.Lines);
        Assert.Equal(4, plan.Lines[0].Dish.DishId);
    }

    [Fact]
    public void Without_tagged_vegetarian_mains_it_says_so_instead_of_guessing()
    {
        var menu = Menu.Where(x => !x.IsVegetarian).ToArray();
        var plan = MenuPlanner.Suggest(menu, 3, 600_000, 1, false, 0, 0);
        Assert.Contains("Chưa có món chính nào được gắn là món chay.", plan.Notes);
        Assert.DoesNotContain(plan.Lines, l => l.ForVegetarians);
    }

    [Fact]
    public void Swaps_stay_in_the_budget_and_keep_vegetarian_lines_vegetarian()
    {
        var plan = MenuPlanner.Suggest(Menu, 4, 800_000, 1, false, 5, 8);
        foreach (var line in plan.Lines)
            foreach (var alt in MenuPlanner.Alternatives(Menu, plan, line, 800_000, false, 5, 8))
            {
                Assert.Equal(line.Dish.Course, alt.Course);
                if (line.ForVegetarians) Assert.True(alt.IsVegetarian);
                Assert.True(MenuPlanner.Total(plan.Subtotal - line.Dish.Price * line.Quantity + alt.Price * line.Quantity, 5, 8) <= 800_000);
            }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 8)]
    [InlineData(10, 10)]
    [InlineData(7.5, 8)]
    [InlineData(30, 20)]
    public void The_estimate_is_the_bill_formula(decimal fee, decimal vat)
    {
        foreach (var subtotal in new decimal[] { 0, 5_000, 45_000, 99_999, 275_000, 1_234_567, 9_999_995 })
        {
            var bill = CheckoutPricing.Compute(new PricingInput(subtotal, null, null, 0, 0, 0, 0, 0, ServiceFeePercent: fee, VatPercent: vat));
            Assert.Equal(bill.GrossTotal, MenuPlanner.Total(subtotal, fee, vat));
        }
    }

    [Theory]
    [InlineData("MAIN", null, true)]
    [InlineData("drink", 1, true)]
    [InlineData(null, null, true)]
    [InlineData("BREAKFAST", null, false)]
    [InlineData("MAIN", 0, false)]
    [InlineData("MAIN", 21, false)]
    public void Dish_attribute_checks(string? course, int? servings, bool ok)
    {
        Assert.Equal(ok, DishAttributesController.Validate(course, servings) is null);
    }
}
