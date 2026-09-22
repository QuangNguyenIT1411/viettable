using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class BranchRevenue
{
    public int BranchID { get; set; }

    public string BranchName { get; set; } = null!;

    public int? TotalOrders { get; set; }

    public decimal? TotalRevenue { get; set; }

    public DateOnly? OrderDate { get; set; }
}
