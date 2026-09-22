using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class ActiveOrders
{
    public int OrderID { get; set; }

    public string? OrderCode { get; set; }

    public DateTime OrderTime { get; set; }

    public string? CustomerName { get; set; }

    public string? PhoneNumber { get; set; }

    public int? TableSeats { get; set; }

    public string? StatusName { get; set; }

    public string? BranchName { get; set; }
}
