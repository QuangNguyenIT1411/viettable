using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class InvoiceHistoryQueryTests
{
    [Fact]
    public async Task Single_read_preserves_customer_page_order_and_optional_metadata()
    {
        var counter = new QueryCounter();
        await using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).EnableServiceProviderCaching(false)
            .AddInterceptors(counter).Options);
        var now = DateTime.UtcNow;
        db.Invoices.AddRange(
            Invoice(1, 7, now), Invoice(2, 7, now), Invoice(3, 7, now.AddDays(-1)),
            Invoice(4, 99, now.AddDays(1)));
        db.CheckoutQuotes.Add(new CheckoutQuote { CheckoutQuoteId = 1, CustomerId = 7,
            QuoteCode = "fixture-quote", DiningSessionCode = "fixture-session", BranchId = 3,
            TableId = 8, IdempotencyKey = "fixture-key", RequestHash = "fixture-hash" });
        db.Payments.Add(new Payment { PaymentId = 1, CustomerId = 7, PaymentCode = "fixture-payment",
            Channel = "ONSITE", Method = "CASH", Status = "SUCCEEDED",
            IdempotencyKey = "fixture-payment-key", RequestHash = "fixture-hash" });
        await db.SaveChangesAsync();
        var controller = new MobileCheckoutController(db, null!, null!, null!, null!, null!);
        var result = await controller.CustomerInvoices(7, 2);
        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result.Result).Value);
        var items = json.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(2, items[0].GetProperty("InvoiceId").GetInt32());
        Assert.Equal(1, items[1].GetProperty("InvoiceId").GetInt32());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("branchId").ValueKind);
        Assert.Equal(3, items[1].GetProperty("branchId").GetInt32());
        Assert.Equal("SUCCEEDED", items[1].GetProperty("paymentStatus").GetString());
        Assert.Equal(35000, items[1].GetProperty("totalAmount").GetInt32());
        Assert.Equal(1, counter.Count); // One compiled read, not three sequential reads.
    }

    [Fact]
    public async Task Invalid_customer_or_limit_does_not_query_database()
    {
        var counter = new QueryCounter();
        await using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).EnableServiceProviderCaching(false)
            .AddInterceptors(counter).Options);
        var controller = new MobileCheckoutController(db, null!, null!, null!, null!, null!);
        Assert.IsType<BadRequestObjectResult>((await controller.CustomerInvoices(0)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CustomerInvoices(7, 101)).Result);
        Assert.Equal(0, counter.Count);
    }

    private static Invoice Invoice(int id, int customer, DateTime issued) => new() {
        InvoiceId = id, InvoiceCode = "fixture-invoice-" + id, CustomerId = customer,
        CheckoutQuoteId = id, PaymentId = id, DiningSessionId = id,
        Subtotal = 35000, TotalAmount = 35000, IssuedAtUtc = issued, ItemsJson = "[]" };

    private sealed class QueryCounter : IQueryExpressionInterceptor
    {
        public int Count { get; private set; }
        public Expression QueryCompilationStarting(Expression expression, QueryExpressionEventData data)
        { Count++; return expression; }
    }
}
