namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>
/// The guest's answer to the booking reminder (owner 06/11): "Sẽ đến" or "Huỷ lịch". The cashier sees
/// "Khách xác nhận sẽ đến" / "Chưa phản hồi" on the booking list.
/// </summary>
public static class ArrivalRules
{
    public const string None = "None";
    public const string WillCome = "WillCome";
    public const string Cancelled = "Cancelled";

    private static readonly HashSet<string> Answerable = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "Confirmed", "PendingDeposit"
    };

    /// <summary>Null when "Sẽ đến" is accepted; otherwise the reason shown to the guest.</summary>
    public static string? WhyNotConfirmable(string status, DateTime reservedAtUtc, int arrivalWindowMinutes, DateTime nowUtc)
    {
        if (!Answerable.Contains(status ?? ""))
            return status switch
            {
                "Cancelled" => "Đặt bàn đã huỷ.",
                "Rejected" => "Nhà hàng đã từ chối đặt bàn này.",
                "NoShow" => "Đặt bàn đã quá giờ hẹn.",
                "CheckingIn" or "CheckedIn" or "Completed" => "Bạn đã vào bàn.",
                _ => "Trạng thái đặt bàn không cho phép xác nhận."
            };
        if (nowUtc > reservedAtUtc.AddMinutes(Math.Max(0, arrivalWindowMinutes)))
            return "Đặt bàn đã quá giờ hẹn.";
        return null;
    }
}
