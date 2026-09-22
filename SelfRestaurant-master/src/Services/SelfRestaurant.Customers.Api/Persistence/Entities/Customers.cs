using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class Customers
{
    public int CustomerID { get; set; }

    public string Name { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public int? LoyaltyPoints { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsActive { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public int CreditPoints { get; set; }

    public virtual ICollection<Bills> Bills { get; set; } = new List<Bills>();

    public virtual ICollection<LoyaltyCards> LoyaltyCards { get; set; } = new List<LoyaltyCards>();

    public virtual ICollection<Orders> Orders { get; set; } = new List<Orders>();

    public virtual ICollection<PasswordResetTokens> PasswordResetTokens { get; set; } = new List<PasswordResetTokens>();

    public virtual ICollection<Payments> Payments { get; set; } = new List<Payments>();
}
