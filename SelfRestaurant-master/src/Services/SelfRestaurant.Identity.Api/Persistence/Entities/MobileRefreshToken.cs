namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class MobileRefreshToken
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string FamilyId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public Customers Customer { get; set; } = null!;
}
