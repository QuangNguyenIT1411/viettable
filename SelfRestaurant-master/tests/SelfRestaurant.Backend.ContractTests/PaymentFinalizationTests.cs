using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class PaymentFinalizationTests
{
    private static CheckoutQuote Quote() => new() { CheckoutQuoteId = 1, CustomerId = 2, DiningSessionId = 3, TotalAmount = 70000, Status = "PAYMENT_REQUESTED" };
    private static Payment Pending() => new() { PaymentId = 4, CheckoutQuoteId = 1, CustomerId = 2, DiningSessionId = 3, Amount = 70000, Status = "PENDING", Currency = "VND" };

    [Theory]
    [InlineData("FAILED")]
    [InlineData("CANCELLED")]
    [InlineData("EXPIRED")]
    [InlineData("PROCESSING")]
    public void Only_pending_transitions_to_success(string status)
    {
        var payment = Pending(); payment.Status = status;
        Assert.Equal("payment_state_conflict", Assert.Throws<PaymentDomainException>(() => PaymentFinalizationService.ValidatePendingTransition(payment, Quote(), null)).Code);
    }

    [Fact]
    public void Persisted_amount_must_match_obligation()
    {
        var payment = Pending(); payment.Amount = 1;
        Assert.Equal("payment_amount_mismatch", Assert.Throws<PaymentDomainException>(() => PaymentFinalizationService.ValidatePendingTransition(payment, Quote(), null)).Code);
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("session")]
    [InlineData("customer")]
    [InlineData("quote")]
    public void Payment_must_match_its_quote(string mismatch)
    {
        var payment = Pending();
        switch (mismatch) { case "currency": payment.Currency = "USD"; break; case "session": payment.DiningSessionId++; break; case "customer": payment.CustomerId++; break; case "quote": payment.CheckoutQuoteId++; break; }
        Assert.Equal("payment_session_mismatch", Assert.Throws<PaymentDomainException>(() => PaymentFinalizationService.ValidatePendingTransition(payment, Quote(), null)).Code);
    }

    [Fact]
    public void Replaced_split_attempt_cannot_be_collected()
    {
        var split = new SplitBill(); split.Parts.Add(new SplitBillPart { PaymentId = 5, Amount = 70000, Status = "PENDING" });
        Assert.Equal("payment_attempt_replaced", Assert.Throws<PaymentDomainException>(() => PaymentFinalizationService.ValidatePendingTransition(Pending(), Quote(), split)).Code);
    }

    [Fact]
    public void Current_split_attempt_uses_part_amount_not_full_quote()
    {
        var payment = Pending(); payment.Amount = 35000;
        var split = new SplitBill(); var part = new SplitBillPart { PaymentId = payment.PaymentId, Amount = 35000, Status = "PENDING" }; split.Parts.Add(part);
        Assert.Same(part, PaymentFinalizationService.ValidatePendingTransition(payment, Quote(), split));
        Assert.Null(PaymentFinalizationService.ValidatePendingTransition(Pending(), Quote(), null));
    }
}
