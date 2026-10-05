using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

// Backend-only compatibility acceptance. Requires the six isolated compatibility
// processes; never targets the live 5100 SQL Server demo, and never logs tokens.
public sealed class PostgresWorkflowTests
{
    private readonly HttpClient gateway = new() { BaseAddress = new Uri("http://127.0.0.1:6100") };
    private readonly HttpClient catalog = new() { BaseAddress = new Uri("http://127.0.0.1:6101") };
    private readonly HttpClient orders = new() { BaseAddress = new Uri("http://127.0.0.1:6102") };

    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path,
        object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Correlation-Id", "postgres-compat-" + Guid.NewGuid().ToString("N"));
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        // Never put a login/token response body into assertion output.
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: HTTP {(int)response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content).RootElement.Clone();
    }

    private static async Task<JsonElement> Race(HttpClient client, string path, object body, string key)
    {
        async Task<HttpResponseMessage> Submit()
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            message.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(message);
        }
        var responses = await Task.WhenAll(Submit(), Submit());
        try
        {
            Assert.Contains(responses, r => r.IsSuccessStatusCode);
            Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode || r.StatusCode == System.Net.HttpStatusCode.Conflict,
                $"Competing {path}: HTTP {(int)r.StatusCode}"));
            return JsonDocument.Parse(await responses.First(r => r.IsSuccessStatusCode).Content.ReadAsStringAsync()).RootElement.Clone();
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    [Trait("Category", "PostgresWorkflow")]
    public async Task Gateway_auth_orders_service_requests_onsite_invoice_and_session_closure()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "!a1";
        var username = "pg_" + suffix;
        var registration = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/register", new {
            name = "Disposable PostgreSQL test", username, password,
            phoneNumber = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999), email = username + "@example.test" });
        Assert.False(string.IsNullOrWhiteSpace(registration.GetProperty("accessToken").GetString()));
        var login = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/login", new { username = username.ToUpperInvariant(), password });
        var refresh = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/refresh", new {
            refreshToken = login.GetProperty("refreshToken").GetString() });
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refresh.GetProperty("accessToken").GetString());
        _ = await Send(gateway, HttpMethod.Get, "/api/mobile/v1/me");
        _ = await Send(gateway, HttpMethod.Get, "/api/mobile/v1/reservations");
        var branches = await Send(gateway, HttpMethod.Get, "/api/mobile/v1/branches");
        var branchId = branches[0].GetProperty("branchId").GetInt32();
        var layout = await Send(catalog, HttpMethod.Get, $"/api/branches/{branchId}/tables");
        var table = layout.GetProperty("tables").EnumerateArray().First(x => x.GetProperty("isAvailable").GetBoolean());
        var session = await Race(gateway, "/api/mobile/v1/check-ins/walk-in", new {
            qrCode = table.GetProperty("qrCode").GetString() }, suffix + "-walkin");
        var sessionId = session.GetProperty("diningSessionId").GetInt32();
        Assert.Equal("ACTIVE", session.GetProperty("status").GetString());
        var menu = await Send(gateway, HttpMethod.Get, $"/api/mobile/v1/branches/{branchId}/menu");
        var dish = menu.GetProperty("categories")[0].GetProperty("dishes")[0];
        var dishId = dish.GetProperty("dishId").GetInt32();
        var body = new { items = new[] { new { dishId, quantity = 1 } } };
        var orderPath = $"/api/mobile/v1/dining-sessions/{sessionId}/orders";
        var first = await Race(gateway, orderPath, body, suffix + "-round1");
        var replay = await Send(gateway, HttpMethod.Post, orderPath, body, suffix + "-round1");
        var orderId = first.GetProperty("orderId").GetInt32();
        Assert.Equal(orderId, replay.GetProperty("orderId").GetInt32());
        var second = await Send(gateway, HttpMethod.Post, orderPath, body, suffix + "-round2");
        Assert.NotEqual(orderId, second.GetProperty("orderId").GetInt32());
        await Send(orders, HttpMethod.Post, $"/api/orders/{orderId}/chef/start");
        await Send(orders, HttpMethod.Post, $"/api/orders/{orderId}/chef/ready");
        var detail = await Send(gateway, HttpMethod.Get, $"/api/mobile/v1/orders/{orderId}");
        var itemId = detail.GetProperty("items")[0].GetProperty("itemId").GetInt32();
        await Send(gateway, HttpMethod.Post, $"/api/mobile/v1/orders/{orderId}/items/{itemId}/confirm-received");
        var servicePath = $"/api/mobile/v1/dining-sessions/{sessionId}/service-requests";
        var request = await Send(gateway, HttpMethod.Post, servicePath, new { requestType = "WATER" }, suffix + "-water");
        var cancelled = await Send(gateway, HttpMethod.Post, $"/api/mobile/v1/service-requests/{request.GetProperty("serviceRequestId").GetInt32()}/cancel");
        Assert.Equal("CANCELLED", cancelled.GetProperty("status").GetString());
        var quote = await Send(gateway, HttpMethod.Post, $"/api/mobile/v1/dining-sessions/{sessionId}/checkout/quote", new { }, suffix + "-quote");
        var quoteId = quote.GetProperty("checkoutQuoteId").GetInt32();
        Assert.Equal(dish.GetProperty("price").GetDecimal() * 2, quote.GetProperty("totalAmount").GetDecimal());
        await Send(gateway, HttpMethod.Post, $"/api/mobile/v1/dining-sessions/{sessionId}/payment-request", new { checkoutQuoteId = quoteId });
        var paymentPath = $"/api/mobile/v1/checkout/quotes/{quoteId}/payments";
        var paymentBody = new { channel = "ONSITE", method = "CASH", location = "TABLE" };
        var payment = await Send(gateway, HttpMethod.Post, paymentPath, paymentBody, suffix + "-payment");
        var retry = await Send(gateway, HttpMethod.Post, paymentPath, paymentBody, suffix + "-payment");
        var paymentId = payment.GetProperty("paymentId").GetInt32();
        Assert.Equal(paymentId, retry.GetProperty("paymentId").GetInt32());
        Assert.Equal("PENDING", payment.GetProperty("status").GetString());
        var connection = Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Disposable test connection required.");
        await using var billing = (BillingDbContext)PostgresModelTests.Context("billing", connection);
        using var ordersHttp = new HttpClient { BaseAddress = orders.BaseAddress };
        using var identityHttp = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:6104") };
        // Domain finalization with REAL downstream APIs, not a provider callback fake.
        // Cashier UI/JWT authorization is a separate acceptance, not claimed here.
        var finalizer = new PaymentFinalizationService(billing, new OrdersApiClient(ordersHttp, billing),
            new CustomersApiClient(identityHttp), NullLogger<PaymentFinalizationService>.Instance);
        await using var competingBilling = (BillingDbContext)PostgresModelTests.Context("billing", connection);
        var competingFinalizer = new PaymentFinalizationService(competingBilling,
            new OrdersApiClient(ordersHttp, competingBilling), new CustomersApiClient(identityHttp),
            NullLogger<PaymentFinalizationService>.Instance);
        await Task.WhenAll(finalizer.ConfirmAsync(paymentId, null, null, null, CancellationToken.None),
            competingFinalizer.ConfirmAsync(paymentId, null, null, null, CancellationToken.None));
        var paid = await Send(gateway, HttpMethod.Get, $"/api/mobile/v1/payments/{paymentId}");
        Assert.Equal("SUCCEEDED", paid.GetProperty("status").GetString());
        var invoice = await Send(gateway, HttpMethod.Get, $"/api/mobile/v1/dining-sessions/{sessionId}/invoice");
        Assert.Equal(quote.GetProperty("totalAmount").GetDecimal(), invoice.GetProperty("totalAmount").GetDecimal());
        var closed = await Send(gateway, HttpMethod.Get, $"/api/mobile/v1/dining-sessions/{sessionId}");
        Assert.Equal("CLOSED", closed.GetProperty("status").GetString());
        Assert.Single(await billing.Invoices.Where(x => x.DiningSessionId == sessionId).ToListAsync());
    }
}
