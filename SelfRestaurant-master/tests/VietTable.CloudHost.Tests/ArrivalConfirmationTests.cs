using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11: the booking reminder's "Sẽ đến" / "Huỷ lịch" buttons are stored and shown to the cashier.</summary>
public sealed class ArrivalConfirmationTests
{
    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static Reservation Booking(string status, DateTime reservedAt) => new()
    {
        ReservationId = 40, ReservationCode = "RSV-40", CustomerId = 21, CustomerName = "A", PhoneNumber = "0900000009",
        BranchId = 1, TableId = 8, PartySize = 2, ReservedAt = reservedAt, ArrivalWindowMinutes = 30, Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Confirmed", true)]
    [InlineData("PendingDeposit", true)]
    [InlineData("Cancelled", false)]
    [InlineData("Rejected", false)]
    [InlineData("NoShow", false)]
    [InlineData("CheckedIn", false)]
    public void Only_open_bookings_can_be_confirmed(string status, bool allowed)
    {
        var now = DateTime.UtcNow;
        Assert.Equal(allowed, ArrivalRules.WhyNotConfirmable(status, now.AddHours(1), 30, now) is null);
    }

    [Fact]
    public void A_booking_past_its_arrival_window_cannot_be_confirmed()
    {
        var now = DateTime.UtcNow;
        Assert.Null(ArrivalRules.WhyNotConfirmable("Confirmed", now.AddMinutes(-29), 30, now));
        Assert.Equal("Đặt bàn đã quá giờ hẹn.", ArrivalRules.WhyNotConfirmable("Confirmed", now.AddMinutes(-31), 30, now));
    }

    [Fact]
    public async Task Will_come_is_stored_with_its_time_and_returned()
    {
        await using var db = Database();
        db.Reservations.Add(Booking("Confirmed", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();
        var result = await new CustomersController(db, null!, null!).ConfirmArrival(40,
            new CustomersController.ArrivalConfirmationRequest(21), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
        var saved = await db.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ArrivalRules.WillCome, saved.ArrivalConfirmation);
        Assert.NotNull(saved.ArrivalConfirmedAtUtc);
        Assert.Equal("Confirmed", saved.Status);
    }

    [Fact]
    public async Task Another_customer_cannot_answer_for_the_booking()
    {
        await using var db = Database();
        db.Reservations.Add(Booking("Confirmed", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();
        var result = await new CustomersController(db, null!, null!).ConfirmArrival(40,
            new CustomersController.ArrivalConfirmationRequest(99), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(ArrivalRules.None, (await db.Reservations.AsNoTracking().SingleAsync()).ArrivalConfirmation);
    }

    [Fact]
    public async Task Cancel_from_the_reminder_cancels_and_records_the_answer()
    {
        await using var db = Database();
        db.Reservations.Add(Booking("Confirmed", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();
        await new CustomersController(db, null!, null!).CancelReservation(40, CancellationToken.None, viaReminder: true);
        var saved = await db.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", saved.Status);
        Assert.Equal(ArrivalRules.Cancelled, saved.ArrivalConfirmation);
    }

    [Fact]
    public async Task An_ordinary_cancel_leaves_the_answer_alone()
    {
        await using var db = Database();
        db.Reservations.Add(Booking("Confirmed", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();
        await new CustomersController(db, null!, null!).CancelReservation(40, CancellationToken.None);
        Assert.Equal(ArrivalRules.None, (await db.Reservations.AsNoTracking().SingleAsync()).ArrivalConfirmation);
    }
}
