using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>Reservation review 25/10, item 1 (A1–A7): availability, walk-in guard, no-shows, cancel rules.</summary>
public sealed class ReservationFixesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero); // 17:00 restaurant time

    private static BranchTableDto Table(string status) =>
        new(1, 1, 1, 4, "QR-1", status, status, status == "AVAILABLE");

    // A1: a table with guests right now can still be booked for tomorrow, but not for the next two hours.
    [Fact]
    public void Live_table_status_only_counts_for_bookings_that_start_soon()
    {
        Assert.True(ReservationRules.TableBookableAt(Table("AVAILABLE"), Now.AddMinutes(30), Now));
        Assert.False(ReservationRules.TableBookableAt(Table("OCCUPIED"), Now.AddMinutes(30), Now));
        Assert.False(ReservationRules.TableBookableAt(Table("OCCUPIED"), Now.AddMinutes(119), Now));
        Assert.True(ReservationRules.TableBookableAt(Table("OCCUPIED"), Now.AddHours(2), Now));
        Assert.True(ReservationRules.TableBookableAt(Table("OCCUPIED"), Now.AddDays(1), Now));
        Assert.False(ReservationRules.TableBookableAt(Table("INACTIVE"), Now.AddDays(1), Now));
    }

    // A5: bookings only between 10:00 and 21:30 restaurant time (UTC+7), configurable.
    [Theory]
    [InlineData(3, 0, true)]    // 10:00
    [InlineData(14, 30, true)]  // 21:30
    [InlineData(14, 31, false)] // 21:31
    [InlineData(2, 59, false)]  // 09:59
    [InlineData(20, 0, false)]  // 03:00 next day
    public void Booking_hours_use_restaurant_time(int utcHour, int utcMinute, bool allowed)
    {
        var at = new DateTimeOffset(2026, 10, 26, utcHour, utcMinute, 0, TimeSpan.Zero);
        Assert.Equal(allowed, ReservationRules.WithinBookingHours(at, ReservationRules.DefaultOpenFrom, ReservationRules.DefaultLastSlot));
    }

    [Fact]
    public void Booking_hours_come_from_configuration_with_safe_defaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Reservations:OpenFrom"] = "09:30", ["Reservations:LastSlot"] = "bad" }).Build();
        Assert.Equal(new TimeOnly(9, 30), ReservationRules.ConfiguredTime(config, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom));
        Assert.Equal(new TimeOnly(21, 30), ReservationRules.ConfiguredTime(config, "Reservations:LastSlot", ReservationRules.DefaultLastSlot));
        Assert.Equal("19:00", ReservationRules.LocalClock(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)));
    }

    // A3: a missed reservation is overdue once its 30-minute arrival window is over.
    [Fact]
    public void Missed_open_reservation_is_overdue_after_the_arrival_window()
    {
        var now = DateTime.UtcNow;
        Assert.False(CustomersController.IsOverdue(Reservation(1, "Confirmed", now.AddMinutes(-29)), now));
        Assert.True(CustomersController.IsOverdue(Reservation(1, "Confirmed", now.AddMinutes(-31)), now));
        Assert.True(CustomersController.IsOverdue(Reservation(1, "Pending", now.AddMinutes(-31)), now));
        Assert.False(CustomersController.IsOverdue(Reservation(1, "CheckingIn", now.AddHours(-3)), now));
        Assert.False(CustomersController.IsOverdue(Reservation(1, "Cancelled", now.AddHours(-3)), now));
    }

    // A7: no cancel while checking in or after the time; a repeated cancel is a no-op.
    [Fact]
    public async Task Cancel_is_refused_while_checking_in_or_when_overdue_and_repeats_are_harmless()
    {
        await using var db = Database();
        var now = DateTime.UtcNow;
        db.Reservations.AddRange(Reservation(1, "CheckingIn", now), Reservation(2, "Confirmed", now.AddMinutes(-45)),
            Reservation(3, "Confirmed", now.AddDays(1)), Reservation(4, "NoShow", now.AddHours(-2)));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        AssertConflict(await controller.CancelReservation(1, CancellationToken.None), "reservation_state_conflict");
        AssertConflict(await controller.CancelReservation(2, CancellationToken.None), "reservation_expired");
        AssertConflict(await controller.CancelReservation(4, CancellationToken.None), "reservation_expired");
        Assert.IsType<OkObjectResult>((await controller.CancelReservation(3, CancellationToken.None)).Result);
        Assert.IsType<OkObjectResult>((await controller.CancelReservation(3, CancellationToken.None)).Result);
        Assert.Equal(new[] { "CheckingIn", "Confirmed", "Cancelled", "NoShow" },
            await db.Reservations.OrderBy(x => x.ReservationId).Select(x => x.Status).ToArrayAsync());
    }

    // A2: the walk-in guard sees the earliest open reservation of that table in the window only.
    [Fact]
    public async Task Table_hold_returns_the_earliest_open_reservation_of_that_table_in_the_window()
    {
        await using var db = Database();
        var now = DateTime.UtcNow;
        db.Reservations.AddRange(
            Reservation(1, "Confirmed", now.AddMinutes(50), tableId: 7),
            Reservation(2, "Pending", now.AddMinutes(20), tableId: 7),
            Reservation(3, "Cancelled", now.AddMinutes(10), tableId: 7),
            Reservation(4, "Confirmed", now.AddMinutes(15), tableId: 8),
            Reservation(5, "Confirmed", now.AddMinutes(90), tableId: 9));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        var hold = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(
            await controller.GetTableHold(3, 7, now.AddMinutes(-30), now.AddMinutes(60), CancellationToken.None)).Value);
        Assert.Equal(2, hold.GetProperty("ReservationId").GetInt32());
        Assert.IsType<NotFoundResult>(await controller.GetTableHold(3, 9, now.AddMinutes(-30), now.AddMinutes(60), CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(await controller.GetTableHold(3, 7, now, now, CancellationToken.None));
    }

    private static Reservation Reservation(int id, string status, DateTime reservedAt, int tableId = 7) => new()
    {
        ReservationId = id, ReservationCode = "RSV-" + id, CustomerId = 11, CustomerName = "Khách thử", PhoneNumber = "0900000000",
        BranchId = 3, TableId = tableId, PartySize = 2, ReservedAt = reservedAt, ArrivalWindowMinutes = 30, Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static void AssertConflict(ActionResult<object> result, string code)
    {
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.DoesNotContain("?", body.GetProperty("message").GetString());
    }

    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Cancel and table-hold use only the reservation tables.
    private static CustomersController Controller(CustomersDbContext db) => new(db, null!, null!);
}
