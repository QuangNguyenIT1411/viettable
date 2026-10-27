using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using SelfRestaurant.Deposits;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>Reservation deposits (owner 27/10/2026, docs/DEPOSIT_PLAN.md).</summary>
public sealed class ReservationDepositTests
{
    private const string Secret = "test-secret-not-real";
    private const string Account = "96247TEST01";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);
    private static readonly DepositOptions On = new() { Enabled = true };

    // ---- Amount ----

    [Theory]
    [InlineData(false, 8, 0)]          // feature off: never a deposit
    [InlineData(true, 1, 0)]
    [InlineData(true, 5, 0)]
    [InlineData(true, 6, 300_000)]     // threshold: 6 × 50.000đ
    [InlineData(true, 10, 500_000)]
    public void Deposit_is_50k_per_guest_from_six_guests_when_enabled(bool enabled, int guests, int expected) =>
        Assert.Equal(expected, DepositRules.RequiredAmount(new DepositOptions { Enabled = enabled }, guests));

    [Fact]
    public void Threshold_and_amount_come_from_configuration()
    {
        var options = new DepositOptions { Enabled = true, MinGuests = 4, PerGuestVnd = 100_000m };
        Assert.Equal(0m, DepositRules.RequiredAmount(options, 3));
        Assert.Equal(400_000m, DepositRules.RequiredAmount(options, 4));
    }

    // ---- 15 minutes ----

    [Fact]
    public void Unpaid_deposit_expires_after_15_minutes()
    {
        var created = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var due = DepositRules.DueAt(created, On);
        Assert.Equal(created.AddMinutes(15), due);
        Assert.False(DepositRules.IsOverdue(DepositRules.Pending, due, due));            // at the deadline: still payable
        Assert.True(DepositRules.IsOverdue(DepositRules.Pending, due, due.AddSeconds(1)));
        Assert.Equal(DepositRules.Expired, DepositRules.Effective(DepositRules.Pending, due, due.AddMinutes(1)));
        Assert.False(DepositRules.IsOverdue(DepositRules.Paid, due, due.AddHours(1)));   // paid never expires
    }

    [Fact]
    public async Task Reading_an_overdue_deposit_stores_it_expired()
    {
        var db = Db();
        db.ReservationDeposits.Add(Deposit(due: DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var deposit = await new ReservationDepositService(db, null!).GetAsync(901, default);
        Assert.Equal(DepositRules.Expired, deposit!.Status);
        Assert.Equal("Hết hạn đặt cọc", deposit.ClosedReason);
    }

    [Fact]
    public async Task Creating_twice_keeps_one_deposit()
    {
        var db = Db();
        var service = new ReservationDepositService(db, null!);
        var request = new CreateDepositRequest(901, 7, 1, 6, 300_000m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddMinutes(15));
        var first = await service.CreateAsync(request, default);
        var second = await service.CreateAsync(request with { Amount = 999_000m }, default);
        Assert.Equal(first.ReservationDepositId, second.ReservationDepositId);
        Assert.Equal(300_000m, Assert.Single(db.ReservationDeposits).Amount);
    }

    // ---- Cancel ≥ 2 h / < 2 h, rejected, no-show ----

    [Theory]
    [InlineData(180, "REFUND_REQUIRED")]   // 3 hours ahead
    [InlineData(120, "REFUND_REQUIRED")]   // exactly 2 hours: refunded
    [InlineData(119, "FORFEITED")]         // 1 h 59: kept
    [InlineData(10, "FORFEITED")]
    public void Paid_deposit_on_cancel_follows_the_two_hour_rule(int minutesBefore, string expected)
    {
        var reservedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var result = DepositRules.OnReservationClosed(DepositRules.Paid, "Cancelled", reservedAt, reservedAt.AddMinutes(-minutesBefore), On);
        Assert.Equal(expected, result!.Value.Status);
    }

    [Fact]
    public void Rejected_is_refunded_no_show_is_kept_and_money_never_moves_by_itself()
    {
        var reservedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DepositRules.RefundRequired, DepositRules.OnReservationClosed(DepositRules.Paid, "Rejected", reservedAt, reservedAt.AddMinutes(-5), On)!.Value.Status);
        Assert.Equal(DepositRules.Forfeited, DepositRules.OnReservationClosed(DepositRules.Paid, "NoShow", reservedAt, reservedAt.AddMinutes(40), On)!.Value.Status);
        Assert.Equal(DepositRules.RefundRequired, DepositRules.OnReservationClosed(DepositRules.Paid, DepositRules.ReservationClosedBeforePayment, reservedAt, reservedAt, On)!.Value.Status);
        Assert.Equal(DepositRules.Cancelled, DepositRules.OnReservationClosed(DepositRules.Pending, "Cancelled", reservedAt, reservedAt.AddHours(-5), On)!.Value.Status);
        // Nothing changes once decided, nor for an open booking.
        Assert.Null(DepositRules.OnReservationClosed(DepositRules.Applied, "Cancelled", reservedAt, reservedAt.AddHours(-5), On));
        Assert.Null(DepositRules.OnReservationClosed(DepositRules.RefundRequired, "NoShow", reservedAt, reservedAt, On));
        Assert.Null(DepositRules.OnReservationClosed(DepositRules.Paid, "Confirmed", reservedAt, reservedAt, On));
    }

    // ---- Deposit on the final bill ----

    [Fact]
    public void Deposit_comes_off_the_bill_after_voucher_and_points()
    {
        var quote = Quote(subtotal: 800_000m);
        ReservationDepositService.AttachToQuote(quote, Deposit(status: DepositRules.Paid));
        Assert.Equal(300_000m, quote.DepositApplied);
        Assert.Equal(500_000m, quote.TotalAmount);
        quote.VoucherDiscount = 50_000m; quote.PointsDiscount = 10_000m;
        ReservationDepositService.Recalculate(quote);
        Assert.Equal(300_000m, quote.DepositApplied);
        Assert.Equal(440_000m, quote.TotalAmount);
    }

    [Fact]
    public void Deposit_larger_than_the_bill_leaves_nothing_to_pay_never_below_zero()
    {
        var quote = Quote(subtotal: 200_000m);
        ReservationDepositService.AttachToQuote(quote, Deposit(status: DepositRules.Paid));
        Assert.Equal(200_000m, quote.DepositApplied);
        Assert.Equal(0m, quote.TotalAmount);
    }

    [Fact]
    public async Task Paying_the_bill_uses_the_deposit_once()
    {
        var db = Db();
        var deposit = Deposit(status: DepositRules.Paid);
        db.ReservationDeposits.Add(deposit);
        await db.SaveChangesAsync();
        var quote = Quote(subtotal: 800_000m);
        ReservationDepositService.AttachToQuote(quote, deposit);
        await ReservationDepositService.MarkAppliedAsync(db, quote, default);
        await db.SaveChangesAsync();
        var stored = await db.ReservationDeposits.SingleAsync();
        Assert.Equal(DepositRules.Applied, stored.Status);
        Assert.Equal(300_000m, stored.AppliedAmount);
        Assert.Equal(40, stored.AppliedCheckoutQuoteId);
        Assert.Null(await ReservationDepositService.PaidForReservationAsync(db, 901, default)); // not offered to a second bill
    }

    // ---- Webhook ----

    [Theory]
    [InlineData("VT DC901", 901)]
    [InlineData("vt dc901", 901)]
    [InlineData("VTDC901", 901)]
    [InlineData("MBVCB.1234.VT DC901.CT tu 0123", 901)]
    [InlineData("VT-DC-901 dat coc", 901)]
    public void Deposit_reference_survives_bank_formatting(string content, int reservationId) =>
        Assert.Equal(reservationId, SePayContentParser.ParseDeposit(content));

    [Theory]
    [InlineData("VT B1 TT128")]
    [InlineData("VT DC901 VT DC902")]
    [InlineData("XVT DC901")]
    [InlineData("chuyen tien")]
    public void Table_payments_and_ambiguous_contents_are_not_deposits(string content) =>
        Assert.Null(SePayContentParser.ParseDeposit(content));

    [Fact]
    public void Payment_decision_covers_in_time_short_late_and_already_paid()
    {
        var due = Now.UtcDateTime.AddMinutes(5);
        Assert.Equal(DepositRules.OutcomePaid, DepositRules.DecidePayment(DepositRules.Pending, 300_000m, due, 300_000m, Now.UtcDateTime));
        Assert.Equal(DepositRules.OutcomePaid, DepositRules.DecidePayment(DepositRules.Pending, 300_000m, due, 350_000m, Now.UtcDateTime));
        Assert.Equal(DepositRules.OutcomeAmountTooLow, DepositRules.DecidePayment(DepositRules.Pending, 300_000m, due, 299_000m, Now.UtcDateTime));
        Assert.Equal(DepositRules.OutcomeLate, DepositRules.DecidePayment(DepositRules.Pending, 300_000m, due, 300_000m, due.AddSeconds(1)));
        Assert.Equal(DepositRules.OutcomeLate, DepositRules.DecidePayment(DepositRules.Cancelled, 300_000m, due, 300_000m, Now.UtcDateTime));
        Assert.Equal(DepositRules.OutcomeNotPending, DepositRules.DecidePayment(DepositRules.Paid, 300_000m, due, 300_000m, Now.UtcDateTime));
        Assert.Equal(DepositRules.OutcomeNoMatch, DepositRules.DecidePayment(null, 0, default, 300_000m, Now.UtcDateTime));
    }

    [Fact]
    public async Task Right_amount_in_time_pays_the_deposit_once_even_when_delivered_twice()
    {
        var (db, confirmer, service) = await Setup();
        var body = Body("7001", 300_000m, "IBFT VT DC901");
        var first = await Deliver(service, body);
        var second = await Deliver(service, body);
        Assert.Equal(DepositRules.OutcomePaid, first.Outcome);
        Assert.Equal(901, first.ReservationId);
        Assert.Equal(7, first.CustomerId);
        Assert.Equal(1, first.BranchId);
        Assert.Equal(SePayWebhookService.Duplicate, second.Outcome);
        Assert.Equal(901, second.ReservationId); // the retry names the booking again (Gateway repeats its step)
        Assert.Equal(0, confirmer.Calls); // the table payment path is never touched
        var deposit = await db.ReservationDeposits.SingleAsync();
        Assert.Equal(DepositRules.Paid, deposit.Status);
        Assert.Equal(300_000m, deposit.PaidAmount);
        Assert.Equal("SEPAY:7001", deposit.ProviderTransactionId);
        var row = Assert.Single(db.BankTransferEvents);
        Assert.Equal(DepositRules.OutcomePaid, row.Outcome);
        Assert.Null(row.PaymentId);
    }

    [Fact]
    public async Task Too_little_money_keeps_the_deposit_waiting_and_warns_the_cashier()
    {
        var (db, _, service) = await Setup();
        var result = await Deliver(service, Body("7002", 250_000m, "VT DC901"));
        Assert.Equal(DepositRules.OutcomeAmountTooLow, result.Outcome);
        Assert.Equal(DepositRules.Pending, (await db.ReservationDeposits.SingleAsync()).Status);
        Assert.Contains(DepositRules.OutcomeAmountTooLow, CashierBankTransfersController.WarningOutcomes);
    }

    [Fact]
    public async Task Money_after_the_deadline_is_a_refund_to_do()
    {
        var (db, _, service) = await Setup(due: Now.UtcDateTime.AddMinutes(-1));
        var result = await Deliver(service, Body("7003", 300_000m, "VT DC901"));
        Assert.Equal(DepositRules.OutcomeLate, result.Outcome);
        var deposit = await db.ReservationDeposits.SingleAsync();
        Assert.Equal(DepositRules.RefundRequired, deposit.Status);
        Assert.Equal(300_000m, deposit.PaidAmount);
    }

    [Fact]
    public async Task Unknown_booking_is_kept_for_the_cashier()
    {
        var (db, _, service) = await Setup();
        var result = await Deliver(service, Body("7004", 300_000m, "VT DC999"));
        Assert.Equal(DepositRules.OutcomeNoMatch, result.Outcome);
        Assert.Equal(DepositRules.Pending, (await db.ReservationDeposits.SingleAsync()).Status);
        Assert.Contains(DepositRules.OutcomeNoMatch, CashierBankTransfersController.WarningOutcomes);
    }

    // ---- quote_locked (CLAUDE.md 6b) ----

    [Fact]
    public async Task Voucher_and_points_on_a_locked_quote_say_quote_locked()
    {
        var db = Db();
        var quote = Quote(subtotal: 100_000m);
        quote.Status = "PAYMENT_REQUESTED";
        db.CheckoutQuotes.Add(quote);
        await db.SaveChangesAsync();
        var controller = new MobileCheckoutController(db, null!, null!, null!, NullLogger<MobileCheckoutController>.Instance, null!);
        var result = await controller.ApplyVoucher(40, new MobileCheckoutController.VoucherRequest(7, null), default);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("quote_locked", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }

    // ---- helpers ----

    private static BillingDbContext Db() =>
        new(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReservationDeposit Deposit(string status = DepositRules.Pending, DateTime? due = null) => new()
    {
        ReservationDepositId = 11, ReservationId = 901, CustomerId = 7, BranchId = 1, PartySize = 6, Amount = 300_000m, Status = status,
        ReservedAtUtc = DateTime.UtcNow.AddDays(1), DueAtUtc = due ?? DateTime.UtcNow.AddMinutes(15),
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
    };

    private static CheckoutQuote Quote(decimal subtotal) => new()
    {
        CheckoutQuoteId = 40, QuoteCode = "Q-40", DiningSessionId = 55, DiningSessionCode = "DS-55", CustomerId = 7, BranchId = 1,
        TableId = 4, Status = "OPEN", Subtotal = subtotal, TotalAmount = subtotal, IdempotencyKey = "q-40", RequestHash = "h",
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
    };

    private static string Body(string id, decimal amount, string content) =>
        System.Text.Json.JsonSerializer.Serialize(new { id = long.Parse(id), gateway = "BIDV", transactionDate = "2026-10-27 20:00:00",
            accountNumber = Account, subAccount = (string?)null, code = (string?)null, content, transferType = "in",
            transferAmount = amount, accumulated = 0, referenceCode = "FT" + id });

    private static Task<SePayWebhookResult> Deliver(SePayWebhookService service, string body)
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        return service.ProcessAsync(body, ts, SePaySignature.Compute(Secret, ts, body), Now, default);
    }

    private static async Task<(BillingDbContext, CountingConfirmer, SePayWebhookService)> Setup(DateTime? due = null)
    {
        var db = Db();
        db.ReservationDeposits.Add(Deposit(due: due ?? Now.UtcDateTime.AddMinutes(10)));
        await db.SaveChangesAsync();
        var confirmer = new CountingConfirmer();
        var options = Options.Create(new SePayOptions { Enabled = true, WebhookSecret = Secret, AccountNumbers = Account });
        return (db, confirmer, new SePayWebhookService(db, options, confirmer, NullLogger<SePayWebhookService>.Instance));
    }

    private sealed class CountingConfirmer : ISePayPaymentConfirmer
    {
        public int Calls { get; private set; }
        public Task<Payment> ConfirmAsync(int paymentId, string providerTransactionId, CancellationToken ct)
        {
            Calls++;
            throw new InvalidOperationException("A deposit must never confirm a table payment.");
        }
    }
}
