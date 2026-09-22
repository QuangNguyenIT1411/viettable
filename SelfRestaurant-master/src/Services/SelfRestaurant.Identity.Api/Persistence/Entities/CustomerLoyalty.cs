namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class CustomerLoyalty
{
    public int CustomerID { get; set; }

    public string Name { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public int? LoyaltyPoints { get; set; }

    public int? CardID { get; set; }

    public int? CardPoints { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
