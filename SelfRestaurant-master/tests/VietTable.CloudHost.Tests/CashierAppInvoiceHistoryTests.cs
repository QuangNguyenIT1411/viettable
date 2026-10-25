using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

// Cashier history/report pages must list app (mobile checkout) Invoices next to legacy Bills, once each.
public sealed class CashierAppInvoiceHistoryTests
{
    private const int Cashier = 3;
    private const int OtherCashier = 9;
    private const int Branch = 1;

    [Fact]
    public async Task Cashier_bills_include_own_confirmed_app_invoices_newest_first_without_duplicates()
    {
        await using var db = await SeedAsync();
        var result = await Controller(db).GetBills(Cashier, Branch, Today(), take: 50);
        var rows = Rows(result.Result);

        Assert.Equal(new[] { "Hóa đơn #11", "BILL-1" }, rows.Select(x => x.BillCode));
        var app = rows[0];
        Assert.Equal(-11, app.BillId);
        Assert.Equal(45, app.OrderId);
        Assert.Equal("ORD-45", app.OrderCode);
        Assert.Equal("Bàn 4", app.TableName);
        Assert.Equal("Khách App", app.CustomerName);
        Assert.Equal(90_000m, app.TotalAmount);
        Assert.Equal(10_000m, app.Discount);
        Assert.Equal("CASH", app.PaymentMethod);
    }

