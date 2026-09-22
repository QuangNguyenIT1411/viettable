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

    public decimal PointsDiscount { get; set; }

    public int? PointsUsed { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public decimal? PaymentAmount { get; set; }

    public decimal? ChangeAmount { get; set; }

    public int? EmployeeID { get; set; }

    public int? CustomerID { get; set; }

    public bool IsActive { get; set; }
}
