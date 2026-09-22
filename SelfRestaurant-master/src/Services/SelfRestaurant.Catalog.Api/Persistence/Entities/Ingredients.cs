using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class Ingredients
{
    public int IngredientID { get; set; }

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal CurrentStock { get; set; }

    public decimal ReorderLevel { get; set; }

    public string IssueMethod { get; set; } = "FEFO";

    public bool IsActive { get; set; }

    public virtual ICollection<IngredientBatches> IngredientBatches { get; set; } = new List<IngredientBatches>();

    public virtual ICollection<IngredientStockMovements> IngredientStockMovements { get; set; } = new List<IngredientStockMovements>();

    public virtual ICollection<DishIngredients> DishIngredients { get; set; } = new List<DishIngredients>();

    public virtual ICollection<OrderItemIngredients> OrderItemIngredients { get; set; } = new List<OrderItemIngredients>();
}
