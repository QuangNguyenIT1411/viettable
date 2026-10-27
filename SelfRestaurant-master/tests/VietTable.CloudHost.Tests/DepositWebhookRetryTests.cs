using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfRestaurant.Gateway.Api.Controllers;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Owner 27/10: when the booking cannot be updated after the deposit money arrived, the webhook answers 5xx so SePay
/// retries; the retry is a Billing duplicate that names the booking again, and the Gateway repeats its idempotent step.
/// The FAQ deposit answer follows the backend flag.
/// </summary>
public sealed class DepositWebhookRetryTests
{
    [Theory]
    [InlineData("DEPOSIT_PAID", 901, true)]
    [InlineData("DEPOSIT_LATE", 901, true)]
    [InlineData("DUPLICATE", 901, true)]       // SePay's retry after our 5xx
    [InlineData("DUPLICATE", null, false)]     // a table payment re-delivered: nothing to do
    [InlineData("AUTO_CONFIRMED", 901, false)]
    [InlineData("DEPOSIT_AMOUNT_TOO_LOW", 901, false)]
    [InlineData("DEPOSIT_PAID", null, false)]
    public void Only_recorded_deposit_money_moves_a_booking_on(string outcome, int? reservationId, bool expected) =>
        Assert.Equal(expected, SePayWebhookGatewayController.NeedsDepositFollowUp(outcome, reservationId));

    [Fact]
    public async Task Customers_failure_is_thrown_so_the_webhook_answers_5xx()
    {
        var coordinator = Coordinator(depositPaidStatus: HttpStatusCode.InternalServerError);
        var error = await Assert.ThrowsAsync<ApiClientException>(() => coordinator.RecordPaymentAsync(901, default));
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public async Task Retry_moves_the_booking_on_once_customers_answers()
    {
        var coordinator = Coordinator(depositPaidStatus: HttpStatusCode.OK);
        var view = await coordinator.RecordPaymentAsync(901, default);
        Assert.NotNull(view);
        Assert.Equal("Pending", view!.Reservation.Status);
        Assert.Equal("PAID", view.Status);
    }

    [Fact]
    public async Task Booking_gone_is_not_retried_forever()
    {
        var coordinator = Coordinator(depositPaidStatus: HttpStatusCode.OK, bookingStatus: HttpStatusCode.NotFound);
        Assert.Null(await coordinator.RecordPaymentAsync(901, default));
    }

    [Fact]
    public void Faq_has_the_deposit_answer_only_while_the_flag_is_on()
    {
        var faqs = JsonDocument.Parse("""{"items":[{"faqId":"reservation","question":"q","answer":"a"}]}""").RootElement;
        var off = ReservationDepositCoordinator.WithDepositFaq(faqs, false, 6, 50_000m, 15, 2);
        Assert.Single(off["items"]!.AsArray());
        var on = ReservationDepositCoordinator.WithDepositFaq(faqs, true, 6, 50_000m, 15, 2);
        var items = on["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        var answer = items[1]!["answer"]!.GetValue<string>();
        Assert.Contains("6 khách", answer);
        Assert.Contains("50.000đ/khách", answer);
        Assert.Contains("15 phút", answer);
        Assert.Contains("2 giờ", answer);
    }

    // ---- helpers ----

    private const string Booking = """
        {"reservationId":901,"reservationCode":"RSV-20261029-0901","customerId":7,"customerName":"Khách","phoneNumber":"0901234567",
         "branchId":1,"tableId":4,"partySize":6,"reservedAt":"2026-10-29T12:00:00Z","arrivalWindowMinutes":30,"status":"STATUS",
         "createdAtUtc":"2026-10-27T12:00:00Z","preOrderItems":[],"depositAmount":300000,"depositDueAtUtc":"2026-10-27T12:15:00Z"}
        """;

    private static ReservationDepositCoordinator Coordinator(HttpStatusCode depositPaidStatus, HttpStatusCode bookingStatus = HttpStatusCode.OK)
    {
        var customers = new CustomersClient(new HttpClient(new Route(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/deposit-paid"))
                return depositPaidStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, Booking.Replace("STATUS", "Pending"))
                    : Json(depositPaidStatus, """{"code":"unavailable","message":"down"}""");
            if (path.EndsWith("/by-id/901"))
                return bookingStatus == HttpStatusCode.OK ? Json(HttpStatusCode.OK, Booking.Replace("STATUS", "PendingDeposit"))
                    : new HttpResponseMessage(bookingStatus);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })) { BaseAddress = new Uri("http://customers") }, new Factory());
        var billing = new BillingClient(new HttpClient(new Route(request =>
            request.RequestUri!.AbsolutePath.Contains("/deposits/by-reservation/901")
                ? Json(HttpStatusCode.OK, """{"deposit":{"reservationId":901,"amount":300000,"status":"PAID"},"transfer":null}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound))) { BaseAddress = new Uri("http://billing") },
            new ConfigurationBuilder().Build());
        // The Gateway's own DepositOptions type (this test project links its own copy of DepositRules.cs).
        var optionsType = typeof(ReservationDepositCoordinator).Assembly.GetType("SelfRestaurant.Deposits.DepositOptions")!;
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("Enabled")!.SetValue(options, true);
        var wrapped = typeof(Options).GetMethod(nameof(Options.Create))!.MakeGenericMethod(optionsType).Invoke(null, [options]);
        return (ReservationDepositCoordinator)Activator.CreateInstance(typeof(ReservationDepositCoordinator),
            billing, customers, wrapped, NullLogger<ReservationDepositCoordinator>.Instance)!;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Route(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(answer(request));
    }

    private sealed class Factory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Route(_ => new HttpResponseMessage(HttpStatusCode.NotFound))) { BaseAddress = new Uri("http://fallback") };
    }
}
