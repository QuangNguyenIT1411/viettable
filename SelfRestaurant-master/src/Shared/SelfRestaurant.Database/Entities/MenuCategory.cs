using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class MenuCategory
{
    public int MenuCategoryID { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsActive { get; set; }

    public int MenuID { get; set; }

    public int CategoryID { get; set; }

    public virtual Categories Category { get; set; } = null!;

    public virtual ICollection<CategoryDish> CategoryDish { get; set; } = new List<CategoryDish>();

    public virtual Menus Menu { get; set; } = null!;
}
