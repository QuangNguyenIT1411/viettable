using System;
using System.Collections.Generic;

namespace SelfRestaurant.Database.Entities;

public partial class PasswordResetTokens
{
    public int TokenID { get; set; }

    public int CustomerID { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiryDate { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Customers Customer { get; set; } = null!;
}
