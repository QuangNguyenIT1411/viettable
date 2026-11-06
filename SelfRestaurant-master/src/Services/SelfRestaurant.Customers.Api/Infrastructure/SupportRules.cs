using System.Globalization;
using System.Text.RegularExpressions;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>Pure rules of the support chat (owner 06/11); controllers load, call these and save.</summary>
public static class SupportRules
{
    public const string Waiting = "Waiting";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";

    public const string Customer = "CUSTOMER";
    public const string Staff = "STAFF";
    public const string Auto = "AUTO";

    public const int MaxBodyLength = 1000;
    /// <summary>Anti-flood: at most this many customer messages in <see cref="BurstWindow"/>.</summary>
    public const int BurstMessages = 5;
    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(30);
    /// <summary>At most this many new conversations per customer per hour.</summary>
    public const int NewConversationsPerHour = 3;

    public sealed record Topic(string Code, string Label, string? ContextType);

    /// <summary>The topics the app shows as cards, in order. ContextType = what the customer picks from their own data.</summary>
    public static readonly IReadOnlyList<Topic> Topics = new[]
    {
        new Topic("DEPOSIT_TRANSFER", "Đã chuyển khoản đặt cọc nhưng chưa được xác nhận", "DEPOSIT"),
        new Topic("BILL_TRANSFER", "Đã chuyển khoản thanh toán nhưng chưa được xác nhận", "PAYMENT"),
        new Topic("BOOKING_CHANGE", "Đổi / huỷ đặt bàn", "RESERVATION"),
        new Topic("DEPOSIT_REFUND", "Hoàn tiền cọc", "DEPOSIT"),
        new Topic("BILL_WRONG", "Hoá đơn sai món / sai tiền", "INVOICE"),
        new Topic("LOYALTY_PROMO", "Điểm thưởng / voucher / khuyến mãi không áp được", "INVOICE"),
        new Topic("OTHER", "Khác", null),
    };

