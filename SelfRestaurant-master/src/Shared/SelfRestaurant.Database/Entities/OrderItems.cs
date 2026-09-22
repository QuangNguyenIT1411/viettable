using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class OrderItems
{
    public int ItemID { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string? Note { get; set; }

    public int OrderID { get; set; }

    public int DishID { get; set; }

    public virtual Dishes Dish { get; set; } = null!;

    public virtual ICollection<OrderItemIngredients> OrderItemIngredients { get; set; } = new List<OrderItemIngredients>();
}
