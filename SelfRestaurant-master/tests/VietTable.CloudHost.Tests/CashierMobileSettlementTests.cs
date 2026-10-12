using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class CashierMobileSettlementTests
{
    private static (string Channel, string Status)[] Payments(params (string, string)[] rows) => rows;

    [Fact]
    public void Session_without_payment_request_gets_a_quote_on_the_customers_behalf()
        => Assert.Equal(MobileSettlementStep.CreateQuote, CashierMobileSettlementService.Decide(null, false, Payments()));

    [Fact]
    public void Requested_quote_without_payment_gets_one_onsite_payment()
        => Assert.Equal(MobileSettlementStep.CreatePayment,
            CashierMobileSettlementService.Decide("PAYMENT_REQUESTED", false, Payments(("ONSITE", "EXPIRED"), ("ONLINE", "FAILED"))));

    [Fact]
    public void Pending_onsite_payment_from_the_app_is_confirmed_not_duplicated()
        => Assert.Equal(MobileSettlementStep.ConfirmPending,
            CashierMobileSettlementService.Decide("PAYMENT_REQUESTED", false, Payments(("ONSITE", "PENDING"))));

    [Theory]
    [InlineData("PAID")]
    [InlineData("PAYMENT_REQUESTED")]
    public void Already_collected_payment_is_replayed_never_charged_twice(string quoteStatus)
        => Assert.Equal(MobileSettlementStep.AlreadyPaid,
            CashierMobileSettlementService.Decide(quoteStatus, false, Payments(("ONSITE", "SUCCEEDED"))));

    [Fact]
    public void Online_payment_in_progress_blocks_the_cashier()
    {
        var error = Assert.Throws<PaymentDomainException>(() =>
            CashierMobileSettlementService.Decide("PAYMENT_REQUESTED", false, Payments(("ONLINE", "PENDING"))));
        Assert.Equal("online_payment_in_progress", error.Code);
    }

    [Fact]
    public void Split_bill_must_be_collected_part_by_part()
    {
        var error = Assert.Throws<PaymentDomainException>(() =>
            CashierMobileSettlementService.Decide("PAYMENT_REQUESTED", true, Payments(("ONSITE", "PENDING"))));
        Assert.Equal("split_bill_in_app", error.Code);
    }

    [Fact]
    public void Several_pending_payments_are_never_guessed()
    {
        var error = Assert.Throws<PaymentDomainException>(() =>
            CashierMobileSettlementService.Decide("PAYMENT_REQUESTED", false, Payments(("ONSITE", "PENDING"), ("ONSITE", "PROCESSING"))));
        Assert.Equal("payment_state_conflict", error.Code);
    }

    [Fact]
    public void Paid_quote_without_successful_payment_is_a_conflict()
    {
        var error = Assert.Throws<PaymentDomainException>(() => CashierMobileSettlementService.Decide("PAID", false, Payments()));
        Assert.Equal("payment_state_conflict", error.Code);
    }
}
