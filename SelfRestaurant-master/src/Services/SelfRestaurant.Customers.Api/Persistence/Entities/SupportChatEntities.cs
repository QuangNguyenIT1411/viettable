namespace SelfRestaurant.Customers.Api.Persistence.Entities;

/// <summary>
/// "Chat với nhân viên hỗ trợ" (owner 06/11): one conversation between a customer and the staff of one branch, about
/// one topic and optionally one item (a booking, a deposit, a transfer, an invoice) that staff see with its live status.
/// </summary>
public sealed class SupportConversation
{
    public int SupportConversationId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = null!;
    public int BranchId { get; set; }
    /// <summary>DEPOSIT_TRANSFER, BILL_TRANSFER, BOOKING_CHANGE, DEPOSIT_REFUND, BILL_WRONG, LOYALTY_PROMO, OTHER.</summary>
    public string Topic { get; set; } = null!;
    /// <summary>NONE, RESERVATION, DEPOSIT, PAYMENT, INVOICE.</summary>
    public string ContextType { get; set; } = "NONE";
    public int? ContextId { get; set; }
    /// <summary>What the customer picked, as they saw it (e.g. "Đặt bàn #37 · 19:00 07/11"); the live status is read again.</summary>
    public string? ContextLabel { get; set; }
    /// <summary>Waiting (customer waits for staff), InProgress (staff answered), Resolved.</summary>
    public string Status { get; set; } = "Waiting";
    /// <summary>Oldest customer message staff have not answered yet; null once answered. Staff list sorts by it.</summary>
    public DateTime? WaitingSinceUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public long CustomerLastReadMessageId { get; set; }
    public long StaffLastReadMessageId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int? ResolvedByEmployeeId { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class SupportMessage
{
    public long SupportMessageId { get; set; }
    public int SupportConversationId { get; set; }
    /// <summary>CUSTOMER, STAFF or AUTO ("Tin nhắn tự động").</summary>
    public string SenderType { get; set; } = null!;
    public int? SenderEmployeeId { get; set; }
    public string? SenderName { get; set; }
    public string Body { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Ready answers staff pick from (admin configures; BranchId null = every branch).</summary>
public sealed class SupportQuickReply
{
    public int SupportQuickReplyId { get; set; }
    public int? BranchId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Support hours of a branch set by the admin ("HH:mm", restaurant time). Without a row, the branch's
/// opening hours are used; without both, the automatic reply does not mention hours.</summary>
public sealed class SupportBranchHours
{
    public int BranchId { get; set; }
    public string OpenFrom { get; set; } = null!;
    public string OpenTo { get; set; } = null!;
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByName { get; set; }
}
