using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class LoyaltyCards
{
    public int CardID { get; set; }

    public int? Points { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public bool? IsActive { get; set; }

    public int CustomerID { get; set; }

    public virtual Customers Customer { get; set; } = null!;
}
