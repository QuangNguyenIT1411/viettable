using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class DiningTables
{
    public int TableID { get; set; }

    public int NumberOfSeats { get; set; }

    public int? CurrentOrderID { get; set; }

    public string? QRCode { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int BranchID { get; set; }

    public int StatusID { get; set; }

    public virtual Branches Branch { get; set; } = null!;

    public virtual ICollection<Orders> Orders { get; set; } = new List<Orders>();

    public virtual TableStatus Status { get; set; } = null!;
}
