using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class VnPayPaymentProviderTests
{
    private const string Secret = "unit-test-secret-that-is-never-used-outside-tests";

    [Fact]
    public async Task Create_buildsSignedVndSandboxUrlWithExactScaledAmount()
    {
        var provider = Provider();
        var session = await provider.CreateAsync(Payment(123_456m), "127.0.0.1", CancellationToken.None);
        var query = ParseQuery(new Uri(session.PaymentUrl).Query);

        Assert.Equal("VNPAY", session.ProviderName);
        Assert.Equal("VND", session.Currency);
        Assert.Equal("12345600", query["vnp_Amount"]);
        Assert.Equal("PAY-TEST-1", query["vnp_TxnRef"]);
        Assert.Equal(Sign(query), query["vnp_SecureHash"]);
    }

    [Fact]
    public void VerifyWebhook_rejectsInvalidSignature()
    {
        var values = SuccessfulWebhook(100_000);
        values["vnp_SecureHash"] = new string('0', 128);

        Assert.False(Provider().VerifyWebhook(values).SignatureValid);
    }

    [Fact]
    public void VerifyWebhook_mapsSignedSuccessAndPreservesProviderIdentity()
    {
        var values = SuccessfulWebhook(100_000);
        values["vnp_SecureHash"] = Sign(values);

        var result = Provider().VerifyWebhook(values);

        Assert.True(result.SignatureValid);
        Assert.Equal("PAY-TEST-1", result.ProviderReference);
        Assert.Equal("998877", result.ProviderTransactionId);
        Assert.Equal(100_000m, result.Amount);
        Assert.Equal("VND", result.Currency);
        Assert.Equal("SUCCEEDED", result.PaymentStatus);
    }

    [Theory]
    [InlineData("24", "CANCELLED")]
    [InlineData("11", "EXPIRED")]
    [InlineData("99", "FAILED")]
    public void VerifyWebhook_mapsProviderFailureCodes(string responseCode, string expectedStatus)
    {
        var values = SuccessfulWebhook(10_000); values["vnp_ResponseCode"] = responseCode; values["vnp_TransactionStatus"] = responseCode;
        values["vnp_SecureHash"] = Sign(values);

        Assert.Equal(expectedStatus, Provider().VerifyWebhook(values).PaymentStatus);
    }

    private static VnPayPaymentProvider Provider() => new(Options.Create(new VnPayOptions
    {
        Enabled = true,
        TmnCode = "UNITTEST",
        HashSecret = Secret,
        ReturnUrl = "https://example.test/api/payments/providers/vnpay/return"
    }));

    private static Payment Payment(decimal amount) => new() { PaymentCode = "PAY-TEST-1", Amount = amount };

    private static Dictionary<string, string> SuccessfulWebhook(long amount) => new(StringComparer.Ordinal)
    {
        ["vnp_Amount"] = checked(amount * 100).ToString(), ["vnp_CurrCode"] = "VND", ["vnp_ResponseCode"] = "00",
        ["vnp_TransactionStatus"] = "00", ["vnp_TransactionNo"] = "998877", ["vnp_TxnRef"] = "PAY-TEST-1"
    };

    private static string Sign(IReadOnlyDictionary<string, string> values)
    {
        var signed = values.Where(x => x.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        return VnPayPaymentProvider.Sign(VnPayPaymentProvider.BuildCanonicalQuery(signed), Secret);
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&')
        .Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => Uri.UnescapeDataString(x[1]).Replace('+', ' '));
}
