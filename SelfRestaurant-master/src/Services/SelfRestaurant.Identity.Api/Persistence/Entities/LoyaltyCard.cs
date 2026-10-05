namespace SelfRestaurant.Identity.Api.Persistence.Entities;

// PostgreSQL baseline for the existing locally-owned LoyaltyCards view source.
public sealed class LoyaltyCard
{
    public int CardID { get; set; }
    public int? Points { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool? IsActive { get; set; }
    public int CustomerID { get; set; }
}
