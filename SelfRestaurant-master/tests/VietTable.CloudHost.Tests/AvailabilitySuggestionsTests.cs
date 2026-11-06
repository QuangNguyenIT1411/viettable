using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11 "Hết bàn": nearest times of the same day, and other branches ordered by distance or name.</summary>
public sealed class AvailabilitySuggestionsTests
{
    private static readonly TimeZoneInfo Vn = ReservationRules.RestaurantTimeZone;
    private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Nearest_slots_come_first_before_and_after_the_asked_time()
    {
        var times = AvailabilitySuggestions.CandidateTimes(At(20, 19, 0), new TimeOnly(10, 0), new TimeOnly(21, 30), At(20, 8, 0), Vn);
        Assert.Equal(new[] { At(20, 18, 30), At(20, 19, 30), At(20, 18, 0), At(20, 20, 0) }, times.Take(4).ToArray());
        Assert.DoesNotContain(At(20, 19, 0), times);
        Assert.All(times, x => Assert.Equal(20, TimeZoneInfo.ConvertTime(x, Vn).Day));
        Assert.Equal(At(20, 10, 0), times.Min());
        Assert.Equal(At(20, 21, 30), times.Max());
    }

    [Fact]
    public void Slots_less_than_15_minutes_ahead_are_never_offered()
    {
        var times = AvailabilitySuggestions.CandidateTimes(At(20, 19, 0), new TimeOnly(10, 0), new TimeOnly(21, 30), At(20, 18, 20), Vn);
        Assert.DoesNotContain(At(20, 18, 30), times);
        Assert.Equal(At(20, 19, 30), times.First());
    }

    [Fact]
    public void Branches_with_a_distance_come_first_nearest_first_then_by_name()
    {
        var ordered = AvailabilitySuggestions.OrderBranches(new[]
        {
            new AvailabilitySuggestions.BranchOption(3, "Chi nhánh Gò Vấp", null, null, 2),
            new AvailabilitySuggestions.BranchOption(2, "Chi nhánh Bình Thạnh", null, 4.2, 1),
            new AvailabilitySuggestions.BranchOption(4, "Chi nhánh Bến Thành", null, null, 3),
            new AvailabilitySuggestions.BranchOption(5, "Chi nhánh Thủ Đức", null, 1.5, 5),
            new AvailabilitySuggestions.BranchOption(6, "Chi nhánh Đầy", null, 0.5, 0),
        });
        Assert.Equal(new[] { 5, 2, 4 }, ordered.Select(x => x.BranchId).ToArray()); // full one dropped, max 3
    }
}
