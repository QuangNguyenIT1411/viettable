using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class Menus
{
    public int MenuID { get; set; }

    public string MenuName { get; set; } = null!;

    public DateOnly? Date { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int BranchID { get; set; }

    public virtual Branches Branch { get; set; } = null!;

    public virtual ICollection<MenuCategory> MenuCategory { get; set; } = new List<MenuCategory>();
}
