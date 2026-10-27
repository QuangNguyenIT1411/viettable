using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Promotions (owner 27/10/2026, docs/PROMO_PLAN.md): one formula for app and counter — subtotal → best promotion OR
/// voucher (the better, never both) → points (10% of what is left) → deposit.
/// </summary>
public sealed class PromotionPricingTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 5, 0, 0, DateTimeKind.Utc);

    // ---- Discount of one promotion ----

    [Fact]
    public void Percent_takes_the_share_of_the_subtotal()
    {
        Assert.Equal(50_000m, CheckoutPricing.Discount(Percent(10), 500_000m));
        Assert.Equal(33_333m, CheckoutPricing.Discount(Percent(10), 333_333m)); // rounded down
    }

    [Fact]
    public void Percent_stops_at_its_cap()
    {
        Assert.Equal(50_000m, CheckoutPricing.Discount(Percent(10, cap: 50_000m), 1_000_000m));
        Assert.Equal(30_000m, CheckoutPricing.Discount(Percent(10, cap: 50_000m), 300_000m));
    }

    [Fact]
    public void Fixed_amount_never_exceeds_the_bill()
    {
        Assert.Equal(30_000m, CheckoutPricing.Discount(Fixed(30_000m), 200_000m));
        Assert.Equal(20_000m, CheckoutPricing.Discount(Fixed(30_000m), 20_000m));
    }

    // ---- Who qualifies ----

    [Fact]
    public void Minimum_bill_is_required()
    {
        var offer = Percent(10, min: 300_000m);
        Assert.False(CheckoutPricing.Eligible(offer, 1, 299_999m, Now));
        Assert.True(CheckoutPricing.Eligible(offer, 1, 300_000m, Now));
    }

    [Fact]
    public void Only_while_on_and_between_start_and_end()
    {
        Assert.False(CheckoutPricing.Eligible(Percent(10, ends: Now.AddSeconds(-1)), 1, 500_000m, Now));   // expired
        Assert.False(CheckoutPricing.Eligible(Percent(10, starts: Now.AddMinutes(1)), 1, 500_000m, Now));  // not yet
        Assert.False(CheckoutPricing.Eligible(Percent(10, active: false), 1, 500_000m, Now));              // switched off
        Assert.True(CheckoutPricing.Eligible(Percent(10, ends: Now), 1, 500_000m, Now));                   // last moment
    }

    [Fact]
    public void Branch_list_limits_where_it_applies()
    {
        Assert.False(CheckoutPricing.Eligible(Percent(10, branches: [2]), 1, 500_000m, Now));
        Assert.True(CheckoutPricing.Eligible(Percent(10, branches: [1, 2]), 1, 500_000m, Now));
        Assert.True(CheckoutPricing.Eligible(Percent(10), 7, 500_000m, Now)); // no list = every branch
    }

    [Fact]
    public void Best_single_promotion_wins_and_ties_take_the_one_ending_first()
    {
        var small = Fixed(20_000m, id: 1);
        var big = Percent(10, id: 2);                       // 50.000đ on 500.000đ
        var wrongBranch = Fixed(90_000m, id: 3, branches: [9]);
        var best = CheckoutPricing.Best([small, big, wrongBranch], 1, 500_000m, Now)!.Value;
        Assert.Equal(2, best.Offer.PromotionId);
        Assert.Equal(50_000m, best.Discount);
        var later = Fixed(50_000m, id: 4, ends: Now.AddDays(9));
        var sooner = Fixed(50_000m, id: 5, ends: Now.AddDays(1));
        Assert.Equal(5, CheckoutPricing.Best([later, sooner], 1, 500_000m, Now)!.Value.Offer.PromotionId);
        Assert.Null(CheckoutPricing.Best([wrongBranch], 1, 500_000m, Now));
    }

    // ---- Against a voucher: the better one, never both ----

    [Fact]
    public void Voucher_better_than_the_promotion_is_used_and_said()
    {
        var r = CheckoutPricing.Compute(Input(500_000m, promo: 30_000m, voucher: 50_000m));
        Assert.Equal(0m, r.PromotionDiscount);
        Assert.Equal(50_000m, r.VoucherDiscount);
        Assert.Null(r.PromotionId);
        Assert.Contains("Áp dụng voucher", r.DiscountNote);
        Assert.Contains("không cộng dồn", r.DiscountNote);
        Assert.Equal(450_000m, r.Total);
    }

    [Fact]
    public void Promotion_better_or_equal_is_used_and_said()
    {
        var better = CheckoutPricing.Compute(Input(500_000m, promo: 60_000m, voucher: 50_000m));
        Assert.Equal(60_000m, better.PromotionDiscount);
        Assert.Equal(0m, better.VoucherDiscount);
        Assert.Contains("Áp dụng khuyến mãi \"Giờ vàng\"", better.DiscountNote);
        var tie = CheckoutPricing.Compute(Input(500_000m, promo: 50_000m, voucher: 50_000m));
        Assert.Equal(50_000m, tie.PromotionDiscount);
        Assert.Equal(0m, tie.VoucherDiscount);
        var alone = CheckoutPricing.Compute(Input(500_000m, promo: 60_000m));
        Assert.Null(alone.DiscountNote); // nothing to compare: no note
    }

    // ---- Order: discount → points (10% of what is left) → deposit ----

    [Fact]
    public void Points_cap_is_ten_percent_of_the_amount_after_the_promotion()
    {
        // 1.000.000đ − 100.000đ promotion = 900.000đ; points at most 10% = 90.000đ = 9.000 points (steps of 1.000).
        var r = CheckoutPricing.Compute(Input(1_000_000m, promo: 100_000m, points: 20_000, balance: 20_000));
        Assert.Equal(9_000, r.PointsApplied);
        Assert.Equal(90_000m, r.PointsDiscount);
        Assert.Equal(810_000m, r.Total);
    }

    [Fact]
    public void Deposit_comes_last_and_never_below_zero()
    {
        var r = CheckoutPricing.Compute(Input(1_000_000m, promo: 100_000m, points: 9_000, balance: 9_000, deposit: 300_000m));
        Assert.Equal(300_000m, r.DepositApplied);
        Assert.Equal(510_000m, r.Total);
        var small = CheckoutPricing.Compute(Input(200_000m, promo: 50_000m, deposit: 300_000m));
        Assert.Equal(150_000m, small.DepositApplied);
        Assert.Equal(0m, small.Total);
    }

    [Fact]
    public void Quote_keeps_both_offers_so_removing_the_voucher_brings_the_promotion_back()
    {
        var quote = new CheckoutQuote { Subtotal = 500_000m, BranchId = 1, QuoteCode = "Q", DiningSessionCode = "S", IdempotencyKey = "k", RequestHash = "h" };
        CheckoutPricing.AttachPromotion(quote, [Percent(10, cap: 40_000m)], Now);
        CheckoutPricing.Apply(quote);
        Assert.Equal(40_000m, quote.PromotionDiscount);
        Assert.Equal(460_000m, quote.TotalAmount);
        quote.VoucherId = 3; quote.VoucherOfferDiscount = 70_000m;      // guest picks a better voucher
        CheckoutPricing.Apply(quote);
        Assert.Equal(0m, quote.PromotionDiscount);
        Assert.Equal(70_000m, quote.VoucherDiscount);
        Assert.NotNull(quote.DiscountNote);
        quote.VoucherId = null; quote.VoucherOfferDiscount = 0;         // removes it again
        CheckoutPricing.Apply(quote);
        Assert.Equal(40_000m, quote.PromotionDiscount);
        Assert.Equal(0m, quote.VoucherDiscount);
        Assert.Null(quote.DiscountNote);
    }

    // ---- Admin endpoints (Billing, in-memory) ----

    [Fact]
    public async Task Invalid_promotions_are_refused_in_Vietnamese()
    {
        var controller = new InternalPromotionsController(Db());
        var tooMuch = await controller.Create(Request(type: "PERCENT", value: 120), default);
        Assert.Contains("100%", Json(Assert.IsType<BadRequestObjectResult>(tooMuch.Result).Value));
        var backwards = await controller.Create(Request(starts: Now, ends: Now.AddDays(-1)), default);
        Assert.Contains("kết thúc", Json(Assert.IsType<BadRequestObjectResult>(backwards.Result).Value));
        var http = await controller.Create(Request(image: "http://example.com/a.png"), default);
        Assert.Contains("https", Json(Assert.IsType<BadRequestObjectResult>(http.Result).Value));
    }

    [Fact]
    public async Task Running_list_follows_branch_time_and_switch_and_is_announced_once()
    {
        var db = Db();
        var controller = new InternalPromotionsController(db);
        var nowUtc = DateTime.UtcNow;
        await controller.Create(Request(name: "Quận 1", branches: [1], starts: nowUtc.AddHours(-1), ends: nowUtc.AddDays(1)), default);
        await controller.Create(Request(name: "Mọi chi nhánh", starts: nowUtc.AddHours(-1), ends: nowUtc.AddDays(1)), default);
        await controller.Create(Request(name: "Tuần sau", starts: nowUtc.AddDays(7), ends: nowUtc.AddDays(8)), default);
        var off = await controller.Create(Request(name: "Đã tắt", starts: nowUtc.AddHours(-1), ends: nowUtc.AddDays(1)), default);
        await controller.Toggle(IdOf(off), new InternalPromotionsController.ToggleRequest(false), default);

        Assert.Equal(["Quận 1", "Mọi chi nhánh"], Names(await controller.Active(1, default)));
        Assert.Equal(["Mọi chi nhánh"], Names(await controller.Active(2, default)));

        var due = Names(await controller.DueAnnouncements(default));
        Assert.Equal(2, due.Length);
        foreach (var p in await db.Promotions.Where(x => x.NotifiedAtUtc == null && x.IsActive && x.StartsAtUtc <= nowUtc).ToListAsync())
            await controller.MarkAnnounced(p.PromotionId, default);
        Assert.Empty(Names(await controller.DueAnnouncements(default)));
    }

    [Fact]
    public void Summary_reads_like_the_app_shows_it()
    {
        Assert.Equal("Giảm 10% (tối đa 50.000đ) cho hóa đơn từ 300.000đ", InternalPromotionsController.Summary(new Promotion
            { Name = "x", DiscountType = "PERCENT", DiscountValue = 10, MaxDiscount = 50_000m, MinSubtotal = 300_000m }));
        Assert.Equal("Giảm 30.000đ cho mọi hóa đơn", InternalPromotionsController.Summary(new Promotion
            { Name = "x", DiscountType = "FIXED", DiscountValue = 30_000m }));
    }

    [Fact]
    public async Task Announcement_reaches_each_customer_once()
    {
        var db = new SelfRestaurant.Customers.Api.Persistence.CustomersDbContext(
            new DbContextOptionsBuilder<SelfRestaurant.Customers.Api.Persistence.CustomersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new SelfRestaurant.Customers.Api.Controllers.MobileEngagementController(db);
        var request = new SelfRestaurant.Customers.Api.Controllers.MobileEngagementController.BroadcastRequest(
            [7, 8, 8], "promotion.created.v1", 12, "Giờ vàng: Giảm 10%.");
        Assert.Contains("\"created\":2", Json(Assert.IsType<OkObjectResult>((await controller.Broadcast(request, default)).Result).Value));
        Assert.Contains("\"created\":0", Json(Assert.IsType<OkObjectResult>((await controller.Broadcast(request, default)).Result).Value));
        Assert.Equal("offer", SelfRestaurant.Customers.Api.Infrastructure.CustomerInbox.Category("promotion.created.v1"));
        Assert.Equal("Ưu đãi mới", SelfRestaurant.Customers.Api.Infrastructure.CustomerInbox.Title("promotion.created.v1"));
    }

    // ---- helpers ----

    private static PricingInput Input(decimal subtotal, decimal promo = 0, decimal voucher = 0, int points = 0, int balance = 0, decimal deposit = 0) =>
        new(subtotal, promo > 0 ? 12 : null, promo > 0 ? "Giờ vàng" : null, promo, voucher, points, balance, deposit);

    private static PromotionOffer Percent(decimal value, decimal? cap = null, decimal min = 0, int id = 1, int[]? branches = null,
        DateTime? starts = null, DateTime? ends = null, bool active = true) =>
        new(id, "Giờ vàng", "PERCENT", value, cap, min, (branches ?? []).ToHashSet(), starts ?? Now.AddDays(-1), ends ?? Now.AddDays(3), active);

    private static PromotionOffer Fixed(decimal value, int id = 1, int[]? branches = null, DateTime? ends = null) =>
        new(id, "Giảm thẳng", "FIXED", value, null, 0, (branches ?? []).ToHashSet(), Now.AddDays(-1), ends ?? Now.AddDays(3), true);

    private static InternalPromotionsController.PromotionRequest Request(string name = "Giờ vàng", string type = "PERCENT", decimal value = 10,
        int[]? branches = null, DateTime? starts = null, DateTime? ends = null, string? image = null) =>
        new(name, "Mô tả", image, type, value, 50_000m, 0, branches, starts ?? Now, ends ?? Now.AddDays(3));

    private static BillingDbContext Db() =>
        new(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string Json(object? value) => JsonSerializer.Serialize(value, new JsonSerializerOptions
        { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static int IdOf(ActionResult<object> result) =>
        JsonDocument.Parse(Json(Assert.IsType<OkObjectResult>(result.Result).Value)).RootElement.GetProperty("PromotionId").GetInt32();

    private static string[] Names(ActionResult<object> result) =>
        JsonDocument.Parse(Json(Assert.IsType<OkObjectResult>(result.Result).Value)).RootElement.EnumerateArray()
            .Select(x => x.GetProperty("Name").GetString()!).ToArray();
}
