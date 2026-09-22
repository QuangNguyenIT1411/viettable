using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class Branches
{
    public int BranchID { get; set; }

    public string Name { get; set; } = null!;

    public string? Location { get; set; }

    public string? ManagerName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? OpeningHours { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int RestaurantID { get; set; }

    public virtual ICollection<DiningTables> DiningTables { get; set; } = new List<DiningTables>();

    public virtual ICollection<Employees> Employees { get; set; } = new List<Employees>();

    public virtual ICollection<Menus> Menus { get; set; } = new List<Menus>();

    public virtual Restaurants Restaurant { get; set; } = null!;
}
