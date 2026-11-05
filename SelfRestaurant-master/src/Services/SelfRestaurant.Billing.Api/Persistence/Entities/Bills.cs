using System;

namespace SelfRestaurant.Billing.Api.Persistence.Entities;

public sealed class Bills
{
    public int BillID { get; set; }

    public int OrderID { get; set; }
    public string? DiningSessionCode { get; set; }
    public string? CheckoutIdempotencyKey { get; set; }

    public string BillCode { get; set; } = null!;

    public string? OrderCodeSnapshot { get; set; }

    public int? TableIdSnapshot { get; set; }

    public string? TableNameSnapshot { get; set; }

    public int? BranchIdSnapshot { get; set; }

    public string? BranchNameSnapshot { get; set; }

    public DateTime BillTime { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    /// <summary>Promotion applied at the counter instead of the cashier's discount when it was better (docs/PROMO_PLAN.md).</summary>
    public int? PromotionId { get; set; }

    public decimal PromotionDiscount { get; set; }

    public decimal PointsDiscount { get; set; }

    public int? PointsUsed { get; set; }

    /// <summary>Service fee and VAT frozen at checkout (owner 05/11); 0 on bills from before.</summary>
    public decimal ServiceFeePercent { get; set; }
    public decimal ServiceFeeAmount { get; set; }
    public decimal VatPercent { get; set; }
    public decimal VatAmount { get; set; }
    /// <summary>Dishes as sold (name, unit price, quantity, line total) at checkout; null on bills from before.</summary>
    public string? ItemsJson { get; set; }
    public string? EmployeeNameSnapshot { get; set; }
    public decimal TotalAmount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public decimal? PaymentAmount { get; set; }

    public decimal? ChangeAmount { get; set; }

    public int? EmployeeID { get; set; }

    public int? CustomerID { get; set; }

    public bool IsActive { get; set; }
}
