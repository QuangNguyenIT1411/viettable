using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using VietTable.CloudHost.Realtime;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>Reservation item 2 (26/10): the cashier confirms, rejects or marks a booking absent; a check-in confirms it.</summary>
public sealed class ReservationDecisionTests
{
    [Fact]
    public async Task Confirm_turns_a_pending_booking_confirmed_once_and_tells_the_guest()
    {
        await using var db = Database();
        db.Reservations.Add(Reservation(1, "Pending", DateTime.UtcNow.AddHours(3)));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        var body = Ok(await controller.ConfirmReservation(1, new(42), CancellationToken.None));
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(42, body.GetProperty("statusChangedByEmployeeId").GetInt32());
        Ok(await controller.ConfirmReservation(1, new(43), CancellationToken.None)); // a second click changes nothing

        var saved = await db.Reservations.SingleAsync();
        Assert.Equal(42, saved.StatusChangedByEmployeeId);
        var inbox = await db.ReadyDishNotifications.SingleAsync();
        Assert.Equal(CustomerInbox.ReservationConfirmed, inbox.EventName);
        Assert.Equal("Đặt bàn đã được xác nhận", CustomerInbox.Title(inbox.EventName));
        Assert.Contains("RSV-1", inbox.Message);
    }

    [Fact]
    public async Task Reject_needs_a_reason_frees_the_table_and_shows_the_reason_to_the_guest()
    {
        await using var db = Database();
        var at = DateTime.UtcNow.AddHours(3);
        db.Reservations.Add(Reservation(1, "Pending", at, tableId: 7));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        var missing = Assert.IsType<BadRequestObjectResult>((await controller.RejectReservation(1, new(42, "  "), CancellationToken.None)).Result);
        Assert.Equal("reason_required", JsonSerializer.SerializeToElement(missing.Value).GetProperty("code").GetString());

        var occupiedBefore = Assert.IsType<OkObjectResult>((await controller.GetOccupiedTableIds(3, at, at.AddHours(2), null, CancellationToken.None)).Result);
        Assert.Contains(7, (IEnumerable<int>)occupiedBefore.Value!);

        var body = Ok(await controller.RejectReservation(1, new(42, "Hết bàn"), CancellationToken.None));
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.Equal("Hết bàn", body.GetProperty("statusReason").GetString());

        var occupiedAfter = Assert.IsType<OkObjectResult>((await controller.GetOccupiedTableIds(3, at, at.AddHours(2), null, CancellationToken.None)).Result);
        Assert.DoesNotContain(7, (IEnumerable<int>)occupiedAfter.Value!);
        var inbox = await db.ReadyDishNotifications.SingleAsync();
        Assert.Equal(CustomerInbox.ReservationRejected, inbox.EventName);
        Assert.StartsWith("Đặt bàn bị từ chối: Hết bàn.", inbox.Message);
    }

    [Fact]
    public async Task Only_a_pending_booking_can_be_confirmed_or_rejected()
    {
        await using var db = Database();
        var later = DateTime.UtcNow.AddHours(3);
        db.Reservations.AddRange(Reservation(1, "Cancelled", later), Reservation(2, "CheckingIn", later),
            Reservation(3, "Confirmed", later), Reservation(4, "Pending", DateTime.UtcNow.AddMinutes(-45)), Reservation(5, "Rejected", later));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        AssertConflict(await controller.ConfirmReservation(1, new(42), CancellationToken.None), "reservation_state_conflict");
        AssertConflict(await controller.RejectReservation(2, new(42, "Khác"), CancellationToken.None), "reservation_state_conflict");
        AssertConflict(await controller.RejectReservation(3, new(42, "Khác"), CancellationToken.None), "reservation_state_conflict");
        AssertConflict(await controller.ConfirmReservation(4, new(42), CancellationToken.None), "reservation_expired");
        AssertConflict(await controller.ConfirmReservation(5, new(42), CancellationToken.None), "reservation_state_conflict");
        Assert.Equal(new[] { "Cancelled", "CheckingIn", "Confirmed", "Pending", "Rejected" },
            await db.Reservations.OrderBy(x => x.ReservationId).Select(x => x.Status).ToArrayAsync());
        Assert.Empty(await db.ReadyDishNotifications.ToListAsync());
    }

