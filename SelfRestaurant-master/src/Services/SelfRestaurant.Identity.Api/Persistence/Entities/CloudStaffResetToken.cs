namespace SelfRestaurant.Identity.Api.Persistence.Entities;

// Identity-owned, hash-only recovery state. Raw reset tokens never reach storage.
public sealed class CloudStaffResetToken
{
    public string TokenHash { get; set; } = null!;
    public int EmployeeId { get; set; }
    public DateTime ExpiryUtc { get; set; }
    public bool IsUsed { get; set; }
}
