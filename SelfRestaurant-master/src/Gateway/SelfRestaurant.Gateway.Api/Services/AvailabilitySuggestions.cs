namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// "Hết bàn" alternatives (owner 06/11): the nearest bookable times of the same day at the chosen branch (before and
/// after), and other branches with room at the same time (closest first when the guest's position is known, otherwise
/// by name). Pure; the controller checks each candidate against the real tables and bookings.
/// </summary>
public static class AvailabilitySuggestions
{
    public const int SlotMinutes = 30;
    public const int MaxTimeSuggestions = 4;
    /// <summary>How many candidate times are checked at most (each costs one read of the bookings).</summary>
    public const int MaxTimeChecks = 10;
    public const int MaxBranchSuggestions = 3;

    /// <summary>
    /// Bookable slots of the same restaurant day as <paramref name="at"/>, nearest first (the earlier one wins a tie),
    /// never <paramref name="at"/> itself, never within 15 minutes from now.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> CandidateTimes(DateTimeOffset at, TimeOnly openFrom, TimeOnly lastSlot,
        DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var day = local.Date;
        var result = new List<DateTimeOffset>();
        for (var t = openFrom; t <= lastSlot; t = t.AddMinutes(SlotMinutes))
        {
            var wall = day + t.ToTimeSpan();
            var slot = new DateTimeOffset(wall, zone.GetUtcOffset(wall));
            if (slot.UtcDateTime == at.UtcDateTime) continue;
            if (slot <= now.AddMinutes(14)) continue;
            result.Add(slot);
            if (t.AddMinutes(SlotMinutes) < t) break; // wrapped past midnight
        }
        return result
            .OrderBy(x => Math.Abs((x - at).TotalMinutes))
            .ThenBy(x => x)
            .ToArray();
    }

    public sealed record BranchOption(int BranchId, string Name, string? Location, double? DistanceKm, int AvailableTableCount);

    /// <summary>Branches with room, closest first when distances are known; then by name (Vietnamese order).</summary>
    public static IReadOnlyList<BranchOption> OrderBranches(IEnumerable<BranchOption> options) =>
        options.Where(x => x.AvailableTableCount > 0)
            .OrderBy(x => x.DistanceKm is null ? 1 : 0)
            .ThenBy(x => x.DistanceKm ?? 0)
            .ThenBy(x => x.Name, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true))
            .Take(MaxBranchSuggestions)
            .ToArray();
}
