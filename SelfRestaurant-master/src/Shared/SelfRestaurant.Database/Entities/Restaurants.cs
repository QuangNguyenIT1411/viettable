using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class Restaurants
{
    public int RestaurantID { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? Description { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Branches> Branches { get; set; } = new List<Branches>();
}
