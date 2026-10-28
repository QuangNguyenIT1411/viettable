using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Vouchers (owner 28/10/2026): uses per customer (1, N or none) and in total; a use is a bill paid or waiting for the
/// money, and cancelling the payment request gives it back. Never applied by itself; "Không dùng khuyến mãi".
/// </summary>
public sealed class VoucherLimitTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    // ---- Pure rules ----

    [Fact]
    public void One_time_voucher_is_usable_once_then_shows_used()
    {
        var voucher = Fixed(50_000m, perCustomer: 1);
        var fresh = VoucherRules.Check(voucher, Now, 400_000m, VoucherUse.None);
        Assert.True(fresh.Usable);
        Assert.Equal("Còn 1 lượt", VoucherRules.RemainingLabel(fresh));

        var used = VoucherRules.Check(voucher, Now, 400_000m, new VoucherUse(1, 1));
        Assert.False(used.Usable);
        Assert.Equal(VoucherRules.UsedUp, used.Code);
        Assert.Equal("Đã dùng", used.Reason);
    }

    [Fact]
    public void N_time_voucher_counts_down_per_customer()
    {
        var voucher = Fixed(20_000m, perCustomer: 3);
        Assert.Equal(1, VoucherRules.Check(voucher, Now, 100_000m, new VoucherUse(2, 9)).RemainingForCustomer);
        var done = VoucherRules.Check(voucher, Now, 100_000m, new VoucherUse(3, 9));
        Assert.False(done.Usable);
        Assert.Equal("Đã dùng hết 3 lượt", done.Reason);
        // Another customer is not affected by these three uses.
        Assert.True(VoucherRules.Check(voucher, Now, 100_000m, new VoucherUse(0, 9)).Usable);
    }

    [Fact]
    public void Unlimited_voucher_has_no_remaining_label()
    {
        var check = VoucherRules.Check(Fixed(20_000m), Now, 100_000m, new VoucherUse(40, 400));
        Assert.True(check.Usable);
        Assert.Null(check.RemainingForCustomer);
        Assert.Null(VoucherRules.RemainingLabel(check));
    }

    [Fact]
    public void Total_uses_run_out_for_everyone()
    {
        var voucher = Fixed(20_000m, total: 5);
        Assert.Equal(1, VoucherRules.Check(voucher, Now, 100_000m, new VoucherUse(0, 4)).RemainingTotal);
        var soldOut = VoucherRules.Check(voucher, Now, 100_000m, new VoucherUse(0, 5));
        Assert.False(soldOut.Usable);
        Assert.Equal(VoucherRules.SoldOut, soldOut.Code);
        Assert.Equal("Đã hết lượt", soldOut.Reason);
    }

    [Fact]
    public void Reasons_for_a_greyed_voucher()
    {
        var minimum = VoucherRules.Check(Fixed(50_000m, min: 300_000m), Now, 250_000m, VoucherUse.None);
        Assert.Equal(VoucherRules.MinimumNotMet, minimum.Code);
        Assert.Contains("300.000", minimum.Reason);
        Assert.Contains("50.000", minimum.Reason); // còn thiếu

        Assert.Equal(VoucherRules.Expired, VoucherRules.Check(Fixed(1m, ends: Now.AddMinutes(-1)), Now, 100m, VoucherUse.None).Code);
        Assert.Equal(VoucherRules.NotStarted, VoucherRules.Check(Fixed(1m, starts: Now.AddDays(1)), Now, 100m, VoucherUse.None).Code);
    }

    [Fact]
    public void Percent_voucher_respects_cap_and_bill()
    {
        var voucher = new Voucher { DiscountType = "PERCENT", DiscountValue = 10, MaxDiscount = 30_000m };
        Assert.Equal(30_000m, VoucherRules.Discount(voucher, 1_000_000m));
        Assert.Equal(12_345m, VoucherRules.Discount(voucher, 123_456m));
        Assert.Equal(10_000m, VoucherRules.Discount(Fixed(50_000m), 10_000m));
    }

    // ---- Counting in the database ----

    [Fact]
    public async Task Cancelling_the_payment_request_gives_the_use_back()
    {
        await using var db = Db();
        var voucher = Seed(db, Fixed(50_000m, perCustomer: 1));
        var quote = Quote(db, customerId: 7, voucher, status: "PAYMENT_REQUESTED");
        await db.SaveChangesAsync();
        Assert.Equal(new VoucherUse(1, 1), await VoucherUsage.CountAsync(db, voucher.VoucherId, 7, null, default));

        quote.Status = "CANCELLED"; // CancelPaymentRequest / cashier cancel
        await db.SaveChangesAsync();
        Assert.Equal(VoucherUse.None, await VoucherUsage.CountAsync(db, voucher.VoucherId, 7, null, default));
        Assert.True(VoucherRules.Check(voucher, Now, 400_000m, VoucherUse.None).Usable);
    }

    [Fact]
    public async Task Only_paid_or_waiting_bills_that_took_the_voucher_off_count()
    {
        await using var db = Db();
        var voucher = Seed(db, Fixed(50_000m));
        Quote(db, 1, voucher, "PAID");
        Quote(db, 1, voucher, "PAYMENT_REQUESTED");
        Quote(db, 1, voucher, "OPEN");                         // only picked, not paid
        Quote(db, 2, voucher, "EXPIRED");
        Quote(db, 2, voucher, "PAID", voucherDiscount: 0);      // promotion was better: voucher not used
        await db.SaveChangesAsync();
        Assert.Equal(new VoucherUse(2, 2), await VoucherUsage.CountAsync(db, voucher.VoucherId, 1, null, default));
        Assert.Equal(new VoucherUse(0, 2), await VoucherUsage.CountAsync(db, voucher.VoucherId, 2, null, default));
    }

    [Fact]
    public async Task Two_guests_racing_for_the_last_use_only_one_gets_it()
    {
        var name = Guid.NewGuid().ToString();
        int voucherId, first, second;
        await using (var setup = Db(name))
        {
            var voucher = Seed(setup, Fixed(50_000m, total: 3));
            Quote(setup, 1, voucher, "PAID");
            Quote(setup, 2, voucher, "PAID");                   // 2 of 3 used: one left
            var a = Quote(setup, 3, voucher, "OPEN");
            var b = Quote(setup, 4, voucher, "OPEN");
            await setup.SaveChangesAsync();
            voucherId = voucher.VoucherId; first = a.CheckoutQuoteId; second = b.CheckoutQuoteId;
        }
        // The per-voucher transaction lock of RequestPayment, here a semaphore: check and save happen inside it.
        var voucherLock = new SemaphoreSlim(1, 1);
        async Task<bool> RequestPayment(int quoteId)
        {
            await using var db = Db(name);
            await voucherLock.WaitAsync();
            try
            {
                var quote = await db.CheckoutQuotes.SingleAsync(x => x.CheckoutQuoteId == quoteId);
                if (await VoucherUsage.BlockingCheckAsync(db, quote, Now, default) is not null) return false;
                quote.Status = "PAYMENT_REQUESTED";
                await db.SaveChangesAsync();
                return true;
            }
            finally { voucherLock.Release(); }
        }
        var results = await Task.WhenAll(Task.Run(() => RequestPayment(first)), Task.Run(() => RequestPayment(second)));
        Assert.Single(results, x => x);
        await using var check = Db(name);
        Assert.Equal(3, (await VoucherUsage.CountAsync(check, voucherId, 0, null, default)).UsedTotal);
    }

    [Fact]
    public async Task Blocking_check_ignores_the_quote_itself()
    {
        await using var db = Db();
        var voucher = Seed(db, Fixed(50_000m, perCustomer: 1));
        var quote = Quote(db, 5, voucher, "PAYMENT_REQUESTED"); // a repeated request for the same quote
        await db.SaveChangesAsync();
        Assert.Null(await VoucherUsage.BlockingCheckAsync(db, quote, Now, default));
    }

    // ---- Checkout endpoints ----

    [Fact]
    public async Task Picking_a_used_up_voucher_is_refused_with_the_reason()
    {
        await using var db = Db();
        var voucher = Seed(db, Fixed(50_000m, perCustomer: 1));
        Quote(db, 9, voucher, "PAID");
        var open = Quote(db, 9, null, "OPEN");
        await db.SaveChangesAsync();
        var result = await Controller(db).ApplyVoucher(open.CheckoutQuoteId, new MobileCheckoutController.VoucherRequest(9, voucher.VoucherId), default);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(VoucherRules.UsedUp, Json(conflict.Value).GetProperty("code").GetString());
        Assert.Null((await db.CheckoutQuotes.SingleAsync(x => x.CheckoutQuoteId == open.CheckoutQuoteId)).VoucherId);
    }

    [Fact]
    public async Task Voucher_list_for_a_bill_shows_usable_first_and_greys_the_rest()
    {
        await using var db = Db();
        var good = Seed(db, Fixed(30_000m, perCustomer: 2, code: "GOOD"));
        var big = Seed(db, Fixed(80_000m, min: 500_000m, code: "BIG"));
        var once = Seed(db, Fixed(40_000m, perCustomer: 1, code: "ONCE"));
        Quote(db, 3, once, "PAID");
        var open = Quote(db, 3, null, "OPEN");
        await db.SaveChangesAsync();
        var ok = Assert.IsType<OkObjectResult>((await Controller(db).Vouchers(3, open.CheckoutQuoteId, default)).Result);
        var rows = Json(ok.Value).EnumerateArray().ToArray();
        Assert.Equal(new[] { "GOOD", "BIG", "ONCE" }.OrderBy(x => x), rows.Select(x => x.GetProperty("code").GetString()).OrderBy(x => x));
        Assert.Equal("GOOD", rows[0].GetProperty("code").GetString());
        Assert.True(rows[0].GetProperty("usable").GetBoolean());
        Assert.Equal("Còn 2 lượt", rows[0].GetProperty("remainingLabel").GetString());
        Assert.Equal(30_000, rows[0].GetProperty("discount").GetInt64());
        var greyed = rows.Skip(1).ToDictionary(x => x.GetProperty("code").GetString()!, x => x);
        Assert.False(greyed["BIG"].GetProperty("usable").GetBoolean());
        Assert.Equal(VoucherRules.MinimumNotMet, greyed["BIG"].GetProperty("reasonCode").GetString());
        Assert.Equal("Đã dùng", greyed["ONCE"].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Guest_can_decline_the_promotion_and_take_it_back()
    {
        await using var db = Db();
        var quote = Quote(db, 4, null, "OPEN");
        quote.PromotionId = 2; quote.PromotionName = "Giờ vàng"; quote.PromotionOfferDiscount = 40_000m;
        CheckoutPricing.Apply(quote);
        await db.SaveChangesAsync();
        Assert.Equal(360_000m, quote.TotalAmount);

        var controller = Controller(db);
        await controller.UsePromotion(quote.CheckoutQuoteId, new MobileCheckoutController.PromotionChoiceRequest(4, false), default);
        Assert.True(quote.PromotionDeclined);
        Assert.Equal(0m, quote.PromotionDiscount);
        Assert.Equal(400_000m, quote.TotalAmount);

        await controller.UsePromotion(quote.CheckoutQuoteId, new MobileCheckoutController.PromotionChoiceRequest(4, true), default);
        Assert.Equal(40_000m, quote.PromotionDiscount);
        Assert.Equal(360_000m, quote.TotalAmount);
    }

    [Fact]
    public async Task Declined_promotion_lets_a_smaller_voucher_apply()
    {
        await using var db = Db();
        var voucher = Seed(db, Fixed(30_000m));
        var quote = Quote(db, 4, null, "OPEN");
        quote.PromotionId = 2; quote.PromotionName = "Giờ vàng"; quote.PromotionOfferDiscount = 40_000m;
        CheckoutPricing.Apply(quote);
        await db.SaveChangesAsync();
        var controller = Controller(db);
        await controller.ApplyVoucher(quote.CheckoutQuoteId, new MobileCheckoutController.VoucherRequest(4, voucher.VoucherId), default);
        Assert.Equal(40_000m, quote.PromotionDiscount);   // promotion better: voucher picked but not used
        Assert.Equal(0m, quote.VoucherDiscount);
        await controller.UsePromotion(quote.CheckoutQuoteId, new MobileCheckoutController.PromotionChoiceRequest(4, false), default);
        Assert.Equal(0m, quote.PromotionDiscount);
        Assert.Equal(30_000m, quote.VoucherDiscount);
        await controller.ApplyVoucher(quote.CheckoutQuoteId, new MobileCheckoutController.VoucherRequest(4, null), default); // "Bỏ voucher"
        Assert.Equal(0m, quote.VoucherDiscount);
        Assert.Equal(400_000m, quote.TotalAmount);
    }

    [Fact]
    public void Voucher_offer_without_a_picked_voucher_is_never_applied()
    {
        var quote = new CheckoutQuote { Subtotal = 400_000m, VoucherId = null, VoucherOfferDiscount = 50_000m };
        CheckoutPricing.Apply(quote);
        Assert.Equal(0m, quote.VoucherDiscount);
        Assert.Equal(400_000m, quote.TotalAmount);
    }

    // ---- Admin ----

    [Fact]
    public async Task Admin_creates_a_one_time_voucher_with_a_total()
    {
        await using var db = Db();
        var controller = new InternalVouchersController(db);
        var request = new InternalVouchersController.VoucherAdminRequest(" chao-ban ", "Chào bạn mới", "FIXED", 50_000m, null, 200_000m,
            Now.AddDays(-1), Now.AddDays(30), 1, 100);
        Assert.IsType<OkObjectResult>((await controller.Create(request, default)).Result);
        var row = await db.Vouchers.SingleAsync();
        Assert.Equal("CHAO-BAN", row.Code);
        Assert.Equal(1, row.MaxUsesPerCustomer);
        Assert.Equal(100, row.MaxTotalUses);
        Assert.IsType<ConflictObjectResult>((await controller.Create(request, default)).Result); // code taken
        Assert.IsType<BadRequestObjectResult>((await controller.Create(request with { Code = "NEW1", MaxUsesPerCustomer = 0 }, default)).Result);
    }

    // ---- "Ưu đãi mới" broadcast (bug 1, owner's test 28/10) ----

    [Fact]
    public async Task Broadcast_reaches_many_customers_once_and_only_adds_the_missing()
    {
        await using var db = new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var ids = Enumerable.Range(1, 1200).ToArray();
        Assert.Equal(1200, await CustomerInbox.BroadcastAsync(db, ids, CustomerInbox.PromotionCreated, 5, "Ưu đãi mới"));
        Assert.Equal(0, await CustomerInbox.BroadcastAsync(db, ids, CustomerInbox.PromotionCreated, 5, "Ưu đãi mới"));
        Assert.Equal(1, await CustomerInbox.BroadcastAsync(db, new[] { 1201, 3 }, CustomerInbox.PromotionCreated, 5, "Ưu đãi mới"));
        Assert.Equal(1, await CustomerInbox.BroadcastAsync(db, new[] { 3 }, CustomerInbox.PromotionCreated, 6, "Ưu đãi khác"));
        Assert.Equal(1202, await db.ReadyDishNotifications.CountAsync());
        Assert.Equal("offer", CustomerInbox.Category(CustomerInbox.PromotionCreated));
    }

    // ---- helpers ----

    private static int _code;

    private static Voucher Fixed(decimal value, decimal min = 0, int? perCustomer = null, int? total = null,
        DateTime? starts = null, DateTime? ends = null, string? code = null) => new()
    {
        Code = code ?? "V" + Interlocked.Increment(ref _code), Name = "Voucher", DiscountType = "FIXED", DiscountValue = value,
        MinSubtotal = min, IsActive = true, StartsAtUtc = starts ?? Now.AddDays(-1), EndsAtUtc = ends ?? Now.AddDays(10),
        MaxUsesPerCustomer = perCustomer, MaxTotalUses = total,
    };

    private static Voucher Seed(BillingDbContext db, Voucher voucher) { db.Vouchers.Add(voucher); db.SaveChanges(); return voucher; }

    private static CheckoutQuote Quote(BillingDbContext db, int customerId, Voucher? voucher, string status, decimal voucherDiscount = 50_000m)
    {
        var now = DateTime.UtcNow;
        var quote = new CheckoutQuote
        {
            QuoteCode = "Q" + Guid.NewGuid().ToString("N"), DiningSessionId = 1, DiningSessionCode = "S", CustomerId = customerId,
            BranchId = 1, TableId = 1, Status = status, Subtotal = 400_000m, TotalAmount = 400_000m,
            VoucherId = voucher?.VoucherId, VoucherOfferDiscount = voucher is null ? 0 : voucherDiscount,
            VoucherDiscount = voucher is null ? 0 : voucherDiscount, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = new string('a', 64), ItemsJson = "[]", ExpiresAtUtc = now.AddMinutes(15), CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        db.CheckoutQuotes.Add(quote);
        return quote;
    }

    private static MobileCheckoutController Controller(BillingDbContext db) => new(db, null!, null!, null!, null!, null!);

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static BillingDbContext Db(string? name = null) => new(new DbContextOptionsBuilder<BillingDbContext>()
        .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
}
