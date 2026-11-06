using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11 waitlist: sign-up order, holds that expire, one winner when two act at once.</summary>
public sealed class WaitlistTests
{
    private static CustomersDbContext Database(string? name = null) => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static WaitlistEntry Entry(int id, int customer, DateTime created, int party = 4, string status = WaitlistRules.Waiting,
        DateTime? start = null) => new()
    {
        WaitlistEntryId = id, CustomerId = customer, ContactName = "Khách " + id, PhoneNumber = "0900000009", BranchId = 1,
        WindowStartUtc = start ?? DateTime.UtcNow.AddHours(3), WindowEndUtc = (start ?? DateTime.UtcNow.AddHours(3)).AddHours(2),
        PartySize = party, Status = status, CreatedAtUtc = created
    };

    [Fact]
    public void Queue_is_sign_up_order_and_skips_finished_or_past_windows()
    {
        var now = DateTime.UtcNow;
        var queue = WaitlistRules.Queue(new[]
        {
            Entry(3, 30, now.AddMinutes(-5)),
            Entry(1, 10, now.AddMinutes(-30)),
            Entry(2, 20, now.AddMinutes(-20), status: WaitlistRules.Offered),
            Entry(4, 40, now.AddMinutes(-40), start: now.AddHours(-3)), // window already over
            Entry(5, 50, now.AddMinutes(-30)),
        }, now);
        Assert.Equal(new[] { 1, 5, 3 }, queue.Select(x => x.WaitlistEntryId).ToArray());
    }

    [Fact]
    public void Position_counts_only_earlier_guests_still_waiting_at_the_branch_that_day()
    {
        var now = DateTime.UtcNow;
        var all = new[] { Entry(1, 10, now.AddMinutes(-30)), Entry(2, 20, now.AddMinutes(-20)), Entry(3, 30, now.AddMinutes(-10)),
            Entry(4, 40, now.AddMinutes(-25), status: WaitlistRules.Cancelled) };
        Assert.Equal(1, WaitlistRules.Position(all[0], all, ReservationRules.RestaurantTimeZone));
        Assert.Equal(3, WaitlistRules.Position(all[2], all, ReservationRules.RestaurantTimeZone));
        Assert.Equal(0, WaitlistRules.Position(all[3], all, ReservationRules.RestaurantTimeZone));
    }

    [Theory]
    [InlineData(0, 2, "Số khách phải từ 1 đến 30.")]
    [InlineData(4, -1, "Giờ kết thúc phải sau giờ bắt đầu.")]
    [InlineData(4, 7, "Khoảng giờ chờ tối đa 6 tiếng.")]
    public void Join_refuses_bad_requests(int party, int windowHours, string message)
    {
        var now = DateTime.UtcNow;
        Assert.Equal(message, WaitlistRules.WhyNotJoinable(now.AddHours(2), now.AddHours(2 + windowHours), party, 0, now));
    }

    [Fact]
    public void A_fourth_active_entry_is_refused()
    {
        var now = DateTime.UtcNow;
        Assert.NotNull(WaitlistRules.WhyNotJoinable(now.AddHours(2), now.AddHours(3), 4, 3, now));
        Assert.Null(WaitlistRules.WhyNotJoinable(now.AddHours(2), now.AddHours(3), 4, 2, now));
    }

    [Fact]
    public async Task Offer_holds_the_table_and_accept_turns_it_into_a_booking()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        var at = DateTime.UtcNow.AddHours(3);

        Assert.IsType<OkObjectResult>((await controller.Offer(1, new WaitlistController.OfferRequest(8, at, 10), CancellationToken.None)).Result);
        var hold = await db.Reservations.SingleAsync();
        Assert.Equal(WaitlistRules.HoldStatus, hold.Status);
        Assert.NotNull(hold.HoldExpiresAtUtc);
        Assert.Equal(WaitlistRules.Offered, (await db.WaitlistEntries.SingleAsync()).Status);
        Assert.True(await db.ReadyDishNotifications.AnyAsync(x => x.EventName == CustomerInbox.WaitlistOffered && x.CustomerId == 10));

