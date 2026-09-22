using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class Bills
{
    public int BillID { get; set; }

    public int OrderID { get; set; }

    public string BillCode { get; set; } = null!;

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

    public virtual Customers? Customer { get; set; }

    public virtual Employees? Employee { get; set; }

    public virtual Orders Order { get; set; } = null!;
}
