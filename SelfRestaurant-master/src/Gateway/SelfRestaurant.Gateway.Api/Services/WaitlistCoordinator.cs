using System.Text.Json;
using SelfRestaurant.Gateway.Api.Infrastructure;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// Offers freed tables to waiting guests (owner 06/11). Vercel runs no background job, so this runs when something can
/// have freed a table (a booking cancelled, rejected, marked absent) and when the waitlist is read (the guest's screen,
/// the app's 15-minute check, the cashier). Each waiting guest, in sign-up order, gets the first time in their window
/// with a table that fits; Customers holds that table for a few minutes and keeps only one winner.
/// </summary>
public sealed class WaitlistCoordinator(CustomersClient customers, CatalogClient catalog, TableAvailability tables,
    CustomerInboxNotifier inbox, IConfiguration configuration, ILogger<WaitlistCoordinator> logger)
{
    public const int MaxGuestsPerRun = 10;
    public const int MaxSlotsPerGuest = 8;

    public int OfferMinutes => int.TryParse(configuration["Waitlist:OfferMinutes"], out var m) && m is >= 1 and <= 60
        ? m : 10;

    public sealed record QueueEntry(int WaitlistEntryId, int CustomerId, int BranchId, int PartySize,
        DateTime WindowStartUtc, DateTime WindowEndUtc, DateTime CreatedAtUtc);

    /// <summary>Never fails the caller: a missed run only delays an offer until the next read.</summary>
    public async Task<int> ProcessBranchQuietlyAsync(int branchId, CancellationToken ct)
    {
        try { return await ProcessBranchAsync(branchId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Waitlist run skipped for branch {BranchId} ({ErrorType}).", branchId, ex.GetType().Name);
            return 0;
        }
    }

    public async Task<int> ProcessBranchAsync(int branchId, CancellationToken ct)
    {
        var queue = await customers.GetWaitlistQueueAsync(branchId, ct);
        if (queue.Count == 0) return 0;
        var layout = await catalog.GetBranchTablesAsync(branchId, ct);
        if (layout is null) return 0;
        var openFrom = ReservationRules.ConfiguredTime(configuration, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom);
        var lastSlot = ReservationRules.ConfiguredTime(configuration, "Reservations:LastSlot", ReservationRules.DefaultLastSlot);
        var offered = 0;
        foreach (var guest in queue.Take(MaxGuestsPerRun))
        {
            var slots = WaitlistSlots.Candidates(guest.WindowStartUtc, guest.WindowEndUtc, DateTimeOffset.UtcNow,
                openFrom, lastSlot, ReservationRules.RestaurantTimeZone);
            foreach (var slot in slots.Take(MaxSlotsPerGuest))
            {
                var found = await tables.FindAsync(layout, branchId, slot, guest.PartySize, null, ct);
                var table = TableAvailability.Best(found.Available);
                if (table is null) continue;
                try
                {
                    await customers.OfferWaitlistAsync(guest.WaitlistEntryId, table.TableId, slot.UtcDateTime, OfferMinutes, ct);
                    offered++;
                    await inbox.PushAsync(guest.CustomerId, ct);
                    break;
                }
                catch (ApiClientException ex) when (ex.StatusCode == 409)
                {
                    // Someone got this table first, or this guest was just handled elsewhere: try the next time.
                    if (ex.Code == "waitlist_state_conflict") break;
                }
            }
        }
        return offered;
    }

    /// <summary>The guest's own entries; the branches they wait at are processed first so an offer shows at once.</summary>
    public async Task<JsonElement> MineAsync(int customerId, CancellationToken ct)
    {
        var mine = await customers.GetMyWaitlistAsync(customerId, ct);
        var branches = mine.ValueKind == JsonValueKind.Array
            ? mine.EnumerateArray().Where(x => x.TryGetProperty("status", out var s) && s.GetString() == "Waiting")
                .Select(x => x.GetProperty("branchId").GetInt32()).Distinct().ToArray()
            : Array.Empty<int>();
        var offered = 0;
        foreach (var branchId in branches.Take(3)) offered += await ProcessBranchQuietlyAsync(branchId, ct);
        return offered > 0 ? await customers.GetMyWaitlistAsync(customerId, ct) : mine;
    }
}

/// <summary>Bookable times inside a guest's window (pure; unit tested).</summary>
public static class WaitlistSlots
{
    /// <summary>Every 30 minutes from the window start to its end, inside booking hours, at least 15 minutes ahead.</summary>
    public static IReadOnlyList<DateTimeOffset> Candidates(DateTime windowStartUtc, DateTime windowEndUtc, DateTimeOffset now,
        TimeOnly openFrom, TimeOnly lastSlot, TimeZoneInfo zone)
    {
        var start = new DateTimeOffset(DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc));
        var end = new DateTimeOffset(DateTime.SpecifyKind(windowEndUtc, DateTimeKind.Utc));
        var result = new List<DateTimeOffset>();
        // Align to the half hour in restaurant time (a window starting 18:10 offers 18:30 first).
        var local = TimeZoneInfo.ConvertTime(start, zone);
        var t = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, local.Minute - local.Minute % 30, 0, local.Offset);
        if (t < local) t = t.AddMinutes(30);
        for (; t <= end && result.Count < 48; t = t.AddMinutes(30))
        {
            if (t <= now.AddMinutes(14)) continue;
            if (!ReservationRules.WithinBookingHours(t, openFrom, lastSlot)) continue;
            result.Add(t);
        }
        return result;
    }
}
