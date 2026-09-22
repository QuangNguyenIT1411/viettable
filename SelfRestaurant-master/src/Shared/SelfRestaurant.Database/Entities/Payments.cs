using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class Payments
{
    public int PaymentID { get; set; }

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public int OrderID { get; set; }

    public int? CustomerID { get; set; }

    public int MethodID { get; set; }

    public int StatusID { get; set; }

    public virtual Customers? Customer { get; set; }

    public virtual PaymentMethod Method { get; set; } = null!;

    public virtual Orders Order { get; set; } = null!;

    public virtual PaymentStatus Status { get; set; } = null!;
}
