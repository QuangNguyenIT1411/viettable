using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

public sealed class VnPayOptions
{
    public bool Enabled { get; set; }
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 15;
}

public sealed class VnPayPaymentProvider : IPaymentProvider
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
    private readonly VnPayOptions _options;

    public VnPayPaymentProvider(IOptions<VnPayOptions> options) => _options = options.Value;

    public string Name => "VNPAY";
    public bool IsConfigured => _options.Enabled
        && Uri.TryCreate(_options.PaymentUrl, UriKind.Absolute, out _)
        && Uri.TryCreate(_options.ReturnUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(_options.TmnCode)
        && !string.IsNullOrWhiteSpace(_options.HashSecret);

    public Task<ProviderPaymentSession> CreateAsync(Payment payment, string clientIpAddress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured) throw new PaymentProviderUnavailableException("VNPAY Sandbox chưa được cấu hình.");
        if (payment.Amount <= 0 || payment.Amount != decimal.Truncate(payment.Amount))
            throw new InvalidOperationException("VNPAY chỉ nhận số tiền VND nguyên dương.");

        var nowUtc = DateTime.UtcNow;
        var expiresUtc = nowUtc.AddMinutes(Math.Clamp(_options.ExpiryMinutes, 5, 60));
        var reference = payment.PaymentCode;
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = checked(((long)payment.Amount * 100L)).ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = FormatProviderTime(nowUtc),
            ["vnp_CurrCode"] = "VND",
            ["vnp_ExpireDate"] = FormatProviderTime(expiresUtc),
            ["vnp_IpAddr"] = NormalizeIpAddress(clientIpAddress),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Thanh toan VietTable {payment.PaymentCode}",
            ["vnp_OrderType"] = "180000",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_TxnRef"] = reference,
            ["vnp_Version"] = "2.1.0"
        };
        var canonical = BuildCanonicalQuery(parameters);
        var signature = Sign(canonical, _options.HashSecret);
        var separator = _options.PaymentUrl.Contains('?') ? "&" : "?";
        var url = $"{_options.PaymentUrl}{separator}{canonical}&vnp_SecureHash={signature}";
        return Task.FromResult(new ProviderPaymentSession(Name, reference, url, "VND", expiresUtc));
    }

    public ProviderWebhookResult VerifyWebhook(IReadOnlyDictionary<string, string> values)
    {
        if (!IsConfigured || !values.TryGetValue("vnp_SecureHash", out var suppliedHash) || string.IsNullOrWhiteSpace(suppliedHash))
            return new(false, null, null, null, null, null, "invalid_signature");

        var signedValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in values)
            if (pair.Key.StartsWith("vnp_", StringComparison.Ordinal) && pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
                signedValues[pair.Key] = pair.Value;
        var expectedBytes = Encoding.ASCII.GetBytes(Sign(BuildCanonicalQuery(signedValues), _options.HashSecret));
        byte[] suppliedBytes;
        try { suppliedBytes = Encoding.ASCII.GetBytes(suppliedHash.Trim().ToLowerInvariant()); }
        catch { return new(false, null, null, null, null, null, "invalid_signature"); }
        if (expectedBytes.Length != suppliedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
            return new(false, null, null, null, null, null, "invalid_signature");

        values.TryGetValue("vnp_TxnRef", out var reference);
        values.TryGetValue("vnp_TransactionNo", out var transactionId);
        values.TryGetValue("vnp_CurrCode", out var currency);
        values.TryGetValue("vnp_ResponseCode", out var responseCode);
        values.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
        decimal? amount = null;
        if (values.TryGetValue("vnp_Amount", out var providerAmount)
            && long.TryParse(providerAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var scaledAmount)
            && scaledAmount >= 0 && scaledAmount % 100 == 0)
            amount = scaledAmount / 100m;
        var status = responseCode == "00" && transactionStatus == "00" ? "SUCCEEDED"
            : responseCode == "24" ? "CANCELLED"
            : responseCode == "11" ? "EXPIRED"
            : "FAILED";
        return new(true, reference, transactionId, amount, currency, status, status == "SUCCEEDED" ? null : responseCode);
    }

    public static string BuildCanonicalQuery(IEnumerable<KeyValuePair<string, string>> values) =>
        string.Join("&", values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));

    public static string Sign(string canonical, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string FormatProviderTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTimeZone).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string NormalizeIpAddress(string value) => IPAddress.TryParse(value, out var ip) ? ip.ToString() : "127.0.0.1";
    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}
