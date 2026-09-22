using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class DishDetails
{
    public int DishID { get; set; }

    public string DishName { get; set; } = null!;

    public decimal Price { get; set; }

    public bool? Available { get; set; }

    public bool? IsVegetarian { get; set; }

    public bool? IsDailySpecial { get; set; }

    public string CategoryName { get; set; } = null!;

    public int CategoryID { get; set; }
}
