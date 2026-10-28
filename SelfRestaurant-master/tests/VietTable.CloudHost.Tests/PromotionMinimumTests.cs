using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Owner 29/10/2026: "blackday" (50%, minimum 300.000đ) showed −17.500đ on a 35.000đ bill at the counter. The minimum
/// must hold everywhere (app, cashier screen, checkout), the counter screen is priced by the server, and a money box
/// cannot save "300.000" as 300đ again.
/// </summary>
public sealed class PromotionMinimumTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 5, 0, 0, DateTimeKind.Utc);

    // ---- The rule itself ----

    [Fact]
    public void Bill_of_35000_under_a_300000_minimum_gets_nothing()
    {
        var blackday = Offer(50, min: 300_000m);
        Assert.False(CheckoutPricing.Eligible(blackday, 1, 35_000m, Now));
        Assert.Null(CheckoutPricing.Best([blackday], 1, 35_000m, Now));
        var quote = Quote(35_000m);
        CheckoutPricing.AttachPromotion(quote, [blackday], Now);
        CheckoutPricing.Apply(quote);
        Assert.Null(quote.PromotionId);
        Assert.Equal(0m, quote.PromotionDiscount);
        Assert.Equal(35_000m, quote.TotalAmount);
    }

    [Fact]
    public void Bill_reaching_the_minimum_gets_the_discount()
    {
        var blackday = Offer(50, min: 300_000m);
        var best = CheckoutPricing.Best([blackday], 1, 300_000m, Now)!.Value;
        Assert.Equal(150_000m, best.Discount);
        Assert.Equal(150_000m, CheckoutPricing.Compute(new PricingInput(300_000m, 1, "blackday", best.Discount, 0, 0, 0, 0)).Total);
    }

    [Fact]
    public void Discount_stops_at_the_cap()
    {
        var capped = Offer(50, min: 300_000m, cap: 50_000m);
        Assert.Equal(50_000m, CheckoutPricing.Best([capped], 1, 300_000m, Now)!.Value.Discount);
        Assert.Equal(50_000m, CheckoutPricing.Best([capped], 1, 2_000_000m, Now)!.Value.Discount);
        Assert.Equal(45_000m, CheckoutPricing.Best([Offer(15, min: 300_000m, cap: 50_000m)], 1, 300_000m, Now)!.Value.Discount);
    }

    [Fact]
    public void The_bug_reproduced_a_300d_minimum_lets_a_35000d_bill_through()
    {
        // What "300.000" in a number box saved: a 300đ minimum. Exactly the −17.500đ seen at the counter.
        var typo = Offer(50, min: 300m);
        Assert.Equal(17_500m, CheckoutPricing.Best([typo], 1, 35_000m, Now)!.Value.Discount);
    }

    // ---- The admin cannot save it again ----

    [Fact]
    public void Money_under_1000d_is_refused_zero_and_full_amounts_pass()
    {
        Assert.Null(CheckoutPricing.MoneyError("Đơn tối thiểu", 0));
        Assert.Null(CheckoutPricing.MoneyError("Đơn tối thiểu", null));
        Assert.Null(CheckoutPricing.MoneyError("Đơn tối thiểu", 300_000m));
        Assert.Contains("1.000đ", CheckoutPricing.MoneyError("Đơn tối thiểu", 300m));
        Assert.Contains("chẵn", CheckoutPricing.MoneyError("Đơn tối thiểu", 300_000.5m));
    }

    [Fact]
    public async Task Promotion_with_a_300d_minimum_is_refused_and_300000d_is_saved_as_typed()
    {
        var db = Db();
        var controller = new InternalPromotionsController(db);
        var typo = await controller.Create(Promotion(min: 300m), default);
        Assert.Contains("Đơn tối thiểu", Json(Assert.IsType<BadRequestObjectResult>(typo.Result).Value));
        var smallCap = await controller.Create(Promotion(min: 300_000m, cap: 50m), default);
        Assert.Contains("Trần giảm", Json(Assert.IsType<BadRequestObjectResult>(smallCap.Result).Value));
        Assert.IsType<OkObjectResult>((await controller.Create(Promotion(min: 300_000m), default)).Result);
        var saved = await db.Promotions.SingleAsync();
        Assert.Equal(300_000m, saved.MinSubtotal);
        Assert.Equal(50m, saved.DiscountValue);
        Assert.Equal("PERCENT", saved.DiscountType);
    }

    [Fact]
    public async Task Fixed_voucher_of_50d_is_refused()
    {
        var controller = new InternalVouchersController(Db());
        var result = await controller.Create(new InternalVouchersController.VoucherAdminRequest("GIAM50", "Giảm 50k", "FIXED", 50m, null, 0,
            Now, Now.AddDays(3), 1, null), default);
        Assert.Contains("Số tiền giảm", Json(Assert.IsType<BadRequestObjectResult>(result.Result).Value));
    }

    // ---- The counter screen is priced by the server ----

    [Fact]
    public async Task Counter_preview_of_35000d_under_the_minimum_shows_no_promotion()
    {
        var db = Db();
        db.Promotions.Add(Running("blackday", 50, min: 300_000m));
        await db.SaveChangesAsync();
        var preview = await Preview(db, subtotal: 35_000m);
        Assert.Equal(0m, preview.PromotionOffer);
        Assert.Equal(0m, preview.PromotionDiscount);
        Assert.Null(preview.PromotionName);
        Assert.Equal(35_000m, preview.TotalAmount);
        Assert.False(preview.AppSession);
    }

    [Fact]
    public async Task Counter_preview_at_the_minimum_applies_the_capped_discount_and_the_better_of_rule()
    {
        var db = Db();
        db.Promotions.Add(Running("blackday", 50, min: 300_000m, cap: 50_000m));
        await db.SaveChangesAsync();
        var promo = await Preview(db, subtotal: 300_000m);
        Assert.Equal("blackday", promo.PromotionName);
        Assert.Equal(50_000m, promo.PromotionDiscount);
        Assert.Equal(250_000m, promo.TotalAmount);

        var manual = await Preview(db, subtotal: 300_000m, discount: 80_000m); // the cashier's discount is better
        Assert.Equal(50_000m, manual.PromotionOffer);
        Assert.Equal(0m, manual.PromotionDiscount);
        Assert.Equal(80_000m, manual.Discount);
        Assert.Contains("không cộng dồn", manual.DiscountNote);
        Assert.Equal(220_000m, manual.TotalAmount);
    }

    [Fact]
    public async Task Counter_preview_takes_points_only_up_to_ten_percent_after_the_promotion()
    {
        var db = Db();
        db.Promotions.Add(Running("blackday", 50, min: 300_000m, cap: 100_000m));
        await db.SaveChangesAsync();
        // 1.000.000đ − 100.000đ = 900.000đ; 10% = 90.000đ = 9.000 points, whatever the screen asks for.
        var preview = await Preview(db, subtotal: 1_000_000m, points: 50_000, balance: 50_000);
        Assert.Equal(100_000m, preview.PromotionDiscount);
        Assert.Equal(9_000, preview.PointsUsed);
        Assert.Equal(9_000, preview.MaxPoints);
        Assert.Equal(810_000m, preview.TotalAmount);
    }

    // ---- helpers ----

    private static async Task<CashierController.CheckoutPreviewResponse> Preview(BillingDbContext db, decimal subtotal,
        decimal discount = 0, int points = 0, int balance = 0)
    {
        var order = JsonSerializer.Serialize(new
        {
            orderId = 60, orderCode = "ORD60", diningSessionCode = (string?)null, tableId = 2, tableName = "Bàn 2", branchId = 1,
            branchName = "Quận 1", customerId = balance > 0 ? 7 : (int?)null, statusCode = "SERVING", statusName = "Đang phục vụ",
            isActive = true, subtotal,
        });
        var customer = JsonSerializer.Serialize(new { customerId = 7, username = "khach", name = "Khách", loyaltyPoints = balance });
        var controller = new CashierController(db,
            new CustomersApiClient(new HttpClient(new Stub(customer)) { BaseAddress = new Uri("http://customers.invalid") }),
            new OrdersApiClient(new HttpClient(new Stub(order)) { BaseAddress = new Uri("http://orders.invalid") }, db),
            null!, null!, null!, null!);
        var result = await controller.PreviewCheckout(60, new CashierController.CheckoutPreviewRequest(discount, points, 1), default);
        return Assert.IsType<CashierController.CheckoutPreviewResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static PromotionOffer Offer(decimal percent, decimal min, decimal? cap = null) =>
        new(1, "blackday", "PERCENT", percent, cap, min, new HashSet<int>(), Now.AddDays(-1), Now.AddDays(3), true);

    private static Promotion Running(string name, decimal percent, decimal min, decimal? cap = null) => new()
    {
        Name = name, DiscountType = "PERCENT", DiscountValue = percent, MaxDiscount = cap, MinSubtotal = min, IsActive = true,
        StartsAtUtc = DateTime.UtcNow.AddHours(-1), EndsAtUtc = DateTime.UtcNow.AddDays(1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
    };

    private static InternalPromotionsController.PromotionRequest Promotion(decimal min, decimal? cap = null) =>
        new("blackday", null, null, "PERCENT", 50, cap, min, [1], Now, Now.AddDays(3));

    private static CheckoutQuote Quote(decimal subtotal) =>
        new() { Subtotal = subtotal, BranchId = 1, QuoteCode = "Q", DiningSessionCode = "S", IdempotencyKey = "k", RequestHash = "h" };

    private static BillingDbContext Db() =>
        new(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string Json(object? value) => JsonSerializer.Serialize(value, new JsonSerializerOptions
        { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private sealed class Stub(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
