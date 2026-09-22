using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class Dishes
{
    public int DishID { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public bool? Available { get; set; }

    public string? Image { get; set; }

    public string? Description { get; set; }

    public string? Unit { get; set; }

    public bool? IsVegetarian { get; set; }

    public bool? IsDailySpecial { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int CategoryID { get; set; }

    public virtual Categories Category { get; set; } = null!;

    public virtual ICollection<CategoryDish> CategoryDish { get; set; } = new List<CategoryDish>();

    public virtual ICollection<DishIngredients> DishIngredients { get; set; } = new List<DishIngredients>();

    public virtual ICollection<OrderItems> OrderItems { get; set; } = new List<OrderItems>();
}
