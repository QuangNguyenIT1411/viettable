namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>The two ways a guest pays today ("Tiền mặt", "Chuyển khoản"); anything older or online is "Khác".</summary>
public static class PaymentMethodGroups
{
    public const string Cash = "CASH";
    public const string Transfer = "TRANSFER";
    public const string Other = "OTHER";

    public static string Of(string? method) => (method ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "CASH" => Cash,
        "QR" or "TRANSFER" or "BANK" or "BANK_TRANSFER" => Transfer,
        _ => Other,
    };
}
