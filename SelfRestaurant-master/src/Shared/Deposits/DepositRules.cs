namespace SelfRestaurant.Deposits;

/// <summary>
/// Reservation deposit settings (owner 27/10/2026), environment variables only:
/// Reservations__Deposit__Enabled (default false), __MinGuests (6), __PerGuestVnd (50000),
/// __PaymentMinutes (15), __RefundCutoffHours (2).
/// </summary>
public sealed class DepositOptions
{
    public const string Section = "Reservations:Deposit";
    public bool Enabled { get; set; }
    public int MinGuests { get; set; } = 6;
    public decimal PerGuestVnd { get; set; } = 50_000m;
    public int PaymentMinutes { get; set; } = 15;
    public int RefundCutoffHours { get; set; } = 2;
}

/// <summary>
/// One source for every deposit rule (docs/DEPOSIT_PLAN.md). Pure, so each rule is unit tested; linked into Gateway
/// and Billing like LoyaltyRules.
/// </summary>
public static class DepositRules
{
    public const string Pending = "PENDING";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string Paid = "PAID";
    public const string Applied = "APPLIED";
    public const string RefundRequired = "REFUND_REQUIRED";
    public const string Refunded = "REFUNDED";
    public const string Forfeited = "FORFEITED";

    /// <summary>Reservation status while the guest still has to pay the deposit (Customers).</summary>
    public const string ReservationPendingDeposit = "PendingDeposit";
    /// <summary>Close reason sent by the Gateway when Customers refused "deposit paid" for a closed booking.</summary>
    public const string ReservationClosedBeforePayment = "ClosedBeforePayment";

    // Webhook outcomes (stored in billing."BankTransferEvents"."Outcome", max 30 chars).
    public const string OutcomePaid = "DEPOSIT_PAID";
    public const string OutcomeLate = "DEPOSIT_LATE";
    public const string OutcomeAmountTooLow = "DEPOSIT_AMOUNT_TOO_LOW";
    public const string OutcomeNotPending = "DEPOSIT_NOT_PENDING";
    public const string OutcomeNoMatch = "DEPOSIT_NO_MATCH";

    /// <summary>50.000đ × guests from the threshold on; 0 when the feature is off or the party is smaller.</summary>
    public static decimal RequiredAmount(DepositOptions options, int partySize) =>
        options.Enabled && options.MinGuests > 0 && options.PerGuestVnd > 0 && partySize >= options.MinGuests
            ? options.PerGuestVnd * partySize : 0m;

    public static DateTime DueAt(DateTime createdAtUtc, DepositOptions options) =>
        createdAtUtc.AddMinutes(Math.Max(1, options.PaymentMinutes));

    /// <summary>Bank transfer content, kept apart from the table payment "VT B{branch} TT{payment}".</summary>
    public static string TransferContent(int reservationId) => $"VT DC{reservationId}";

    /// <summary>A waiting deposit past its 15 minutes: the booking is cancelled and the table freed.</summary>
    public static bool IsOverdue(string status, DateTime dueAtUtc, DateTime nowUtc) => status == Pending && nowUtc > dueAtUtc;

    /// <summary>The state a deposit shows now (a waiting one past its time reads as expired).</summary>
    public static string Effective(string status, DateTime dueAtUtc, DateTime nowUtc) =>
        IsOverdue(status, dueAtUtc, nowUtc) ? Expired : status;

    /// <summary>
    /// What happens to the deposit when the booking closes without the guest eating there. Cancelled at least
    /// RefundCutoffHours before the reserved time, or rejected by the restaurant: refund by hand. Later, or no-show:
    /// the deposit is kept. Money never moves automatically. Null = no change.
    /// </summary>
    public static (string Status, string Reason)? OnReservationClosed(string depositStatus, string reservationStatus,
        DateTime reservedAtUtc, DateTime closedAtUtc, DepositOptions options)
    {
        var status = reservationStatus.Trim();
        if (depositStatus == Pending)
            return status is "Cancelled" or "Rejected" or "NoShow" ? (Cancelled, "Đặt bàn đã đóng trước khi đặt cọc") : null;
        if (depositStatus != Paid) return null;
        return status switch
        {
            "Cancelled" when reservedAtUtc - closedAtUtc >= TimeSpan.FromHours(Math.Max(0, options.RefundCutoffHours))
                => (RefundRequired, $"Huỷ trước giờ đến từ {options.RefundCutoffHours} giờ"),
            "Cancelled" => (Forfeited, $"Huỷ sát giờ (dưới {options.RefundCutoffHours} giờ)"),
            "Rejected" => (RefundRequired, "Nhà hàng từ chối đặt bàn"),
            // The money arrived in time but the booking had just closed (e.g. expired a moment before): never kept.
            ReservationClosedBeforePayment => (RefundRequired, "Tiền cọc tới khi đặt bàn đã đóng"),
            "NoShow" => (Forfeited, "Khách không đến"),
            _ => null,
        };
    }

    /// <summary>The part of a paid deposit taken off the bill: never more than what is left to pay.</summary>
    public static decimal Applicable(decimal depositAmount, decimal payableBeforeDeposit) =>
        Math.Max(0m, Math.Min(depositAmount, payableBeforeDeposit));

    /// <summary>The decision for money that arrived with "VT DC{id}". Late money is kept as a refund to do.</summary>
    public static string DecidePayment(string? depositStatus, decimal depositAmount, DateTime dueAtUtc, decimal amount, DateTime nowUtc)
    {
        if (depositStatus is null) return OutcomeNoMatch;
        if (depositStatus is not (Pending or Expired or Cancelled)) return OutcomeNotPending;
        if (amount < depositAmount) return OutcomeAmountTooLow;
        return depositStatus == Pending && nowUtc <= dueAtUtc ? OutcomePaid : OutcomeLate;
    }
}
