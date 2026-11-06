using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>Pure rules of the waitlist (owner 06/11); the controllers only load, call these and save.</summary>
public static class WaitlistRules
{
    public const string Waiting = "Waiting";
    public const string Offered = "Offered";
    public const string Accepted = "Accepted";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string Closed = "Closed";

    /// <summary>The held booking's status; it occupies the table until <see cref="Reservation.HoldExpiresAtUtc"/>.</summary>
    public const string HoldStatus = "WaitlistHold";

    public const int DefaultOfferMinutes = 10;
    public const int MaxActivePerCustomer = 3;
    public const int MaxWindowHours = 6;

    public static bool IsActive(string status) => status is Waiting or Offered;

    /// <summary>Null when the request can join; otherwise the message for the guest.</summary>
    public static string? WhyNotJoinable(DateTime windowStartUtc, DateTime windowEndUtc, int partySize, int activeEntries, DateTime nowUtc)
    {
        if (partySize is < 1 or > 30) return "Số khách phải từ 1 đến 30.";
        if (windowEndUtc < windowStartUtc) return "Giờ kết thúc phải sau giờ bắt đầu.";
        if ((windowEndUtc - windowStartUtc).TotalHours > MaxWindowHours) return $"Khoảng giờ chờ tối đa {MaxWindowHours} tiếng.";
        if (windowEndUtc <= nowUtc.AddMinutes(15)) return "Khoảng giờ đã qua. Vui lòng chọn giờ khác.";
        if (windowStartUtc > nowUtc.AddDays(90)) return "Chỉ có thể chờ bàn trong 90 ngày tới.";
        if (activeEntries >= MaxActivePerCustomer) return $"Bạn đang chờ {activeEntries} lượt. Huỷ bớt rồi đăng ký lại.";
        return null;
    }

    /// <summary>An offer whose time to answer has passed (the held table goes to the next guest).</summary>
    public static bool OfferExpired(WaitlistEntry entry, DateTime nowUtc) =>
        entry.Status == Offered && entry.OfferExpiresAtUtc is not null && entry.OfferExpiresAtUtc <= nowUtc;

    /// <summary>A guest still waiting whose whole window is too close or over: nothing can be offered any more.</summary>
    public static bool WindowOver(WaitlistEntry entry, DateTime nowUtc) =>
        entry.Status == Waiting && entry.WindowEndUtc <= nowUtc.AddMinutes(15);

    /// <summary>Null when the guest may accept now; otherwise why not.</summary>
    public static string? WhyNotAcceptable(WaitlistEntry entry, DateTime nowUtc) => entry.Status switch
    {
        Offered when OfferExpired(entry, nowUtc) => "Đã hết thời gian giữ bàn. Bàn đã được mời cho khách kế tiếp.",
        Offered => null,
        Accepted => "Bạn đã nhận bàn này rồi.",
        Waiting => "Chưa có bàn cho bạn. Chúng tôi sẽ báo khi có.",
        Expired => "Đã hết thời gian giữ bàn.",
        Cancelled => "Bạn đã huỷ chờ.",
        _ => "Lượt chờ đã kết thúc."
    };

    /// <summary>1-based place among guests still waiting at the same branch on the same restaurant day, by sign-up.</summary>
    public static int Position(WaitlistEntry entry, IEnumerable<WaitlistEntry> branchEntries, TimeZoneInfo zone)
    {
        if (entry.Status != Waiting) return 0;
        var day = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entry.WindowStartUtc, DateTimeKind.Utc), zone).Date;
        return 1 + branchEntries.Count(x => x.WaitlistEntryId != entry.WaitlistEntryId && x.Status == Waiting
            && x.BranchId == entry.BranchId
            && TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.WindowStartUtc, DateTimeKind.Utc), zone).Date == day
            && (x.CreatedAtUtc < entry.CreatedAtUtc || (x.CreatedAtUtc == entry.CreatedAtUtc && x.WaitlistEntryId < entry.WaitlistEntryId)));
    }

    /// <summary>Guests in the order they are offered tables: sign-up time, then id.</summary>
    public static IReadOnlyList<WaitlistEntry> Queue(IEnumerable<WaitlistEntry> entries, DateTime nowUtc) =>
        entries.Where(x => x.Status == Waiting && !WindowOver(x, nowUtc))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.WaitlistEntryId).ToArray();
}