    [Fact]
    public async Task Daily_report_counts_each_bill_and_invoice_once_with_correct_revenue()
    {
        await using var db = await SeedAsync();
        var report = Assert.IsType<CashierBillsController.CashierReportResponse>(
            Assert.IsType<OkObjectResult>((await Controller(db).GetReport(Cashier, Branch, Today())).Result).Value);

        Assert.Equal(2, report.BillCount);              // legacy bill + own app invoice
        Assert.Equal(50_000m + 90_000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Branch_report_counts_all_branch_app_invoices_including_online_and_other_cashiers()
    {
        await using var db = await SeedAsync();
        var report = Assert.IsType<CashierBillsController.BranchCashierReportResponse>(
            Assert.IsType<OkObjectResult>((await Controller(db).GetBranchReport(Branch, Today())).Result).Value);

        // Bill 50k + invoice 11 (cashier) 90k + invoice 12 (other cashier) 70k + invoice 13 (online VNPAY) 40k.
        // Invoice 14 is another branch and invoice 15 is yesterday.
        Assert.Equal(4, report.BillCount);
        Assert.Equal(250_000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Internal_history_pages_across_bills_and_invoices()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db);

        async Task<JsonElement> Page(int page) => JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(
            (await controller.GetInternalCashierHistory(Cashier, page: page, pageSize: 1)).Result).Value);

        // Admin history is not branch-scoped (same as legacy bills): bill + invoices 11, 14 (branch 2) and 15 (yesterday).
        var first = await Page(1);
        Assert.Equal(4, first.GetProperty("totalItems").GetInt32());
        Assert.Equal(4, first.GetProperty("totalPages").GetInt32());
        Assert.Equal("Hóa đơn #11", first.GetProperty("items")[0].GetProperty("BillCode").GetString());
        Assert.Equal("Hóa đơn #14", (await Page(2)).GetProperty("items")[0].GetProperty("BillCode").GetString());
        Assert.Equal("BILL-1", (await Page(3)).GetProperty("items")[0].GetProperty("BillCode").GetString());
        Assert.Equal("Hóa đơn #15", (await Page(4)).GetProperty("items")[0].GetProperty("BillCode").GetString());
    }

    [Fact]
    public async Task Other_cashier_sees_only_the_invoice_they_confirmed()
    {
        await using var db = await SeedAsync();
        var rows = Rows((await Controller(db).GetBills(OtherCashier, Branch, Today(), take: 50)).Result);
        Assert.Equal(new[] { "Hóa đơn #12" }, rows.Select(x => x.BillCode));
    }

    // SePay auto-confirmed invoices have no cashier: every cashier of that branch sees them, labelled automatic.
    [Fact]
    public async Task Sepay_auto_confirmed_invoice_is_listed_for_every_cashier_of_the_branch()
    {
        await using var db = await SeedAsync();
        AddAppInvoice(db, 16, Branch, DateTime.UtcNow.AddMinutes(-1), 120_000m, "TRANSFER", null, orderId: 50);
        AddAppInvoice(db, 17, 2, DateTime.UtcNow.AddMinutes(-2), 60_000m, "TRANSFER", null, orderId: 51);
        await db.SaveChangesAsync();
        foreach (var id in new[] { 16, 17 }) (await db.Payments.SingleAsync(p => p.PaymentId == id)).ProviderName = "SEPAY";
        await db.SaveChangesAsync();

        foreach (var cashier in new[] { Cashier, OtherCashier })
        {
            var rows = Rows((await Controller(db).GetBills(cashier, Branch, Today(), take: 50)).Result);
            var auto = Assert.Single(rows, x => x.BillCode == "Hóa đơn #16");
            Assert.True(auto.AutoConfirmed);
            Assert.DoesNotContain(rows, x => x.BillCode == "Hóa đơn #17"); // other branch
            Assert.All(rows.Where(x => x.BillCode != "Hóa đơn #16"), x => Assert.False(x.AutoConfirmed));
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    private static List<CashierBillsController.BillSummaryResponse> Rows(ActionResult? result) =>
        Assert.IsAssignableFrom<IEnumerable<CashierBillsController.BillSummaryResponse>>(
            Assert.IsType<OkObjectResult>(result).Value).ToList();

    private static async Task<BillingDbContext> SeedAsync()
    {
        var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.Now;
        db.Bills.Add(new Bills { BillID = 1, OrderID = 30, BillCode = "BILL-1", OrderCodeSnapshot = "ORD-30",
            TableNameSnapshot = "Bàn 2", BranchIdSnapshot = Branch, BillTime = now.AddMinutes(-30), Subtotal = 50_000m,
            TotalAmount = 50_000m, PaymentMethod = "CASH", EmployeeID = Cashier, IsActive = true });
        db.OrderContextSnapshots.Add(new OrderContextSnapshots { OrderId = 45, OrderCode = "ORD-45", TableId = 4,
            TableName = "Bàn 4", BranchId = Branch, RefreshedAtUtc = DateTime.UtcNow });

        AddAppInvoice(db, 11, Branch, DateTime.UtcNow.AddMinutes(-5), 90_000m, "CASH", Cashier, orderId: 45, discount: 10_000m);
        AddAppInvoice(db, 12, Branch, DateTime.UtcNow.AddMinutes(-10), 70_000m, "CASH", OtherCashier, orderId: 46);
        AddAppInvoice(db, 13, Branch, DateTime.UtcNow.AddMinutes(-15), 40_000m, "VNPAY", null, orderId: 47);
        AddAppInvoice(db, 14, 2, DateTime.UtcNow.AddMinutes(-20), 30_000m, "CASH", Cashier, orderId: 48);
        AddAppInvoice(db, 15, Branch, DateTime.UtcNow.AddDays(-1).AddHours(-1), 20_000m, "CASH", Cashier, orderId: 49);
        // A failed attempt by this cashier must not attach invoice 12 to them.
        db.Payments.Add(new Payment { PaymentId = 120, PaymentCode = "PAY-120", CheckoutQuoteId = 12, DiningSessionId = 12,
            CustomerId = 7, Amount = 70_000m, Channel = "ONSITE", Method = "CASH", Status = "FAILED",
            ConfirmedByEmployeeId = Cashier, IdempotencyKey = "k-120", RequestHash = "h" });
        await db.SaveChangesAsync();
        return db;
    }

    private static void AddAppInvoice(BillingDbContext db, int id, int branch, DateTime issuedUtc, decimal total,
        string method, int? confirmedBy, int orderId, decimal discount = 0m)
    {
        db.CheckoutQuotes.Add(new CheckoutQuote { CheckoutQuoteId = id, QuoteCode = $"Q-{id}", DiningSessionId = id,
            DiningSessionCode = $"DS-{id}", CustomerId = 7, BranchId = branch, TableId = 4, Status = "PAID",
            Subtotal = total + discount, VoucherDiscount = discount, TotalAmount = total, IdempotencyKey = $"q-{id}", RequestHash = "h" });
        db.Payments.Add(new Payment { PaymentId = id, PaymentCode = $"PAY-{id}", CheckoutQuoteId = id, DiningSessionId = id,
            CustomerId = 7, Amount = total, Channel = method == "VNPAY" ? "ONLINE" : "ONSITE", Method = method,
            Status = "SUCCEEDED", ConfirmedByEmployeeId = confirmedBy, IdempotencyKey = $"p-{id}", RequestHash = "h" });
        db.Invoices.Add(new Invoice { InvoiceId = id, InvoiceCode = $"INV-{Guid.NewGuid():N}", CheckoutQuoteId = id,
            PaymentId = id, DiningSessionId = id, CustomerId = 7, Subtotal = total + discount, Discount = discount,
            TotalAmount = total, ItemsJson = $"[{{\"ItemId\":1,\"OrderId\":{orderId},\"DishId\":2,\"DishName\":\"Phở\",\"Quantity\":1}}]",
            IssuedAtUtc = issuedUtc });
    }

    private static CashierBillsController Controller(BillingDbContext db) => new(db,
        new OrdersApiClient(new HttpClient(new Stub(HttpStatusCode.ServiceUnavailable, "")) { BaseAddress = new Uri("http://orders.invalid") }, db),
        new CustomersApiClient(new HttpClient(new Stub(HttpStatusCode.OK,
            "[{\"customerId\":7,\"username\":\"app\",\"name\":\"Khách App\"}]")) { BaseAddress = new Uri("http://customers.invalid") }));

    // Orders is "down" so order context comes from the local snapshot cache; Customers returns one fixture.
    private sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
