namespace SelfRestaurant.Customers.Api.Persistence.Entities;

/// <summary>
/// A guest waiting for a table (owner 06/11): branch, a window of acceptable times, party size. When a table frees up
/// the first fitting entry in sign-up order gets a held booking (Reservation with status WaitlistHold) for a few
/// minutes; accepting turns it into a real booking, letting it expire frees the table for the next guest.
/// Statuses: Waiting, Offered, Accepted, Expired, Cancelled, Closed. See WaitlistRules.
/// </summary>
public sealed class WaitlistEntry
{
    public int WaitlistEntryId { get; set; }
    public int CustomerId { get; set; }
    public string ContactName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public int BranchId { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public int PartySize { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Waiting";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    /// <summary>The held booking while Offered; the real booking once Accepted.</summary>
    public int? OfferedReservationId { get; set; }
    public int? OfferedTableId { get; set; }
    public DateTime? OfferedReservedAtUtc { get; set; }
    public DateTime? OfferedAtUtc { get; set; }
    public DateTime? OfferExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedReason { get; set; }
    /// <summary>Optimistic concurrency: two requests acting on the same entry at once, only one wins.</summary>
    public int Version { get; set; }
}
