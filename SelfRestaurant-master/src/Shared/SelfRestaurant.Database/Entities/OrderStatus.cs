using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class OrderStatus
{
    public int StatusID { get; set; }

    public string StatusCode { get; set; } = null!;

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Orders> Orders { get; set; } = new List<Orders>();
}
