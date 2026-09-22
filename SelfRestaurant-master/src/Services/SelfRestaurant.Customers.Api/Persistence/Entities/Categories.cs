using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class Categories
{
    public int CategoryID { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int? DisplayOrder { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Dishes> Dishes { get; set; } = new List<Dishes>();

    public virtual ICollection<MenuCategory> MenuCategory { get; set; } = new List<MenuCategory>();
}
