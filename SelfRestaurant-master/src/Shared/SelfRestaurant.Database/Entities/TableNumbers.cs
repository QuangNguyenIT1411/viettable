using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class TableNumbers
{
    public int TableID { get; set; }

    public int BranchID { get; set; }

    public string BranchName { get; set; } = null!;

    public long? TableNumber { get; set; }

    public int NumberOfSeats { get; set; }

    public string? QRCode { get; set; }

    public string StatusName { get; set; } = null!;

    public int? CurrentOrderID { get; set; }

    public bool? IsActive { get; set; }
}
