namespace SelfRestaurant.Billing.Api.Persistence.Entities;

/// <summary>
/// A reservation deposit paid by VietQR ("VT DC{reservationId}"), confirmed by the SePay webhook and taken off the
/// final bill (docs/DEPOSIT_PLAN.md). Status values: SelfRestaurant.Deposits.DepositRules.
/// </summary>
public sealed class ReservationDeposit
{
    public int ReservationDepositId { get; set; }
    public int ReservationId { get; set; }
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int PartySize { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime ReservedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public decimal? PaidAmount { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? ProviderTransactionId { get; set; }
    public int? AppliedCheckoutQuoteId { get; set; }
    public decimal? AppliedAmount { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    /// <summary>Why it closed: "Huỷ trước giờ đến từ 2 giờ", "Khách không đến", "Tiền tới sau khi hết hạn"…</summary>
    public string? ClosedReason { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public int? RefundedByEmployeeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
