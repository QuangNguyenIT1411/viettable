namespace SelfRestaurant.Identity.Api.Persistence.Entities;

// A one-way ticket fingerprint, never a cookie or authentication credential.
public sealed class CloudStaffRevocation
{
    public string TicketHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