        Assert.IsType<OkObjectResult>((await controller.Accept(1, new WaitlistController.AcceptRequest(10), CancellationToken.None)).Result);
        var booking = await db.Reservations.SingleAsync();
        Assert.Equal("Pending", booking.Status);
        Assert.Null(booking.HoldExpiresAtUtc);
        Assert.StartsWith("RSV-", booking.ReservationCode);
        Assert.Equal(WaitlistRules.Accepted, (await db.WaitlistEntries.SingleAsync()).Status);
    }

    [Fact]
    public async Task Accept_with_a_deposit_waits_for_it_and_a_second_accept_returns_the_same_booking()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30), party: 8));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 10), CancellationToken.None);
        var request = new WaitlistController.AcceptRequest(10, CustomersController.PendingDeposit, 400_000m, DateTime.UtcNow.AddMinutes(15));
        await controller.Accept(1, request, CancellationToken.None);
        var again = await controller.Accept(1, request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(again.Result);
        var booking = await db.Reservations.SingleAsync();
        Assert.Equal(CustomersController.PendingDeposit, booking.Status);
        Assert.Equal(400_000m, booking.DepositAmount);
    }

    [Fact]
    public async Task Another_guest_cannot_accept_someone_elses_offer()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 10), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>((await controller.Accept(1, new WaitlistController.AcceptRequest(99), CancellationToken.None)).Result);
        Assert.Equal(WaitlistRules.Offered, (await db.WaitlistEntries.SingleAsync()).Status);
    }

    [Fact]
    public async Task Two_requests_acting_on_the_same_entry_at_once_leave_one_winner()
    {
        var name = Guid.NewGuid().ToString();
        await using (var seed = Database(name))
        {
            seed.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
            await seed.SaveChangesAsync();
            await new WaitlistController(seed).Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 10), CancellationToken.None);
        }
        // Both contexts read the Offered entry before either saves (two taps, two devices).
        await using var first = Database(name);
        await using var second = Database(name);
        var a = await first.WaitlistEntries.SingleAsync();
        var b = await second.WaitlistEntries.SingleAsync();
        a.Status = WaitlistRules.Accepted; a.Version++;
        b.Status = WaitlistRules.Cancelled; b.Version++;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = Database(name);
        Assert.Equal(WaitlistRules.Accepted, (await check.WaitlistEntries.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_table_already_booked_at_that_time_is_not_offered()
    {
        await using var db = Database();
        var at = DateTime.UtcNow.AddHours(3);
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        db.Reservations.Add(new Reservation { ReservationCode = "RSV-1", CustomerName = "B", PhoneNumber = "0900000001", BranchId = 1,
            TableId = 8, PartySize = 2, ReservedAt = at.AddMinutes(30), Status = "Confirmed", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = await new WaitlistController(db).Offer(1, new WaitlistController.OfferRequest(8, at, 10), CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("reservation_slot_unavailable", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
        Assert.Equal(WaitlistRules.Waiting, (await db.WaitlistEntries.SingleAsync()).Status);
    }

    [Fact]
    public async Task An_unanswered_offer_expires_frees_the_table_and_tells_the_guest()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 10), CancellationToken.None);
        // The 10 minutes run out.
        var entry = await db.WaitlistEntries.SingleAsync();
        entry.OfferExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var hold = await db.Reservations.SingleAsync();
        hold.HoldExpiresAtUtc = entry.OfferExpiresAtUtc;
        await db.SaveChangesAsync();

        await controller.Mine(10, CancellationToken.None); // any read closes stale offers
        Assert.Equal(WaitlistRules.Expired, (await db.WaitlistEntries.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("Cancelled", (await db.Reservations.AsNoTracking().SingleAsync()).Status);
        Assert.True(await db.ReadyDishNotifications.AnyAsync(x => x.EventName == CustomerInbox.WaitlistExpired));
        var late = await controller.Accept(1, new WaitlistController.AcceptRequest(10), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(late.Result);
    }

    [Theory]
    [InlineData(null, false, 20)]
    [InlineData(null, true, 10)]
    [InlineData("", false, 20)]
    [InlineData("15", false, 15)]
    [InlineData("15", true, 15)]
    [InlineData("0", true, 10)]
    [InlineData("90", false, 20)]
    [InlineData("abc", false, 20)]
    public void Hold_minutes_are_20_without_push_10_with_fcm_and_configurable(string? configured, bool fcm, int minutes)
    {
        Assert.Equal(minutes, WaitlistCoordinator.ResolveOfferMinutes(configured, fcm));
    }

    [Fact]
    public async Task After_an_offer_lapses_the_guest_can_rejoin_at_the_end_of_the_queue_once()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        db.WaitlistEntries.Add(Entry(2, 11, DateTime.UtcNow.AddMinutes(-20)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 20), CancellationToken.None);
        var stale = await db.WaitlistEntries.SingleAsync(x => x.WaitlistEntryId == 1);
        stale.OfferExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        (await db.Reservations.SingleAsync()).HoldExpiresAtUtc = stale.OfferExpiresAtUtc;
        await db.SaveChangesAsync();

        var first = await controller.Rejoin(1, new WaitlistController.CustomerRequest(10), CancellationToken.None);
        Assert.IsType<OkObjectResult>(first.Result);
        Assert.Equal(WaitlistRules.Expired, (await db.WaitlistEntries.AsNoTracking().SingleAsync(x => x.WaitlistEntryId == 1)).Status);
        var again = await db.WaitlistEntries.AsNoTracking().Where(x => x.CustomerId == 10 && x.Status == WaitlistRules.Waiting).ToListAsync();
        Assert.Single(again);
        var queue = WaitlistRules.Queue(await db.WaitlistEntries.AsNoTracking().ToListAsync(), DateTime.UtcNow);
        Assert.Equal(new[] { 2, again[0].WaitlistEntryId }, queue.Select(x => x.WaitlistEntryId).ToArray()); // behind guest 2

        await controller.Rejoin(1, new WaitlistController.CustomerRequest(10), CancellationToken.None); // a second tap
        Assert.Single(await db.WaitlistEntries.Where(x => x.CustomerId == 10 && x.Status == WaitlistRules.Waiting).ToListAsync());
        var other = await controller.Rejoin(1, new WaitlistController.CustomerRequest(11), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(other.Result);
    }

    [Fact]
    public void Rejoin_only_after_a_lapsed_offer_and_while_the_window_is_ahead()
    {
        var now = DateTime.UtcNow;
        Assert.Null(WaitlistRules.WhyNotRejoinable(Entry(1, 10, now, status: WaitlistRules.Expired), 0, now));
        Assert.Equal("Bạn vẫn đang trong danh sách chờ.", WaitlistRules.WhyNotRejoinable(Entry(1, 10, now), 0, now));
        Assert.NotNull(WaitlistRules.WhyNotRejoinable(Entry(1, 10, now, status: WaitlistRules.Cancelled), 0, now));
        Assert.NotNull(WaitlistRules.WhyNotRejoinable(Entry(1, 10, now, status: WaitlistRules.Expired, start: now.AddHours(-3)), 0, now));
        Assert.NotNull(WaitlistRules.WhyNotRejoinable(Entry(1, 10, now, status: WaitlistRules.Expired), WaitlistRules.MaxActivePerCustomer, now));
    }

    [Fact]
    public async Task Cancelling_an_offer_releases_the_held_table()
    {
        await using var db = Database();
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, DateTime.UtcNow.AddHours(3), 10), CancellationToken.None);
        await controller.Cancel(1, new WaitlistController.CustomerRequest(10), CancellationToken.None);
        Assert.Equal(WaitlistRules.Cancelled, (await db.WaitlistEntries.SingleAsync()).Status);
        Assert.Equal("Cancelled", (await db.Reservations.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_held_table_is_not_free_for_others_until_it_expires()
    {
        await using var db = Database();
        var at = DateTime.UtcNow.AddHours(3);
        db.WaitlistEntries.Add(Entry(1, 10, DateTime.UtcNow.AddMinutes(-30)));
        db.WaitlistEntries.Add(Entry(2, 20, DateTime.UtcNow.AddMinutes(-20)));
        await db.SaveChangesAsync();
        var controller = new WaitlistController(db);
        await controller.Offer(1, new WaitlistController.OfferRequest(8, at, 10), CancellationToken.None);
        var second = await controller.Offer(2, new WaitlistController.OfferRequest(8, at, 10), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(second.Result);

        var occupied = await new CustomersController(db, null!, null!).GetOccupiedTableIds(1, at, at.AddHours(2), null, CancellationToken.None);
        Assert.Contains(8, Assert.IsAssignableFrom<IReadOnlyList<int>>(Assert.IsType<OkObjectResult>(occupied.Result).Value));
    }

    [Fact]
    public void Waitlist_times_are_half_hours_inside_the_window_and_booking_hours()
    {
        var zone = ReservationRules.RestaurantTimeZone;
        var start = new DateTimeOffset(2026, 11, 20, 18, 10, 0, TimeSpan.FromHours(7));
        var slots = WaitlistSlots.Candidates(start.UtcDateTime, start.AddHours(4).UtcDateTime,
            new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.FromHours(7)), new TimeOnly(10, 0), new TimeOnly(21, 30), zone);
        Assert.Equal(new[] { "18:30", "19:00", "19:30", "20:00", "20:30", "21:00", "21:30" },
            slots.Select(x => TimeZoneInfo.ConvertTime(x, zone).ToString("HH:mm")).ToArray());
    }
}
