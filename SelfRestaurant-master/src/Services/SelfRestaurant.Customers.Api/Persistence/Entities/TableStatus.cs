using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class TableStatus
{
    public int StatusID { get; set; }

    public string StatusCode { get; set; } = null!;

    public string StatusName { get; set; } = null!;

    public virtual ICollection<DiningTables> DiningTables { get; set; } = new List<DiningTables>();
}
