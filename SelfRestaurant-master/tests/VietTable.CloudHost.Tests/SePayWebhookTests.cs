using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class SePayWebhookTests
{
    private const string Secret = "test-secret-not-real";
    private const string Account = "96247TEST01";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);

    // ---- Signature (HMAC-SHA256 over "{timestamp}.{raw body}") ----

    [Fact]
    public void Signature_matches_only_the_exact_body_secret_and_a_fresh_timestamp()
    {
        var body = Body("1", 100_000m, "VT B1 TT128");
        var ts = Now.ToUnixTimeSeconds().ToString();
        var good = SePaySignature.Compute(Secret, ts, body);
        Assert.StartsWith("sha256=", good);
        Assert.True(SePaySignature.IsValid(Secret, ts, good, body, Now, 300));
        Assert.True(SePaySignature.IsValid(Secret, ts, good.ToUpperInvariant().Replace("SHA256=", "sha256="), body, Now, 300));
        Assert.False(SePaySignature.IsValid(Secret, ts, good, body.Replace("100000", "900000"), Now, 300)); // body changed
        Assert.False(SePaySignature.IsValid("other", ts, good, body, Now, 300));                            // wrong secret
        Assert.False(SePaySignature.IsValid(Secret, ts, good, body, Now.AddSeconds(301), 300));             // replay
        Assert.False(SePaySignature.IsValid(Secret, null, good, body, Now, 300));
        Assert.False(SePaySignature.IsValid(Secret, ts, null, body, Now, 300));
        Assert.False(SePaySignature.IsValid(Secret, "abc", good, body, Now, 300));
    }

    // ---- Transfer content → payment reference ----

    [Theory]
    [InlineData("VT B1 TT128", 1, 128)]
    [InlineData("vt b1 tt128", 1, 128)]
    [InlineData("VTB1TT128", 1, 128)]
    [InlineData("MBVCB.123456.VT B1 TT128.CT tu 0123 toi 8830", 1, 128)]
    [InlineData("NGUYEN VAN A chuyen tien VT-B12-TT4501 cam on", 12, 4501)]
    [InlineData("VT B1 TT128 VT B1 TT128", 1, 128)]
    public void Payment_reference_survives_bank_formatting(string content, int branch, int payment)
    {
        var reference = SePayContentParser.Parse(content);
        Assert.NotNull(reference);
        Assert.Equal(branch, reference!.BranchId);
        Assert.Equal(payment, reference.PaymentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chuyen tien an trua")]
    [InlineData("VT B1 TT128 VT B1 TT129")] // two different payments: ambiguous, cashier decides
    [InlineData("XVT B1 TT128")]
    [InlineData("VT B1 DON12")]           // counter order: not automatic in this round
    [InlineData("VT BI TT28")]            // letter I is not the digit 1 (owner's question 25/10)
    [InlineData("VT Bl TT28")]            // letter l is not the digit 1
    [InlineData("an trua")]
    public void No_or_ambiguous_reference_is_never_matched(string? content) => Assert.Null(SePayContentParser.Parse(content));

    // ---- Matching rules ----

    [Fact]
    public void Confirms_only_a_pending_transfer_of_that_branch_with_enough_money()
    {
        var reference = new SePayContentParser.PaymentReference(1, 128);
        Assert.Equal(SePayMatchRules.AutoConfirmed, SePayMatchRules.Decide(reference, Pending(), 1, 100_000m));
        Assert.Equal(SePayMatchRules.AutoConfirmed, SePayMatchRules.Decide(reference, Pending(), 1, 120_000m)); // overpaid
        Assert.Equal(SePayMatchRules.AmountTooLow, SePayMatchRules.Decide(reference, Pending(), 1, 99_999m));
        Assert.Equal(SePayMatchRules.BranchMismatch, SePayMatchRules.Decide(reference, Pending(), 2, 100_000m));
        Assert.Equal(SePayMatchRules.NoMatch, SePayMatchRules.Decide(null, Pending(), 1, 100_000m));
        Assert.Equal(SePayMatchRules.NoMatch, SePayMatchRules.Decide(reference, null, null, 100_000m));
        var cash = Pending(); cash.Method = "CASH";
        Assert.Equal(SePayMatchRules.NotTransfer, SePayMatchRules.Decide(reference, cash, 1, 100_000m));
        var done = Pending(); done.Status = "SUCCEEDED";
        Assert.Equal(SePayMatchRules.PaymentNotPending, SePayMatchRules.Decide(reference, done, 1, 100_000m));
    }

    // ---- Whole webhook ----

    [Fact]
    public async Task Wrong_signature_is_401_and_writes_nothing()
    {
        var (db, confirmer, service) = await Setup();
        var body = Body("9001", 100_000m, "VT B1 TT128");
        var result = await service.ProcessAsync(body, Now.ToUnixTimeSeconds().ToString(), "sha256=00", Now, default);
        Assert.Equal(401, result.StatusCode);
        Assert.Empty(db.BankTransferEvents);
        Assert.Equal(0, confirmer.Calls);
        Assert.Equal("PENDING", (await db.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Disabled_flag_verifies_but_processes_nothing()
    {
        var (db, confirmer, service) = await Setup(enabled: false);
        var result = await Deliver(service, Body("9002", 100_000m, "VT B1 TT128"));
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(SePayWebhookService.Disabled, result.Outcome);
        Assert.Empty(db.BankTransferEvents);
        Assert.Equal(0, confirmer.Calls);
    }

    [Fact]
    public async Task Matching_transfer_is_confirmed_once_even_when_delivered_twice()
    {
        var (db, confirmer, service) = await Setup();
        var body = Body("9003", 100_000m, "IBFT VT B1 TT128");
        var first = await Deliver(service, body);
        var second = await Deliver(service, body);

        Assert.Equal(SePayMatchRules.AutoConfirmed, first.Outcome);
        Assert.Equal(7, first.CustomerId);
        Assert.Equal(55, first.DiningSessionId);
        Assert.Equal("SUCCEEDED", first.PaymentStatus);
        Assert.Equal(SePayWebhookService.Duplicate, second.Outcome);
        Assert.Equal(1, confirmer.Calls);
        Assert.Equal("SEPAY:9003", confirmer.LastProviderTransactionId);
        var row = Assert.Single(db.BankTransferEvents);
        Assert.Equal(SePayMatchRules.AutoConfirmed, row.Outcome);
        Assert.Equal(128, row.PaymentId);
        Assert.Equal(1, row.BranchId);
        Assert.False(row.Overpaid);
    }

    [Fact]
    public async Task Overpaid_transfer_is_confirmed_and_flagged()
    {
        var (db, _, service) = await Setup();
        Assert.Equal(SePayMatchRules.AutoConfirmed, (await Deliver(service, Body("9004", 150_000m, "VT B1 TT128"))).Outcome);
        Assert.True((await db.BankTransferEvents.SingleAsync()).Overpaid);
    }

    [Theory]
    [InlineData(99_000, "VT B1 TT128", "AMOUNT_TOO_LOW")]
    [InlineData(100_000, "VT B2 TT128", "BRANCH_MISMATCH")]
    [InlineData(100_000, "chuyen khoan an toi", "NO_MATCH")]
    [InlineData(100_000, "VT B1 TT999", "NO_MATCH")]
    public async Task Mismatch_is_kept_for_the_cashier_and_never_confirmed(int amount, string content, string outcome)
    {
        var (db, confirmer, service) = await Setup();
        var result = await Deliver(service, Body("9005", amount, content));
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(0, confirmer.Calls);
        Assert.Equal(outcome, (await db.BankTransferEvents.SingleAsync()).Outcome);
        Assert.Equal("PENDING", (await db.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Unknown_receiving_account_and_outgoing_transfers_are_ignored()
    {
        var (db, confirmer, service) = await Setup();
        Assert.Equal(SePayWebhookService.Ignored, (await Deliver(service, Body("9006", 100_000m, "VT B1 TT128", account: "1234567"))).Outcome);
        Assert.Equal(SePayWebhookService.Ignored, (await Deliver(service, Body("9007", 100_000m, "VT B1 TT128", type: "out"))).Outcome);
        Assert.Empty(db.BankTransferEvents);
        Assert.Equal(0, confirmer.Calls);
    }

    [Fact]
    public async Task Virtual_account_in_subAccount_is_accepted()
    {
        var (_, confirmer, service) = await Setup();
        var result = await Deliver(service, Body("9008", 100_000m, "VT B1 TT128", account: "8830150124", subAccount: Account));
        Assert.Equal(SePayMatchRules.AutoConfirmed, result.Outcome);
        Assert.Equal(1, confirmer.Calls);
    }

    [Fact]
    public async Task Payment_already_settled_by_the_cashier_becomes_a_warning()
    {
        var (db, confirmer, service) = await Setup();
        confirmer.Throw = true; // the cashier confirmed by hand a moment before
        var result = await Deliver(service, Body("9009", 100_000m, "VT B1 TT128"));
        Assert.Equal(SePayMatchRules.PaymentNotPending, result.Outcome);
        Assert.Equal(SePayMatchRules.PaymentNotPending, (await db.BankTransferEvents.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task An_unfinished_earlier_delivery_is_finished_by_the_retry()
    {
        var (db, confirmer, service) = await Setup();
        db.BankTransferEvents.Add(new BankTransferEvent { ProviderTransactionId = "9010", AccountNumber = Account,
            Amount = 100_000m, Content = "VT B1 TT128", Outcome = "RECEIVED", ReceivedAtUtc = Now.UtcDateTime });
        await db.SaveChangesAsync();
        var result = await Deliver(service, Body("9010", 100_000m, "VT B1 TT128"));
        Assert.Equal(SePayMatchRules.AutoConfirmed, result.Outcome);
        Assert.Equal(1, confirmer.Calls);
        Assert.Single(db.BankTransferEvents);
    }

    // Incident 25/10 (SePay 41419): confirming cleared the context's change tracker (reconciliation failed) and the
    // row's outcome update was lost, leaving it RECEIVED although the payment was confirmed.
    [Fact]
    public async Task Outcome_is_saved_even_when_confirming_clears_the_change_tracker()
    {
        var (db, confirmer, service) = await Setup();
        confirmer.ClearTrackerAfterConfirm = true;
        var result = await Deliver(service, Body("9012", 100_000m, "VT B1 TT128"));
        Assert.Equal(SePayMatchRules.AutoConfirmed, result.Outcome);
        db.ChangeTracker.Clear();
        var row = await db.BankTransferEvents.SingleAsync();
        Assert.Equal(SePayMatchRules.AutoConfirmed, row.Outcome);
        Assert.Equal(128, row.PaymentId);
        Assert.NotNull(row.ProcessedAtUtc);
    }

    // Incident 25/10: the session was locked for another quote; money must not be confirmed, the cashier is warned.
    [Fact]
    public async Task Session_no_longer_waiting_for_this_quote_becomes_a_warning()
    {
        var (db, confirmer, service) = await Setup();
        confirmer.ThrowCode = "session_not_payable";
        var result = await Deliver(service, Body("9013", 100_000m, "VT B1 TT128"));
        Assert.Equal(SePayMatchRules.SessionNotPayable, result.Outcome);
        Assert.Equal(SePayMatchRules.SessionNotPayable, (await db.BankTransferEvents.SingleAsync()).Outcome);
        Assert.Contains(SePayMatchRules.SessionNotPayable, CashierBankTransfersController.WarningOutcomes);
    }

    [Fact]
    public void Money_is_confirmed_only_while_orders_waits_for_that_exact_quote()
    {
        static OrdersApiClient.SessionPaymentState State(string status, int? quote, bool ended = false) => new(34, status, quote, null, ended);
        Assert.True(PaymentFinalizationService.SessionAwaitsQuote(State("PAYMENT_REQUESTED", 35), 35));
        Assert.False(PaymentFinalizationService.SessionAwaitsQuote(State("PAYMENT_REQUESTED", 36), 35)); // the incident
        Assert.False(PaymentFinalizationService.SessionAwaitsQuote(State("ACTIVE", null), 35));          // unlocked by Back
        Assert.False(PaymentFinalizationService.SessionAwaitsQuote(State("PAYMENT_REQUESTED", 35, ended: true), 35));
        Assert.False(PaymentFinalizationService.SessionAwaitsQuote(null, 35));
    }

    [Fact]
    public async Task Enabled_without_a_secret_refuses_everything()
    {
        var (db, confirmer, service) = await Setup(secret: null);
        var result = await Deliver(service, Body("9011", 100_000m, "VT B1 TT128"));
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(db.BankTransferEvents);
        Assert.Equal(0, confirmer.Calls);
    }

    // ---- helpers ----

    private static Payment Pending() => new() { PaymentId = 128, PaymentCode = "PAY-128", CheckoutQuoteId = 40, DiningSessionId = 55,
        CustomerId = 7, Amount = 100_000m, Channel = "ONSITE", Method = "TRANSFER", Status = "PENDING", Currency = "VND",
        IdempotencyKey = "k-128", RequestHash = "h" };

    private static string Body(string id, decimal amount, string content, string? account = null, string? subAccount = null, string type = "in") =>
        System.Text.Json.JsonSerializer.Serialize(new { id = long.Parse(id), gateway = "BIDV", transactionDate = "2026-10-19 20:00:00",
            accountNumber = account ?? Account, subAccount, code = (string?)null, content, transferType = type,
            transferAmount = amount, accumulated = 0, referenceCode = "FT" + id });

    private static Task<SePayWebhookResult> Deliver(SePayWebhookService service, string body)
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        return service.ProcessAsync(body, ts, SePaySignature.Compute(Secret, ts, body), Now, default);
    }

    private static async Task<(BillingDbContext, FakeConfirmer, SePayWebhookService)> Setup(bool enabled = true, string? secret = Secret)
    {
        var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CheckoutQuotes.Add(new CheckoutQuote { CheckoutQuoteId = 40, QuoteCode = "Q-40", DiningSessionId = 55, DiningSessionCode = "DS-55",
            CustomerId = 7, BranchId = 1, TableId = 4, Status = "PAYMENT_REQUESTED", Subtotal = 100_000m, TotalAmount = 100_000m,
            IdempotencyKey = "q-40", RequestHash = "h" });
        db.Payments.Add(Pending());
        await db.SaveChangesAsync();
        var confirmer = new FakeConfirmer(db);
        var options = Options.Create(new SePayOptions { Enabled = enabled, WebhookSecret = secret, AccountNumbers = Account + ", 0000TEST" });
        return (db, confirmer, new SePayWebhookService(db, options, confirmer, NullLogger<SePayWebhookService>.Instance));
    }

    private sealed class FakeConfirmer(BillingDbContext db) : ISePayPaymentConfirmer
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        public string? ThrowCode { get; set; }
        public bool ClearTrackerAfterConfirm { get; set; }
        public string? LastProviderTransactionId { get; private set; }

        public async Task<Payment> ConfirmAsync(int paymentId, string providerTransactionId, CancellationToken ct)
        {
            Calls++;
            LastProviderTransactionId = providerTransactionId;
            if (Throw) throw new PaymentDomainException("payment_state_conflict", "already settled");
            if (ThrowCode is not null) throw new PaymentDomainException(ThrowCode, "not payable");
            var payment = await db.Payments.SingleAsync(x => x.PaymentId == paymentId, ct);
            payment.Status = "SUCCEEDED"; payment.ProviderName = "SEPAY"; payment.ProviderTransactionId = providerTransactionId;
            await db.SaveChangesAsync(ct);
            if (ClearTrackerAfterConfirm) db.ChangeTracker.Clear(); // what a failed reconciliation does
            return payment;
        }
    }
}
