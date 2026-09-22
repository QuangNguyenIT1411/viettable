namespace SelfRestaurant.Billing.Api.Persistence.Entities;

public sealed class CheckoutQuote
{
    public int CheckoutQuoteId { get; set; }
    public string QuoteCode { get; set; } = null!;
    public int DiningSessionId { get; set; }
    public string DiningSessionCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int TableId { get; set; }
    public string Status { get; set; } = "OPEN";
    public decimal Subtotal { get; set; }
    public int? VoucherId { get; set; }
    public decimal VoucherDiscount { get; set; }
    public int PointsApplied { get; set; }
    public decimal PointsDiscount { get; set; }
    public decimal TotalAmount { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string ItemsJson { get; set; } = "[]";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Voucher
{
    public int VoucherId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string DiscountType { get; set; } = "FIXED";
    public decimal DiscountValue { get; set; }
    public decimal MinSubtotal { get; set; }
    public decimal? MaxDiscount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
}

public sealed class Payment
{
    public int PaymentId { get; set; }
    public string PaymentCode { get; set; } = null!;
    public int CheckoutQuoteId { get; set; }
    public int DiningSessionId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Channel { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string? Location { get; set; }
    public string Status { get; set; } = "CREATED";
    public int AttemptNumber { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string? ProviderReference { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderPaymentUrl { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime? ProviderExpiresAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public int CheckoutQuoteId { get; set; }
    public int PaymentId { get; set; }
    public int DiningSessionId { get; set; }
    public int CustomerId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public DateTime IssuedAtUtc { get; set; }
}

public sealed class SplitBill
{
    public int SplitBillId { get; set; }
    public int CheckoutQuoteId { get; set; }
    public int DiningSessionId { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "OPEN";
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<SplitBillPart> Parts { get; set; } = new List<SplitBillPart>();
}

public sealed class SplitBillPart
{
    public int SplitBillPartId { get; set; }
    public int SplitBillId { get; set; }
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "UNPAID";
    public int? PaymentId { get; set; }
    public SplitBill SplitBill { get; set; } = null!;
}
