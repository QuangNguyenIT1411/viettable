using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class DishIngredients
{
    public int DishIngredientID { get; set; }

    public int DishID { get; set; }

    public int IngredientID { get; set; }

    public decimal QuantityPerDish { get; set; }

    public virtual Dishes Dish { get; set; } = null!;

    public virtual Ingredients Ingredient { get; set; } = null!;
}
