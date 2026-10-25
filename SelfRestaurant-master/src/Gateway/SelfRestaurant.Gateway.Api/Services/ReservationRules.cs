using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>Pure reservation rules shared by availability, booking and walk-in check-in (unit-tested).</summary>
public static class ReservationRules
{
    /// <summary>A table's live status (guests seated now) only matters for a booking that starts this soon.</summary>
    public static readonly TimeSpan LiveStatusHorizon = TimeSpan.FromHours(2);

    /// <summary>A walk-in may not take a table whose booked guest arrives within this time (owner, 25/10).</summary>
    public static readonly TimeSpan WalkInGuard = TimeSpan.FromMinutes(60);

    /// <summary>Booked guests may still arrive this long after the reserved time (the check-in window).</summary>
    public static readonly TimeSpan ArrivalWindow = TimeSpan.FromMinutes(30);

    public static readonly TimeOnly DefaultOpenFrom = new(10, 0);
    public static readonly TimeOnly DefaultLastSlot = new(21, 30);

    /// <summary>
    /// Can this table take a booking at <paramref name="at"/>? Inactive tables never. Before (25/10) a booking for
    /// tomorrow was refused when the table was merely occupied right now.
    /// </summary>
    public static bool TableBookableAt(BranchTableDto table, DateTimeOffset at, DateTimeOffset now) =>
        !string.Equals(table.StatusCode, "INACTIVE", StringComparison.OrdinalIgnoreCase)
        && (table.IsAvailable || at - now >= LiveStatusHorizon);

    /// <summary>Restaurant time of day of the booking is within [openFrom, lastSlot].</summary>
    public static bool WithinBookingHours(DateTimeOffset at, TimeOnly openFrom, TimeOnly lastSlot)
    {
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, RestaurantTimeZone).DateTime);
        return local >= openFrom && local <= lastSlot;
    }

    public static TimeOnly ConfiguredTime(IConfiguration configuration, string key, TimeOnly fallback) =>
        TimeOnly.TryParse(configuration[key], System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>"19:00" in restaurant time, for messages.</summary>
    public static string LocalClock(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), RestaurantTimeZone).ToString("HH:mm");

    public static TimeZoneInfo RestaurantTimeZone { get; } = FindRestaurantTimeZone();

    private static TimeZoneInfo FindRestaurantTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
