using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class CategoryDish
{
    public int CategoryDishID { get; set; }

    public int? DisplayOrder { get; set; }

    public bool? IsAvailable { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int MenuCategoryID { get; set; }

    public int DishID { get; set; }

    public virtual Dishes Dish { get; set; } = null!;

    public virtual MenuCategory MenuCategory { get; set; } = null!;
}
