using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class PaymentStatus
{
    public int StatusID { get; set; }

    public string StatusCode { get; set; } = null!;

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Payments> Payments { get; set; } = new List<Payments>();
}