    [Fact]
    public async Task No_show_waits_for_the_reserved_time_and_cancels_the_dishes_ordered_ahead()
    {
        await using var db = Database();
        var due = Reservation(1, "Confirmed", DateTime.UtcNow.AddMinutes(-5));
        due.PreOrderItems.Add(new ReservationPreOrderItem { DishId = 9, DishNameSnapshot = "Phở", Quantity = 1, Status = "Pending", CreatedAtUtc = DateTime.UtcNow });
        db.Reservations.AddRange(due, Reservation(2, "Pending", DateTime.UtcNow.AddMinutes(20)), Reservation(3, "CheckedIn", DateTime.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        AssertConflict(await controller.MarkReservationNoShow(2, new(42), CancellationToken.None), "reservation_not_due");
        AssertConflict(await controller.MarkReservationNoShow(3, new(42), CancellationToken.None), "reservation_state_conflict");
        Assert.Equal("NoShow", Ok(await controller.MarkReservationNoShow(1, new(42), CancellationToken.None)).GetProperty("status").GetString());
        Ok(await controller.MarkReservationNoShow(1, new(42), CancellationToken.None));

        Assert.Equal("Cancelled", (await db.ReservationPreOrderItems.SingleAsync()).Status);
        Assert.Equal(CustomerInbox.ReservationNoShow, (await db.ReadyDishNotifications.SingleAsync()).EventName);
    }

    [Fact]
    public async Task Checking_in_a_pending_booking_confirms_it_and_records_how()
    {
        await using var db = Database();
        db.Reservations.AddRange(Reservation(1, "Pending", DateTime.UtcNow), Reservation(2, "Pending", DateTime.UtcNow),
            Reservation(3, "Confirmed", DateTime.UtcNow), Reservation(4, "Rejected", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var controller = Controller(db);

        Ok(await controller.BeginReservationCheckIn(1, null, CancellationToken.None));       // QR: no cashier
        Ok(await controller.BeginReservationCheckIn(2, 42, CancellationToken.None));         // at the counter
        Ok(await controller.BeginReservationCheckIn(3, 42, CancellationToken.None));
        Assert.IsType<ConflictObjectResult>((await controller.BeginReservationCheckIn(4, 42, CancellationToken.None)).Result);

        var rows = await db.Reservations.OrderBy(x => x.ReservationId).ToListAsync();
        Assert.Equal(new[] { "CheckingIn", "CheckingIn", "CheckingIn", "Rejected" }, rows.Select(x => x.Status));
        Assert.Equal(CustomersController.AutoConfirmedAtCheckIn, rows[0].StatusReason);
        Assert.Null(rows[0].StatusChangedByEmployeeId);
        Assert.Equal(42, rows[1].StatusChangedByEmployeeId);
        Assert.Null(rows[2].StatusReason); // confirmed earlier by a cashier: nothing to record
        Assert.Empty(await db.ReadyDishNotifications.ToListAsync()); // no "đã xác nhận" noise while checking in
    }

    [Fact]
    public async Task Pending_list_has_every_upcoming_day_of_that_branch_soonest_first()
    {
        await using var db = Database();
        var now = DateTime.UtcNow;
        db.Reservations.AddRange(Reservation(1, "Pending", now.AddDays(5)), Reservation(2, "Pending", now.AddHours(1)),
            Reservation(3, "Confirmed", now.AddHours(2)), Reservation(4, "Pending", now.AddHours(2), branchId: 4));
        await db.SaveChangesAsync();

        var list = Assert.IsType<OkObjectResult>((await Controller(db).GetPendingReservations(3, CancellationToken.None)).Result);
        var ids = JsonSerializer.SerializeToElement(list.Value).EnumerateArray().Select(x => x.GetProperty("reservationId").GetInt32());
        Assert.Equal(new[] { 2, 1 }, ids);
    }

    [Theory]
    [InlineData("notificationCreated")]
    [InlineData("reservationChanged")]
    public void Cloud_realtime_forwards_the_customer_inbox_and_reservation_events(string method) =>
        Assert.True(new PrivateGroupEvent(Guid.NewGuid().ToString("N"), "ABCDEF0123456789ABCDEF01", method, []).IsValid());

    private static Reservation Reservation(int id, string status, DateTime reservedAt, int tableId = 7, int branchId = 3) => new()
    {
        ReservationId = id, ReservationCode = "RSV-" + id, CustomerId = 11, CustomerName = "Khách thử", PhoneNumber = "0900000000",
        BranchId = branchId, TableId = tableId, PartySize = 2, ReservedAt = reservedAt, ArrivalWindowMinutes = 30, Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static JsonElement Ok(ActionResult<object> result) =>
        JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void AssertConflict(ActionResult<object> result, string code)
    {
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.DoesNotContain("?", body.GetProperty("message").GetString());
    }

    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CustomersController Controller(CustomersDbContext db) => new(db, null!, null!);
}
