using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class PaymentMethod
{
    public int MethodID { get; set; }

    public string MethodCode { get; set; } = null!;

    public string MethodName { get; set; } = null!;

    public virtual ICollection<Payments> Payments { get; set; } = new List<Payments>();
}
