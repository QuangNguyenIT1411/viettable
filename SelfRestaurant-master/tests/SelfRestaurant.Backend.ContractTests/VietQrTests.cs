using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class VietQrTests
{
    private const string Bin = "970418"; // BIDV
    private const string Account = "0123456789"; // test account, not a real one

    [Fact]
    public void Crc_is_crc16_ccitt_false()
    {
        Assert.Equal(0x29B1, VietQr.Crc16("123456789")); // standard check value
    }

    [Fact]
    public void Payload_follows_emvco_vietqr_fields_with_amount_and_content()
    {
        var payload = VietQr.BuildPayload(Bin, Account, 125000, "VT B1 DON46");
        var fields = Parse(payload);

        Assert.Equal(new[] { "00", "01", "38", "53", "54", "58", "62", "63" }, fields.Keys.ToArray());
        Assert.Equal("01", fields["00"]);
        Assert.Equal("12", fields["01"]);   // dynamic QR: amount is fixed
        var merchant = Parse(fields["38"]);
        Assert.Equal("A000000727", merchant["00"]);
        Assert.Equal("QRIBFTTA", merchant["02"]);
        var beneficiary = Parse(merchant["01"]);
        Assert.Equal(Bin, beneficiary["00"]);
        Assert.Equal(Account, beneficiary["01"]);
        Assert.Equal("704", fields["53"]);
        Assert.Equal("125000", fields["54"]);
        Assert.Equal("VN", fields["58"]);
        Assert.Equal("VT B1 DON46", Parse(fields["62"])["08"]);
        Assert.Equal(VietQr.Crc16(payload[..^4]).ToString("X4"), fields["63"]);
        Assert.EndsWith("6304" + fields["63"], payload);
    }

    [Fact]
    public void Service_rounds_the_amount_and_cleans_the_content()
    {
        var service = new BankTransferQrService(Options.Create(Configured()));
        var qr = service.Create(99999.6m, "Thanh toán đơn #46 – Bàn 1, chi nhánh Quận 1");

        Assert.Equal(100000m, qr.Amount);
        Assert.Equal("THANH TOAN DON 46 BAN 1 C", qr.Content);   // ASCII only, at most 25 characters
        Assert.Equal("100000", Parse(qr.Payload)["54"]);
        Assert.Equal(qr.Content, Parse(Parse(qr.Payload)["62"])["08"]);
        Assert.Equal("NGUYEN VAN TEST", qr.AccountName);
        var png = Convert.FromBase64String(qr.QrPngBase64);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);   // a PNG image, made locally
    }

    [Fact]
    public void Transfer_contents_identify_the_order_or_the_app_payment()
    {
        Assert.Equal("VT B1 DON46", BankTransferQrService.OrderContent(1, 46));
        Assert.Equal("VT B2 TT128", BankTransferQrService.PaymentContent(2, 128));
    }

    [Fact]
    public void Transfer_is_unavailable_until_every_account_setting_is_present()
    {
        Assert.False(new BankTransferOptions().IsConfigured);
        Assert.False(new BankTransferOptions { BankBin = Bin, BankName = "BIDV", AccountNumber = Account }.IsConfigured);
        Assert.False(new BankTransferOptions { BankBin = "BIDV", BankName = "BIDV", AccountNumber = Account, AccountName = "A" }.IsConfigured);
        Assert.True(Configured().IsConfigured);
        var service = new BankTransferQrService(Options.Create(new BankTransferOptions()));
        var error = Assert.Throws<PaymentDomainException>(() => service.Create(1000, "x"));
        Assert.Equal("bank_transfer_unavailable", error.Code);
    }

    [Theory]
    [InlineData("CASH", "CASH")]
    [InlineData("QR", "TRANSFER")]
    [InlineData("transfer", "TRANSFER")]
    [InlineData("VNPAY", "OTHER")]
    [InlineData(null, "OTHER")]
    public void Report_groups_payment_methods(string? method, string group) =>
        Assert.Equal(group, PaymentMethodGroups.Of(method));

    private static BankTransferOptions Configured() => new()
    {
        BankBin = Bin, BankName = "BIDV", AccountNumber = Account, AccountName = "Nguyen Van Test",
    };

    // EMVCo TLV: 2-char id, 2-digit length, value.
    private static Dictionary<string, string> Parse(string tlv)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < tlv.Length;)
        {
            var id = tlv.Substring(i, 2);
            var length = int.Parse(tlv.Substring(i + 2, 2));
            fields[id] = tlv.Substring(i + 4, length);
            i += 4 + length;
        }
        return fields;
    }
}
