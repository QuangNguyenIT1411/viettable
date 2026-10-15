using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using QRCoder;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>
/// Receiving bank account for "Chuyển khoản". Configured only through environment variables
/// (Payments__BankTransfer__BankBin / BankName / AccountNumber / AccountName), never in code.
/// </summary>
public sealed class BankTransferOptions
{
    /// <summary>Napas bank BIN, e.g. 970418 for BIDV.</summary>
    public string BankBin { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    /// <summary>Account holder name in upper case without diacritics, as the bank shows it.</summary>
    public string AccountName { get; set; } = string.Empty;

    public bool IsConfigured =>
        BankBin.Length == 6 && BankBin.All(char.IsAsciiDigit)
        && AccountNumber.Length is >= 6 and <= 19 && AccountNumber.All(char.IsAsciiLetterOrDigit)
        && !string.IsNullOrWhiteSpace(BankName) && !string.IsNullOrWhiteSpace(AccountName);
}

/// <summary>What the customer scans: amount and transfer content are already filled in.</summary>
public sealed record TransferQrResponse(
    string BankName, string AccountNumber, string AccountName, decimal Amount, string Content, string Payload, string QrPngBase64);

public sealed class BankTransferQrService(IOptions<BankTransferOptions> options)
{
    public bool IsConfigured => options.Value.IsConfigured;

    public TransferQrResponse Create(decimal amount, string content)
    {
        var account = options.Value;
        if (!account.IsConfigured) throw new PaymentDomainException("bank_transfer_unavailable", "Chưa cấu hình tài khoản nhận chuyển khoản.");
        var vnd = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        var text = VietQr.CleanContent(content);
        var payload = VietQr.BuildPayload(account.BankBin, account.AccountNumber, vnd, text);
        return new TransferQrResponse(account.BankName.Trim(), account.AccountNumber.Trim(), account.AccountName.Trim().ToUpperInvariant(),
            vnd, text, payload, VietQr.PngBase64(payload));
    }

    /// <summary>Transfer content for a cashier checkout of one order, e.g. "VT B1 DON46".</summary>
    public static string OrderContent(int branchId, int orderId) => $"VT B{branchId} DON{orderId}";

    /// <summary>Transfer content for an app payment, e.g. "VT B1 TT128"; unique per payment attempt.</summary>
    public static string PaymentContent(int branchId, int paymentId) => $"VT B{branchId} TT{paymentId}";
}

/// <summary>
/// VietQR (Napas 247) payload, EMVCo merchant-presented QR format: every field is ID + 2-digit length + value,
/// ending with a CRC-16/CCITT-FALSE checksum. Built locally; no external QR service is called.
/// </summary>
public static class VietQr
{
    public const string NapasGuid = "A000000727";
    public const string TransferToAccount = "QRIBFTTA";
    public const int MaxContentLength = 25;

    public static string BuildPayload(string bankBin, string accountNumber, long amount, string content)
    {
        if (bankBin.Length != 6 || !bankBin.All(char.IsAsciiDigit)) throw new ArgumentException("Bank BIN must be 6 digits.", nameof(bankBin));
        if (string.IsNullOrWhiteSpace(accountNumber)) throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        var beneficiary = Field("00", bankBin) + Field("01", accountNumber.Trim());
        var merchant = Field("00", NapasGuid) + Field("01", beneficiary) + Field("02", TransferToAccount);
        var text = CleanContent(content);
        var body = new StringBuilder()
            .Append(Field("00", "01"))                        // payload format indicator
            .Append(Field("01", "12"))                        // dynamic QR: one amount, one transfer
            .Append(Field("38", merchant))
            .Append(Field("53", "704"))                       // VND
            .Append(Field("54", amount.ToString(CultureInfo.InvariantCulture)))
            .Append(Field("58", "VN"))
            .Append(text.Length > 0 ? Field("62", Field("08", text)) : string.Empty)
            .Append("6304")
            .ToString();
        return body + Crc16(body).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>Banks accept only plain ASCII letters, digits and spaces in the transfer content.</summary>
    public static string CleanContent(string? content)
    {
        var plain = new StringBuilder();
        foreach (var c in (content ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var ch = c is 'đ' ? 'd' : c is 'Đ' ? 'D' : c;
            plain.Append(char.IsAsciiLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
        }
        var text = string.Join(' ', plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= MaxContentLength ? text : text[..MaxContentLength].TrimEnd();
    }

    /// <summary>CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF), as EMVCo requires for field 63.</summary>
    public static ushort Crc16(string value)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(value))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    public static string PngBase64(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(10));
    }

    private static string Field(string id, string value) =>
        id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;
}
