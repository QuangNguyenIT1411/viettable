using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class Orders
{
    public int OrderID { get; set; }

    public string? OrderCode { get; set; }

    public DateTime OrderTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public string? Note { get; set; }

    public bool? IsActive { get; set; }

    public int? TableID { get; set; }

    public int? CustomerID { get; set; }

    public int StatusID { get; set; }

    public int? CashierID { get; set; }

    public virtual ICollection<Bills> Bills { get; set; } = new List<Bills>();

    public virtual Employees? Cashier { get; set; }

    public virtual Customers? Customer { get; set; }

    public virtual ICollection<Payments> Payments { get; set; } = new List<Payments>();

    public virtual OrderStatus Status { get; set; } = null!;

    public virtual DiningTables? Table { get; set; }
}
