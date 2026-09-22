using System;

namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class PasswordResetTokens
{
    public int TokenID { get; set; }
    public int CustomerID { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customers Customer { get; set; } = null!;
}
