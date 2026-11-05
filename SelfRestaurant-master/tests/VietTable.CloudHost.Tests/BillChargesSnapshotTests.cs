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
/// Service fee + VAT per branch and the invoice snapshot (owner 05/11/2026, docs/CHECKOUT_FORMULA.md):
/// subtotal → discount → points (10% cap) → service fee on that → VAT on (that + fee) → deposit off what is left.
/// </summary>
public sealed class BillChargesSnapshotTests
{
    private static PricingResult Price(decimal subtotal, decimal fee, decimal vat, decimal promo = 0, decimal voucher = 0,
        int points = 0, int balance = 0, decimal deposit = 0) =>
        CheckoutPricing.Compute(new PricingInput(subtotal, promo > 0 ? 7 : null, promo > 0 ? "KM" : null, promo, voucher,
            points, balance, deposit, ServiceFeePercent: fee, VatPercent: vat));

    // ---- Formula ----

    [Fact]
    public void Zero_percent_keeps_the_old_total_to_the_dong()
    {
        var withZero = Price(500_000m, 0, 0, promo: 50_000m, points: 2_000, balance: 5_000, deposit: 100_000m);
        var before = CheckoutPricing.Compute(new PricingInput(500_000m, 7, "KM", 50_000m, 0, 2_000, 5_000, 100_000m));
        Assert.Equal(before.Total, withZero.Total);
        Assert.Equal(0m, withZero.ServiceFee);
        Assert.Equal(0m, withZero.Vat);
        Assert.Equal(330_000m, withZero.Total); // 500k − 50k − 20k points − 100k deposit
    }

    [Fact]
    public void Fee_then_vat_on_the_amount_after_discount_and_fee()
    {
        var r = Price(500_000m, 5m, 8m);
        Assert.Equal(25_000m, r.ServiceFee);           // 5% × 500k
        Assert.Equal(42_000m, r.Vat);                  // 8% × (500k + 25k)
        Assert.Equal(567_000m, r.Total);
        Assert.Equal(567_000m, r.GrossTotal);
    }

    [Fact]
    public void Every_step_together_promotion_points_fee_vat_deposit()
    {
        var r = Price(500_000m, 5m, 8m, promo: 50_000m, points: 2_000, balance: 5_000, deposit: 300_000m);
        Assert.Equal(50_000m, r.PromotionDiscount);
        Assert.Equal(2_000, r.PointsApplied);
        Assert.Equal(20_000m, r.PointsDiscount);       // after discount 450k → points 20k
        Assert.Equal(21_500m, r.ServiceFee);           // 5% × 430k
        Assert.Equal(36_120m, r.Vat);                  // 8% × 451.5k
        Assert.Equal(300_000m, r.DepositApplied);
        Assert.Equal(187_620m, r.Total);               // 487.62k − 300k
        Assert.Equal(487_620m, r.GrossTotal);
    }

    [Fact]
    public void Points_cap_stays_on_the_amount_after_discount_not_on_fee_or_vat()
    {
        // After discount 100k: cap 10% = 10k = 1.000 points, even though fee and VAT make the bill larger.
        var r = Price(100_000m, 10m, 10m, points: 5_000, balance: 9_000);
        Assert.Equal(1_000, r.PointsApplied);
        Assert.Equal(10_000m, r.PointsDiscount);
        Assert.Equal(9_000m, r.ServiceFee);            // 10% × 90k
        Assert.Equal(9_900m, r.Vat);                   // 10% × 99k
        Assert.Equal(108_900m, r.Total);
    }

    [Fact]
    public void Voucher_counts_as_the_discount_before_fee_and_vat()
    {
        var r = Price(200_000m, 5m, 0, voucher: 40_000m);
        Assert.Equal(40_000m, r.VoucherDiscount);
        Assert.Equal(8_000m, r.ServiceFee);            // 5% × 160k
        Assert.Equal(168_000m, r.Total);
    }

    [Fact]
    public void Vat_only_and_fee_only_branches()
    {
        Assert.Equal(108_000m, Price(100_000m, 0, 8m).Total);
        Assert.Equal(105_000m, Price(100_000m, 5m, 0).Total);
    }

