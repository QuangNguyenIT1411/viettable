using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class OrderItemIngredients
{
    public int OrderItemIngredientID { get; set; }

    public int OrderItemID { get; set; }

    public int IngredientID { get; set; }

    public decimal Quantity { get; set; }

    public bool IsRemoved { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Ingredients Ingredient { get; set; } = null!;

    public virtual OrderItems OrderItem { get; set; } = null!;
}
