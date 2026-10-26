using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>Reservation item 5 (26/10): the state of every table on the booking map, same rules as booking.</summary>
public sealed class ReservationMapTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 5, 0, 0, TimeSpan.Zero); // 12:00 restaurant time

    private static BranchTableDto Table(int seats, string status = "AVAILABLE") =>
        new(8, 1, 8, seats, "QR-8", status, status, status == "AVAILABLE");

    [Theory]
    [InlineData(4, "AVAILABLE", 2, false, 180, "AVAILABLE", "Trống")]
    [InlineData(4, "AVAILABLE", 2, true, 180, "BOOKED", "Đã được đặt")]
    [InlineData(2, "AVAILABLE", 4, false, 180, "TOO_SMALL", "Không đủ ghế")]
    [InlineData(2, "AVAILABLE", 4, true, 180, "TOO_SMALL", "Không đủ ghế")]       // the reason the guest can act on
    [InlineData(4, "INACTIVE", 2, false, 1440, "INACTIVE", "Tạm ngưng")]
    [InlineData(4, "OCCUPIED", 2, false, 30, "IN_USE", "Đang có khách")]        // A1: seated now, booking soon
    [InlineData(4, "OCCUPIED", 2, false, 120, "AVAILABLE", "Trống")]            // A1: two hours later it is free
    public void Every_table_gets_one_state_with_a_vietnamese_label(int seats, string status, int party, bool booked,
        int minutesAhead, string expected, string label)
    {
        var state = ReservationRules.TableMapStatus(Table(seats, status), party, booked, Now.AddMinutes(minutesAhead), Now);
        Assert.Equal(expected, state.Status);
        Assert.Equal(label, state.Label);
    }

    [Fact]
    public void Available_on_the_map_means_bookable_by_the_booking_rules()
    {
        foreach (var status in new[] { "AVAILABLE", "OCCUPIED", "INACTIVE" })
        foreach (var minutes in new[] { 20, 119, 120, 600 })
        foreach (var booked in new[] { false, true })
        {
            var table = Table(4, status);
            var at = Now.AddMinutes(minutes);
            var bookable = ReservationRules.TableBookableAt(table, at, Now) && !booked;
            Assert.Equal(bookable, ReservationRules.TableMapStatus(table, 2, booked, at, Now).Status == "AVAILABLE");
        }
    }
}
