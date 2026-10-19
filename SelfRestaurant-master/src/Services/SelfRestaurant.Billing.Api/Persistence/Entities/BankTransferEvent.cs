namespace SelfRestaurant.Billing.Api.Persistence.Entities;

/// <summary>
/// One incoming bank transfer reported by SePay. The unique (Provider, ProviderTransactionId) pair makes a
/// re-delivered webhook a no-op. Outcome says whether it was auto-confirmed or needs the cashier.
/// </summary>
public sealed class BankTransferEvent
{
    public long BankTransferEventId { get; set; }
    public string Provider { get; set; } = "SEPAY";
    /// <summary>SePay's transaction "id".</summary>
    public string ProviderTransactionId { get; set; } = null!;
    /// <summary>Bank reference ("referenceCode").</summary>
    public string? ReferenceCode { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? SubAccount { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Transfer content as the bank sent it (needed by the cashier for manual handling; never logged).</summary>
    public string Content { get; set; } = string.Empty;
    public string? TransactionDate { get; set; }
    /// <summary>RECEIVED (not finished yet), AUTO_CONFIRMED, AMOUNT_TOO_LOW, NO_MATCH, BRANCH_MISMATCH, NOT_TRANSFER, PAYMENT_NOT_PENDING.</summary>
    public string Outcome { get; set; } = "RECEIVED";
    public bool Overpaid { get; set; }
    public int? BranchId { get; set; }
    public int? PaymentId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    /// <summary>Set when a cashier marks a warning as handled.</summary>
    public DateTime? ResolvedAtUtc { get; set; }
    public int? ResolvedByEmployeeId { get; set; }
}
