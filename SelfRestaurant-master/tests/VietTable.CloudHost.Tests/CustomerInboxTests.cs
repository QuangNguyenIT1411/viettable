using System.Text.Json;
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

/// <summary>Notifications review 25/10: every event lands in the right tab, once, for its owner only.</summary>
public sealed class CustomerInboxTests
{
    private const int Owner = 21;

    [Theory]
    [InlineData(CustomerInbox.ReservationCreated, "reservation")]
    [InlineData(CustomerInbox.ReservationNoShow, "reservation")]
    [InlineData(CustomerInbox.KitchenStarted, "order")]
    [InlineData(CustomerInbox.OrderReady, "order")]
    [InlineData(CustomerInbox.ServiceCompleted, "order")]
    [InlineData(CustomerInbox.PaymentCancelled, "payment")]
    [InlineData(CustomerInbox.PaymentSucceeded, "payment")]
    [InlineData(CustomerInbox.PointsEarned, "offer")]
    [InlineData(CustomerInbox.TierUp, "offer")]
    public void Every_event_has_a_vietnamese_title_and_a_tab(string eventName, string category)
    {
        Assert.Equal(category, CustomerInbox.Category(eventName));
        Assert.True(CustomerInbox.IsKnown(eventName));
        Assert.NotEqual("Thông báo", CustomerInbox.Title(eventName));
        Assert.DoesNotContain("?", CustomerInbox.Title(eventName));
        Assert.False(CustomerInbox.IsKnown("order.submitted.v1")); // "Đã gửi bếp" was dropped (owner, 25/10)
    }

    [Fact]
    public async Task Reservation_cancel_writes_one_notification_and_the_list_returns_title_category_and_ref()
    {
        await using var db = Database();
        db.Reservations.Add(new Reservation { ReservationId = 10, ReservationCode = "RSV-10", CustomerId = Owner, CustomerName = "A",
            PhoneNumber = "0343000273", BranchId = 1, TableId = 8, PartySize = 2, ReservedAt = DateTime.UtcNow.AddDays(1),
            ArrivalWindowMinutes = 30, Status = "Confirmed", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var customers = new CustomersController(db, null!, null!);
        await customers.CancelReservation(10, CancellationToken.None);
        await customers.CancelReservation(10, CancellationToken.None); // repeat: no second notification

        var list = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(
            (await new MobileEngagementController(db).Notifications(Owner, null, 20, CancellationToken.None)).Result).Value);
        var items = list.GetProperty("items").EnumerateArray().ToArray();
        var row = Assert.Single(items);
        Assert.Equal("Đặt bàn đã hủy", row.GetProperty("title").GetString());
        Assert.Equal("reservation", row.GetProperty("category").GetString());
        Assert.Equal(10, row.GetProperty("refId").GetInt32());
        Assert.Contains("RSV-10", row.GetProperty("Message").GetString());
        Assert.Equal(1, list.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task Generic_rows_are_idempotent_validated_and_private_and_read_all_clears_the_bell()
    {
        await using var db = Database();
        var inbox = new MobileEngagementController(db);
        var request = new MobileEngagementController.AddNotificationRequest(CustomerInbox.KitchenStarted, 0, "Bếp đã nhận và đang chuẩn bị đơn #5.", 5);
        var first = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>((await inbox.AddNotification(Owner, request, CancellationToken.None)).Result).Value);
        var again = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>((await inbox.AddNotification(Owner, request, CancellationToken.None)).Result).Value);
        Assert.True(first.GetProperty("created").GetBoolean());
        Assert.False(again.GetProperty("created").GetBoolean());
        await inbox.AddNotification(Owner, new(CustomerInbox.ServiceCompleted, 3, "Yêu cầu giục bếp của bạn đã được nhân viên xử lý xong."), CancellationToken.None);
        await inbox.AddNotification(Owner + 1, new(CustomerInbox.PaymentCancelled, 9, "Thu ngân đã hủy giao dịch chờ 70.000đ."), CancellationToken.None);
        Assert.IsAssignableFrom<ObjectResult>((await inbox.AddNotification(Owner, new("order.submitted.v1", 0, "x"), CancellationToken.None)).Result);
        Assert.Equal(3, await db.ReadyDishNotifications.CountAsync());

        var read = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>((await inbox.ReadAllNotifications(Owner, CancellationToken.None)).Result).Value);
        Assert.Equal(2, read.GetProperty("read").GetInt32());
        Assert.Equal(1, await db.ReadyDishNotifications.CountAsync(x => x.Status == "OPEN")); // the other customer's row
        Assert.Equal(Owner + 1, (await db.ReadyDishNotifications.SingleAsync(x => x.Status == "OPEN")).CustomerId);
    }

    [Fact]
    public async Task Loyalty_writes_points_earned_and_a_tier_up_once()
    {
        await using var db = Database();
        var inbox = new MobileEngagementController(db);
        var request = new MobileEngagementController.LoyaltyNotificationRequest(PaymentId: 33, PointsBefore: 990, PointsEarned: 700, PointsAfter: 1690);
        await inbox.LoyaltySettled(Owner, request, CancellationToken.None);
        await inbox.LoyaltySettled(Owner, request, CancellationToken.None);
        var rows = await db.ReadyDishNotifications.OrderBy(x => x.ReadyDishNotificationId).ToListAsync();
        Assert.Equal(new[] { CustomerInbox.PointsEarned, CustomerInbox.TierUp }, rows.Select(x => x.EventName));
        Assert.Equal("Bạn được cộng 700 điểm thưởng. Tổng điểm hiện có: 1.690.", rows[0].Message);
        Assert.Equal("Chúc mừng! Bạn đã lên hạng Bạc.", rows[1].Message);
    }

    [Fact]
    public async Task Table_wide_ready_feed_never_shows_other_kinds_of_notifications()
    {
        await using var db = Database();
        db.ReadyDishNotifications.AddRange(
            new ReadyDishNotifications { OrderId = 5, OrderItemId = 1, CustomerId = Owner, TableId = 8, EventName = CustomerInbox.OrderReady, Message = "Phở đã sẵn sàng.", CreatedAtUtc = DateTime.UtcNow },
            new ReadyDishNotifications { OrderId = 0, OrderItemId = 10, CustomerId = Owner, EventName = CustomerInbox.ReservationCreated, Message = "Đặt bàn", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = await new CustomersController(db, null!, null!).GetReadyNotifications(Owner, null, "OPEN", CancellationToken.None);
        var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result.Result).Value,
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("Phở", json);
        Assert.DoesNotContain("Đặt bàn", json);
    }

    [Theory]
    [InlineData("0343000273", "0343000273")]
    [InlineData(" 0343 000 273 ", "0343000273")]
    [InlineData("0343-000-273", "0343000273")]
    [InlineData("(0343) 000.273", "0343000273")]
    [InlineData("+84 343 000 273", "0343000273")]
    [InlineData("84343000273", "0343000273")]
    [InlineData("034300027", null)]
    [InlineData("0343abc273", null)]
    [InlineData("03430+00273", null)]
    [InlineData("", null)]
    public void Contact_phone_is_normalised_like_the_app(string input, string? expected) =>
        Assert.Equal(expected, ReservationRules.NormalizePhone(input));

    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
}