    [Fact]
    public void Amounts_round_to_the_nearest_dong_half_up()
    {
        var r = Price(333_333m, 5m, 8m);
        Assert.Equal(16_667m, r.ServiceFee);           // 16.666,65 → 16.667
        Assert.Equal(28_000m, r.Vat);                  // 8% × 350.000
        Assert.Equal(378_000m, r.Total);
        Assert.Equal(1m, CheckoutPricing.Charge(10m, 5m)); // 0,5 → 1
    }

    [Fact]
    public void Deposit_never_exceeds_what_fee_and_vat_bring_the_bill_to()
    {
        var r = Price(100_000m, 10m, 10m, deposit: 300_000m);
        Assert.Equal(121_000m, r.DepositApplied);
        Assert.Equal(0m, r.Total);
    }

    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(31, 0, false)]
    [InlineData(0, 21, false)]
    [InlineData(5.555, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(5, 8, true)]
    [InlineData(30, 20, true)]
    [InlineData(7.5, 10, true)]
    public void Admin_percent_limits(double fee, double vat, bool valid) =>
        Assert.Equal(valid, CheckoutPricing.ChargeSettingsError((decimal)fee, (decimal)vat) is null);

    [Fact]
    public void Apply_prices_a_quote_with_its_frozen_percentages()
    {
        var quote = new CheckoutQuote { Subtotal = 500_000m, ServiceFeePercent = 5m, VatPercent = 8m };
        CheckoutPricing.Apply(quote);
        Assert.Equal(25_000m, quote.ServiceFeeAmount);
        Assert.Equal(42_000m, quote.VatAmount);
        Assert.Equal(567_000m, quote.TotalAmount);
    }

    // ---- Branch settings ----

    [Fact]
    public async Task Branch_without_settings_is_zero_and_admin_save_is_validated()
    {
        await using var db = Database();
        Assert.Equal((0m, 0m), await BranchCharges.ForBranchAsync(db, 1, CancellationToken.None));
        var controller = new InternalBranchChargesController(db);
        Assert.IsType<BadRequestObjectResult>((await controller.Save(1, new(35m, 8m, 3, "Admin"), CancellationToken.None)).Result);
        Assert.IsType<OkObjectResult>((await controller.Save(1, new(5m, 8m, 3, "Admin"), CancellationToken.None)).Result);
        Assert.Equal((5m, 8m), await BranchCharges.ForBranchAsync(db, 1, CancellationToken.None));
        Assert.Equal((0m, 0m), await BranchCharges.ForBranchAsync(db, 2, CancellationToken.None));
    }

    // ---- Snapshot ----

    [Fact]
    public void Invoice_copies_every_amount_and_percent_and_is_untouched_by_later_changes()
    {
        var quote = PaidQuote();
        var payment = new Payment { PaymentId = 9, Method = "TRANSFER", ProviderTransactionId = "FT123", CompletedAtUtc = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc) };
        var invoice = InvoiceSnapshot.Create(quote, payment, 4, "Thu ngân Lan", new DateTime(2026, 11, 5, 12, 1, 0, DateTimeKind.Utc));

        Assert.Equal(1, invoice.SnapshotVersion);
        Assert.Equal(5m, invoice.ServiceFeePercent);
        Assert.Equal(8m, invoice.VatPercent);
        Assert.Equal(quote.ServiceFeeAmount, invoice.ServiceFeeAmount);
        Assert.Equal(quote.VatAmount, invoice.VatAmount);
        Assert.Equal(quote.TotalAmount, invoice.TotalAmount);
        Assert.Equal("Chi nhánh Quận 1", invoice.BranchNameSnapshot);
        Assert.Equal("Bàn 6", invoice.TableNameSnapshot);
        Assert.Equal("Thu ngân Lan", invoice.CashierNameSnapshot);
        Assert.Equal("FT123", invoice.BankTransactionRef);
        Assert.Equal(SelfRestaurant.Loyalty.LoyaltyRules.PointsEarned(quote.TotalAmount + quote.DepositApplied), invoice.PointsEarned);

        // The menu price, the branch settings and the quote change afterwards: the issued invoice does not.
        var before = JsonSerializer.Serialize(InvoiceViews.Map(invoice, null, payment), Web);
        quote.ItemsJson = "[{\"dishId\":1,\"dishName\":\"Phở bò\",\"quantity\":2,\"unitPrice\":99000,\"lineTotal\":198000}]";
        quote.ServiceFeePercent = 10m; quote.VatPercent = 10m; CheckoutPricing.Apply(quote);
        Assert.Equal(before, JsonSerializer.Serialize(InvoiceViews.Map(invoice, null, payment), Web));
        var lines = InvoiceViews.Lines(invoice.ItemsJson);
        Assert.Equal(65_000, lines.Single(x => x.DishId == 1).UnitPrice);
    }

    [Fact]
    public void Old_invoice_shows_no_fee_or_vat_percent_and_reads_pascal_case_items()
    {
        var legacy = new Invoice { InvoiceId = 3, InvoiceCode = "INV-old", Subtotal = 130_000m, TotalAmount = 130_000m,
            ItemsJson = "[{\"ItemId\":1,\"OrderId\":2,\"DishId\":5,\"DishName\":\"Bún chả\",\"Quantity\":2,\"UnitPrice\":65000,\"LineTotal\":130000}]" };
        var view = JsonSerializer.SerializeToElement(InvoiceViews.Map(legacy, new CheckoutQuote { BranchId = 1 }, null), Web);
        Assert.Equal(0, view.GetProperty("snapshotVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("serviceFeePercent").ValueKind);
        Assert.Equal(JsonValueKind.Null, view.GetProperty("vatPercent").ValueKind);
        Assert.Equal(2, view.GetProperty("itemCount").GetInt32());
        Assert.Equal("Bún chả", view.GetProperty("items")[0].GetProperty("dishName").GetString());
        Assert.Equal(1, view.GetProperty("branchId").GetInt32());
    }

    [Fact]
    public void History_month_and_cursor()
    {
        var range = InvoiceViews.MonthRange("2026-11")!.Value;
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), range.FromUtc); // 01/11 00:00 Vietnam time
        Assert.Equal(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc), range.ToUtc);
        Assert.Null(InvoiceViews.MonthRange("11/2026"));
        var at = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal((at, 42), InvoiceViews.ReadCursor(InvoiceViews.WriteCursor(at, 42)));
        Assert.Null(InvoiceViews.ReadCursor("not-a-cursor!"));
    }

    [Fact]
    public async Task History_filters_by_month_branch_method_and_code_and_pages()
    {
        await using var db = Database();
        for (var i = 1; i <= 5; i++)
        {
            db.CheckoutQuotes.Add(new CheckoutQuote { CheckoutQuoteId = i, QuoteCode = $"Q{i}", DiningSessionCode = $"S{i}", BranchId = i % 2 == 0 ? 2 : 1,
                IdempotencyKey = $"k{i}", RequestHash = "h", CustomerId = 21 });
            db.Payments.Add(new Payment { PaymentId = i, PaymentCode = $"P{i}", CheckoutQuoteId = i, CustomerId = 21, Channel = "ONSITE",
                Method = i == 3 ? "TRANSFER" : "CASH", Status = "SUCCEEDED", IdempotencyKey = $"pk{i}", RequestHash = "h" });
            db.Invoices.Add(new Invoice { InvoiceId = i, InvoiceCode = $"INV-{i:D3}", CheckoutQuoteId = i, PaymentId = i, DiningSessionId = i,
                CustomerId = 21, Subtotal = 100_000m, TotalAmount = 100_000m, IssuedAtUtc = new DateTime(2026, i <= 2 ? 10 : 11, 10 + i, 5, 0, 0, DateTimeKind.Utc),
                BranchId = i % 2 == 0 ? 2 : 1, PaymentMethodSnapshot = i == 3 ? "TRANSFER" : "CASH", SnapshotVersion = 1 });
        }
        db.Invoices.Add(new Invoice { InvoiceId = 99, InvoiceCode = "INV-other", CheckoutQuoteId = 1, PaymentId = 1, DiningSessionId = 99,
            CustomerId = 22, IssuedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = new MobileCheckoutController(db, null!, null!, null!, null!, null!);

        async Task<JsonElement> Page(int limit = 50, string? cursor = null, string? month = null, int? branch = null, string? method = null, string? q = null) =>
            JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>((await controller.CustomerInvoices(21, limit, cursor, month, branch, method, q, CancellationToken.None)).Result).Value, Web);
        int[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("invoiceId").GetInt32()).ToArray();

        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, Ids(await Page()));                 // never another customer's (99)
        Assert.Equal(new[] { 5, 4, 3 }, Ids(await Page(month: "2026-11")));
        Assert.Equal(new[] { 4, 2 }, Ids(await Page(branch: 2)));
        Assert.Equal(new[] { 3 }, Ids(await Page(method: "TRANSFER")));
        Assert.Equal(new[] { 4 }, Ids(await Page(q: "inv-004")));
        var first = await Page(limit: 2);
        Assert.Equal(new[] { 5, 4 }, Ids(first));
        var second = await Page(limit: 2, cursor: first.GetProperty("nextCursor").GetString());
        Assert.Equal(new[] { 3, 2 }, Ids(second));
        var third = await Page(limit: 2, cursor: second.GetProperty("nextCursor").GetString());
        Assert.Equal(new[] { 1 }, Ids(third));
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public void Counter_bill_receipt_has_the_same_shape_and_its_own_snapshot()
    {
        var bill = new Bills { BillID = 12, BillCode = "B-12", Subtotal = 200_000m, Discount = 20_000m, ServiceFeePercent = 5m, ServiceFeeAmount = 9_000m,
            VatPercent = 8m, VatAmount = 15_120m, TotalAmount = 204_120m, PaymentMethod = "CASH", PaymentAmount = 250_000m, ChangeAmount = 45_880m,
            BillTime = new DateTime(2026, 11, 5, 19, 0, 0), BranchNameSnapshot = "Chi nhánh Quận 1", TableNameSnapshot = "Bàn 2",
            ItemsJson = "[{\"dishId\":1,\"dishName\":\"Phở bò\",\"quantity\":2,\"unitPrice\":100000,\"lineTotal\":200000}]", EmployeeNameSnapshot = "Lan" };
        var view = JsonSerializer.SerializeToElement(CashierReceiptsController.MapBill(bill), Web);
        Assert.Equal(5m, view.GetProperty("serviceFeePercent").GetDecimal());
        Assert.Equal(20_000, view.GetProperty("manualDiscount").GetInt64());
        Assert.Equal(204_120, view.GetProperty("totalAmount").GetInt64());
        Assert.Equal(45_880, view.GetProperty("changeAmount").GetInt64());
        Assert.Equal("Lan", view.GetProperty("cashierName").GetString());
        Assert.Equal(2, view.GetProperty("itemCount").GetInt32());
        var old = JsonSerializer.SerializeToElement(CashierReceiptsController.MapBill(new Bills { BillID = 1, BillCode = "B-1", PaymentMethod = "CASH" }), Web);
        Assert.Equal(JsonValueKind.Null, old.GetProperty("vatPercent").ValueKind);
    }

    private static CheckoutQuote PaidQuote()
    {
        var quote = new CheckoutQuote
        {
            CheckoutQuoteId = 11, QuoteCode = "Q11", DiningSessionId = 46, DiningSessionCode = "S46", CustomerId = 21, BranchId = 1, TableId = 6,
            Subtotal = 130_000m, ServiceFeePercent = 5m, VatPercent = 8m, BranchNameSnapshot = "Chi nhánh Quận 1",
            BranchAddressSnapshot = "123 Nguyễn Huệ", TableNameSnapshot = "Bàn 6", SessionStartedAtUtc = new DateTime(2026, 11, 5, 11, 0, 0, DateTimeKind.Utc),
            ItemsJson = "[{\"dishId\":1,\"dishName\":\"Phở bò\",\"quantity\":2,\"unitPrice\":65000,\"lineTotal\":130000}]",
            IdempotencyKey = "k", RequestHash = "h"
        };
        CheckoutPricing.Apply(quote);
        return quote;
    }

    // The API writes camelCase (ASP.NET Core web defaults); read it the same way.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static BillingDbContext Database() =>
        new(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
