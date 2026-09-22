using System;
using System.Collections.Generic;

namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class Customers
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
    public string? Password { get; set; }
    public string AuthProvider { get; set; } = "Password";
    public string? ExternalProvider { get; set; }
    public string? ExternalSubject { get; set; }
    public int CreditPoints { get; set; }

    public ICollection<PasswordResetTokens> PasswordResetTokens { get; set; } = new List<PasswordResetTokens>();
}