    public static Topic? FindTopic(string? code) => Topics.FirstOrDefault(t => string.Equals(t.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static readonly IReadOnlySet<string> ContextTypes = new HashSet<string>(StringComparer.Ordinal) { "NONE", "RESERVATION", "DEPOSIT", "PAYMENT", "INVOICE" };

    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Dangerous = new(@"<\s*/?\s*(script|iframe|object|embed|style)\b|javascript\s*:|on\w+\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Plain text only: HTML tags removed, control characters dropped (new lines kept), trimmed. Returns null and the
    /// reason when nothing is left, it is too long, or it looks like script.
    /// </summary>
    public static (string? Body, string? Error) CleanBody(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, "Vui lòng nhập nội dung tin nhắn.");
        if (Dangerous.IsMatch(raw)) return (null, "Tin nhắn có nội dung không được phép.");
        var text = Tags.Replace(raw, "");
        text = new string(text.Where(c => c == '\n' || !char.IsControl(c)).ToArray()).Replace("\r", "");
        text = Spaces.Replace(text, " ").Trim();
        if (text.Length == 0) return (null, "Vui lòng nhập nội dung tin nhắn.");
        if (text.Length > MaxBodyLength) return (null, $"Tin nhắn tối đa {MaxBodyLength} ký tự.");
        return (text, null);
    }

    /// <summary>Null when another customer message may be sent now; otherwise the reason.</summary>
    public static string? WhyTooFast(IEnumerable<DateTime> recentCustomerMessagesUtc, DateTime nowUtc)
    {
        var count = recentCustomerMessagesUtc.Count(t => t > nowUtc - BurstWindow);
        return count >= BurstMessages ? "Bạn gửi hơi nhanh. Vui lòng đợi vài giây rồi gửi tiếp." : null;
    }

    public static string? WhyNoNewConversation(int createdInLastHour) =>
        createdInLastHour >= NewConversationsPerHour
            ? "Bạn đã mở nhiều cuộc trò chuyện trong 1 giờ qua. Vui lòng nhắn tiếp trong cuộc đang có."
            : null;

    public sealed record Hours(TimeOnly From, TimeOnly To);

    /// <summary>Reads "07:00 - 22:00", "7h-22h", "07:00–22:30" (branch opening hours as typed). Null when unreadable.</summary>
    public static Hours? ParseHours(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, @"(\d{1,2})\s*(?:[:hg]\s*(\d{2})?)?\s*(?:-|–|—|~|đến|to)\s*(\d{1,2})\s*(?:[:hg]\s*(\d{2})?)?", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        static TimeOnly? T(string h, string m)
        {
            var hour = int.Parse(h, CultureInfo.InvariantCulture);
            var minute = string.IsNullOrEmpty(m) ? 0 : int.Parse(m, CultureInfo.InvariantCulture);
            if (hour == 24 && minute == 0) return new TimeOnly(23, 59);
            return hour is >= 0 and <= 23 && minute is >= 0 and <= 59 ? new TimeOnly(hour, minute) : null;
        }
        var from = T(m.Groups[1].Value, m.Groups[2].Value);
        var to = T(m.Groups[3].Value, m.Groups[4].Value);
        return from is null || to is null || from == to ? null : new Hours(from.Value, to.Value);
    }

    public static bool IsOpen(Hours hours, TimeOnly local) =>
        hours.From < hours.To ? local >= hours.From && local < hours.To : local >= hours.From || local < hours.To; // past midnight

    /// <summary>
    /// The automatic reply after a customer message. Inside support hours: staff answer in a few minutes. Outside: when
    /// the branch opens again. Unknown hours: no promise about time.
    /// </summary>
    public static string AutoReply(Hours? hours, DateTime nowUtc, TimeZoneInfo zone)
    {
        if (hours is null) return "Tin nhắn tự động: Nhân viên đã nhận được tin nhắn và sẽ trả lời bạn sớm nhất có thể.";
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        if (IsOpen(hours, TimeOnly.FromDateTime(local))) return "Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút.";
        var opensToday = TimeOnly.FromDateTime(local) < hours.From;
        var when = opensToday ? $"{hours.From:HH\\:mm} hôm nay" : $"{hours.From:HH\\:mm} ngày mai";
        return $"Tin nhắn tự động: Hiện ngoài giờ làm việc ({hours.From:HH\\:mm}–{hours.To:HH\\:mm}). "
            + $"Nhân viên sẽ trả lời bạn khi chi nhánh mở cửa lúc {when}.";
    }

    /// <summary>No automatic reply while staff (or an automatic reply) spoke this recently: the chat is live (owner 07/11).</summary>
    public static readonly TimeSpan AutoReplyQuiet = TimeSpan.FromMinutes(30);

    /// <summary>
    /// A customer message gets the automatic reply only when it starts a new wait and nobody answered in the last
    /// <see cref="AutoReplyQuiet"/>: during a live conversation the customer is not told "ngoài giờ" again (owner 07/11).
    /// </summary>
    public static bool ShouldAutoReply(bool startsNewWait, DateTime? lastStaffOrAutoUtc, DateTime nowUtc) =>
        startsNewWait && (lastStaffOrAutoUtc is null || nowUtc - lastStaffOrAutoUtc.Value >= AutoReplyQuiet);

    /// <summary>A customer wrote: a resolved conversation opens again; the wait starts if staff had answered.</summary>
    public static void OnCustomerMessage(SupportConversation c, DateTime nowUtc)
    {
        if (c.Status == Resolved) { c.Status = Waiting; c.ResolvedAtUtc = null; c.ResolvedByEmployeeId = null; c.ResolvedByName = null; }
        c.WaitingSinceUtc ??= nowUtc;
        c.LastMessageAtUtc = nowUtc;
        c.UpdatedAtUtc = nowUtc;
    }

    /// <summary>Staff answered: in progress, nobody waits any more.</summary>
    public static void OnStaffMessage(SupportConversation c, DateTime nowUtc)
    {
        if (c.Status != Resolved) c.Status = InProgress;
        c.WaitingSinceUtc = null;
        c.LastMessageAtUtc = nowUtc;
        c.UpdatedAtUtc = nowUtc;
    }

    public static void Resolve(SupportConversation c, int employeeId, string? employeeName, DateTime nowUtc)
    {
        c.Status = Resolved;
        c.WaitingSinceUtc = null;
        c.ResolvedAtUtc = nowUtc;
        c.ResolvedByEmployeeId = employeeId;
        c.ResolvedByName = employeeName;
        c.UpdatedAtUtc = nowUtc;
    }

    /// <summary>Messages the customer has not seen: staff and automatic ones after their read marker.</summary>
    public static int UnreadForCustomer(SupportConversation c, IEnumerable<SupportMessage> messages) =>
        messages.Count(m => m.SupportConversationId == c.SupportConversationId && m.SenderType != Customer && m.SupportMessageId > c.CustomerLastReadMessageId);

    /// <summary>Customer messages staff have not seen.</summary>
    public static int UnreadForStaff(SupportConversation c, IEnumerable<SupportMessage> messages) =>
        messages.Count(m => m.SupportConversationId == c.SupportConversationId && m.SenderType == Customer && m.SupportMessageId > c.StaffLastReadMessageId);

    /// <summary>Read markers only move forward.</summary>
    public static long Advance(long current, long seen) => Math.Max(current, seen);

    /// <summary>A customer sees only their own conversations; staff only those of their branch.</summary>
    public static bool CanSee(SupportConversation c, int? customerId, int? staffBranchId) =>
        (customerId is > 0 && c.CustomerId == customerId) || (staffBranchId is > 0 && c.BranchId == staffBranchId);

    /// <summary>Staff list order: waiting longest first, then those in progress, resolved last.</summary>
    public static IEnumerable<SupportConversation> StaffOrder(IEnumerable<SupportConversation> rows) =>
        rows.OrderBy(c => c.Status == Resolved ? 2 : c.WaitingSinceUtc is null ? 1 : 0)
            .ThenBy(c => c.WaitingSinceUtc ?? DateTime.MaxValue)
            .ThenByDescending(c => c.LastMessageAtUtc);
}
