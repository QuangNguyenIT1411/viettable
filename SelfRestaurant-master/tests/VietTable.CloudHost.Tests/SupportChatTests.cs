using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using SelfRestaurant.Deposits;
using SelfRestaurant.Gateway.Api.Controllers;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11 "Chat với nhân viên hỗ trợ": automatic replies, who sees what, flooding, read markers, resolving.</summary>
public sealed class SupportChatTests
{
    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static readonly TimeZoneInfo Vn = TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

    private static DateTime VnToUtc(int hour, int minute = 0) => new DateTime(2026, 11, 7, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    // ---------- automatic reply ----------

    [Fact]
    public void Inside_hours_says_a_few_minutes()
    {
        var text = SupportRules.AutoReply(new SupportRules.Hours(new TimeOnly(10, 0), new TimeOnly(22, 0)), VnToUtc(15), Vn);
        Assert.Equal("Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút.", text);
    }

    [Theory]
    [InlineData(23, "22:00", "10:00 ngày mai")]
    [InlineData(8, "22:00", "10:00 hôm nay")]
    public void Outside_hours_says_when_the_branch_opens(int hour, string to, string when)
    {
        var text = SupportRules.AutoReply(new SupportRules.Hours(new TimeOnly(10, 0), TimeOnly.Parse(to)), VnToUtc(hour), Vn);
        Assert.StartsWith("Tin nhắn tự động: Hiện ngoài giờ làm việc (10:00–22:00).", text);
        Assert.EndsWith($"lúc {when}.", text);
    }

    [Fact]
    public void Unknown_hours_promise_no_time()
    {
        Assert.Equal("Tin nhắn tự động: Nhân viên đã nhận được tin nhắn và sẽ trả lời bạn sớm nhất có thể.",
            SupportRules.AutoReply(null, VnToUtc(15), Vn));
    }

    [Theory]
    [InlineData("07:00 - 22:00", "07:00", "22:00")]
    [InlineData("7h-22h", "07:00", "22:00")]
    [InlineData("10:00–23:30", "10:00", "23:30")]
    [InlineData("10g đến 22g", "10:00", "22:00")]
    [InlineData("17:00 - 02:00", "17:00", "02:00")]
    public void Reads_branch_opening_hours_as_typed(string text, string from, string to)
    {
        var hours = SupportRules.ParseHours(text);
        Assert.NotNull(hours);
        Assert.Equal(TimeOnly.Parse(from), hours!.From);
        Assert.Equal(TimeOnly.Parse(to), hours.To);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Cả ngày")]
    [InlineData("10:00 - 10:00")]
    public void Unreadable_hours_are_unknown(string? text) => Assert.Null(SupportRules.ParseHours(text));

    [Fact]
    public void Hours_past_midnight_are_open_late_at_night()
    {
        var late = new SupportRules.Hours(new TimeOnly(17, 0), new TimeOnly(2, 0));
        Assert.True(SupportRules.IsOpen(late, new TimeOnly(1, 0)));
        Assert.False(SupportRules.IsOpen(late, new TimeOnly(9, 0)));
    }

    // ---------- content ----------

    [Theory]
    [InlineData("<b>Xin chào</b> nhà hàng", "Xin chào nhà hàng")]
    [InlineData("  Tôi   đã chuyển khoản  ", "Tôi đã chuyển khoản")]
    [InlineData("Dòng 1\nDòng 2", "Dòng 1\nDòng 2")]
    public void Html_is_removed_and_text_kept(string raw, string expected) => Assert.Equal(expected, SupportRules.CleanBody(raw).Body);

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("   ")]
    public void Script_or_empty_is_refused(string raw) => Assert.NotNull(SupportRules.CleanBody(raw).Error);

    [Fact]
    public void Too_long_is_refused() => Assert.NotNull(SupportRules.CleanBody(new string('a', SupportRules.MaxBodyLength + 1)).Error);

    // ---------- flow through the controller ----------

    private static async Task<int> Start(SupportChatController controller, int customerId = 10, int branchId = 1, string message = "Tôi đã chuyển khoản cọc")
    {
        var result = await controller.Start(new SupportChatController.StartRequest(customerId, "Quang", branchId, "DEPOSIT_TRANSFER", "DEPOSIT", 37,
            "Cọc đặt bàn #37 · 500.000đ", message, "10:00 - 22:00"), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
        return 0;
    }

    [Fact]
    public async Task Start_writes_the_message_and_one_automatic_reply()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller);
        var c = await db.SupportConversations.SingleAsync();
        Assert.Equal(SupportRules.Waiting, c.Status);
        Assert.NotNull(c.WaitingSinceUtc);
        var messages = await db.SupportMessages.OrderBy(m => m.SupportMessageId).ToListAsync();
        Assert.Equal(new[] { SupportRules.Customer, SupportRules.Auto }, messages.Select(m => m.SenderType));
        Assert.StartsWith("Tin nhắn tự động:", messages[1].Body);
        Assert.Equal("DEPOSIT", c.ContextType);
        Assert.Equal(37, c.ContextId);
    }

    [Fact]
    public async Task Admin_support_hours_win_over_branch_opening_hours()
    {
        await using var db = Database();
        // Support only 03:00–04:00: whenever the test runs (outside that hour), the reply says outside hours.
        db.SupportBranchHours.Add(new SupportBranchHours { BranchId = 1, OpenFrom = "03:00", OpenTo = "03:01", UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await Start(new SupportChatController(db));
        var auto = await db.SupportMessages.SingleAsync(m => m.SenderType == SupportRules.Auto);
        Assert.Contains("(03:00–03:01)", auto.Body);
    }

    [Fact]
    public async Task A_customer_sees_only_their_own_and_staff_only_their_branch()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller, customerId: 10, branchId: 1);
        var id = (await db.SupportConversations.SingleAsync()).SupportConversationId;

        Assert.IsType<OkObjectResult>((await controller.Get(id, 10, null, null)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Get(id, 11, null, null)).Result);       // another customer
        Assert.IsType<OkObjectResult>((await controller.Get(id, null, 1, null)).Result);              // staff of branch 1
        Assert.IsType<NotFoundObjectResult>((await controller.Get(id, null, 2, null)).Result);        // staff of branch 2
        Assert.IsType<NotFoundObjectResult>((await controller.Send(id, new SupportChatController.MessageRequest(11, null, null, null, "xin chào", null), CancellationToken.None)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Send(id, new SupportChatController.MessageRequest(null, 2, 5, "Lan", "chào bạn", null), CancellationToken.None)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Resolve(id, new SupportChatController.ResolveRequest(2, 5, "Lan"), CancellationToken.None)).Result);
        var other = (ObjectResult)(await controller.ForBranch(2, false, CancellationToken.None)).Result!;
        Assert.DoesNotContain("Quang", System.Text.Json.JsonSerializer.Serialize(other.Value));
    }

    [Fact]
    public async Task Flooding_is_slowed_down()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller);
        var id = (await db.SupportConversations.SingleAsync()).SupportConversationId;
        for (var i = 0; i < SupportRules.BurstMessages - 1; i++)
            Assert.IsType<OkObjectResult>((await controller.Send(id, new SupportChatController.MessageRequest(10, null, null, null, $"tin {i}", null), CancellationToken.None)).Result);
        var blocked = (await controller.Send(id, new SupportChatController.MessageRequest(10, null, null, null, "tin nữa", null), CancellationToken.None)).Result;
        Assert.Equal(429, ((ObjectResult)blocked!).StatusCode);
        Assert.Null(SupportRules.WhyTooFast(new[] { DateTime.UtcNow.AddMinutes(-2) }, DateTime.UtcNow));
    }

    [Fact]
    public async Task Too_many_new_conversations_in_an_hour_are_refused()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        for (var i = 0; i < SupportRules.NewConversationsPerHour; i++) await Start(controller);
        var blocked = await controller.Start(new SupportChatController.StartRequest(10, "Quang", 1, "OTHER", null, null, null, "lần nữa", null), CancellationToken.None);
        Assert.Equal(429, ((ObjectResult)blocked.Result!).StatusCode);
    }

    [Fact]
    public async Task Staff_reply_tells_the_customer_unread_counts_and_read_markers_move_forward()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller);
        var c = await db.SupportConversations.SingleAsync();
        Assert.Equal(1, SupportRules.UnreadForStaff(c, await db.SupportMessages.ToListAsync()));

        await controller.Read(c.SupportConversationId, new SupportChatController.ReadRequest(null, 1, 0), CancellationToken.None);
        Assert.Equal(0, SupportRules.UnreadForStaff(c, await db.SupportMessages.ToListAsync()));

        await controller.Send(c.SupportConversationId, new SupportChatController.MessageRequest(null, 1, 5, "Lan", "Chào bạn, mình kiểm tra ngay.", null), CancellationToken.None);
        Assert.Equal(SupportRules.InProgress, c.Status);
        Assert.Null(c.WaitingSinceUtc);
        Assert.True(await db.ReadyDishNotifications.AnyAsync(n => n.EventName == CustomerInbox.SupportReply && n.CustomerId == 10));
        var messages = await db.SupportMessages.ToListAsync();
        Assert.Equal(2, SupportRules.UnreadForCustomer(c, messages)); // the automatic reply and the staff answer
        var staffMessage = messages.Single(m => m.SenderType == SupportRules.Staff);
        Assert.Equal("Lan", staffMessage.SenderName);
        Assert.Equal(5, staffMessage.SenderEmployeeId);

        await controller.Read(c.SupportConversationId, new SupportChatController.ReadRequest(10, null, staffMessage.SupportMessageId), CancellationToken.None);
        Assert.Equal(0, SupportRules.UnreadForCustomer(c, await db.SupportMessages.ToListAsync()));
        await controller.Read(c.SupportConversationId, new SupportChatController.ReadRequest(10, null, 1), CancellationToken.None);
        Assert.Equal(staffMessage.SupportMessageId, c.CustomerLastReadMessageId); // never goes back
    }

    [Fact]
    public async Task Resolving_saves_who_and_when_and_a_new_customer_message_opens_it_again()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller);
        var c = await db.SupportConversations.SingleAsync();
        await controller.Resolve(c.SupportConversationId, new SupportChatController.ResolveRequest(1, 5, "Lan"), CancellationToken.None);
        Assert.Equal(SupportRules.Resolved, c.Status);
        Assert.Equal("Lan", c.ResolvedByName);
        Assert.NotNull(c.ResolvedAtUtc);
        var openList = (ObjectResult)(await controller.ForBranch(1, false, CancellationToken.None)).Result!;
        Assert.DoesNotContain("DEPOSIT_TRANSFER", System.Text.Json.JsonSerializer.Serialize(openList.Value));

        await controller.Send(c.SupportConversationId, new SupportChatController.MessageRequest(10, null, null, null, "Vẫn chưa thấy", null), CancellationToken.None);
        Assert.Equal(SupportRules.Waiting, c.Status);
        Assert.Null(c.ResolvedAtUtc);
        Assert.Equal(2, await db.SupportMessages.CountAsync(m => m.SenderType == SupportRules.Auto)); // a new wait → a new automatic reply
    }

    [Fact]
    public void Staff_list_puts_the_longest_wait_first()
    {
        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new SupportConversation { SupportConversationId = 1, Status = SupportRules.InProgress, LastMessageAtUtc = now },
            new SupportConversation { SupportConversationId = 2, Status = SupportRules.Waiting, WaitingSinceUtc = now.AddMinutes(-5), LastMessageAtUtc = now },
            new SupportConversation { SupportConversationId = 3, Status = SupportRules.Resolved, LastMessageAtUtc = now },
            new SupportConversation { SupportConversationId = 4, Status = SupportRules.Waiting, WaitingSinceUtc = now.AddMinutes(-30), LastMessageAtUtc = now },
        };
        Assert.Equal(new[] { 4, 2, 1, 3 }, SupportRules.StaffOrder(rows).Select(c => c.SupportConversationId));
    }

    [Fact]
    public async Task Older_messages_load_page_by_page()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        await Start(controller);
        var c = await db.SupportConversations.SingleAsync();
        for (var i = 0; i < 6; i++)
            await controller.Send(c.SupportConversationId, new SupportChatController.MessageRequest(null, 1, 5, "Lan", $"reply {i}", null), CancellationToken.None);
        var newest = (ObjectResult)(await controller.Get(c.SupportConversationId, 10, null, null, take: 3)).Result!;
        var json = System.Text.Json.JsonSerializer.Serialize(newest.Value);
        Assert.Contains("\"hasOlder\":true", json);
        Assert.Contains("reply 5", json);
        Assert.DoesNotContain("reply 2", json);
    }

    // ---------- payment checks and topics ----------

    [Theory]
    [InlineData(DepositRules.Paid, true)]
    [InlineData(DepositRules.Applied, true)]
    [InlineData(DepositRules.RefundRequired, true)]
    [InlineData(DepositRules.Refunded, true)]
    [InlineData(DepositRules.Pending, false)]
    [InlineData(DepositRules.Expired, false)]
    [InlineData(DepositRules.Cancelled, false)]
    public void A_deposit_counts_as_confirmed_once_the_money_arrived(string status, bool confirmed) =>
        Assert.Equal(confirmed, SupportChatContext.DepositConfirmed(status));

    [Fact]
    public void App_topics_and_server_topics_are_the_same_list()
    {
        Assert.Equal(SupportRules.Topics.Select(t => (t.Code, t.Label, t.ContextType)),
            SupportTopics.All.Select(t => (t.Code, t.Label, t.ContextType)));
        Assert.Equal(new[] { "DEPOSIT_TRANSFER", "BILL_TRANSFER" }, SupportTopics.All.Where(t => t.ChecksPayment).Select(t => t.Code));
    }

    [Fact]
    public async Task Quick_replies_of_another_branch_cannot_be_changed()
    {
        await using var db = Database();
        var controller = new SupportChatController(db);
        var created = (ObjectResult)(await controller.CreateQuickReply(new SupportChatController.QuickReplyRequest(1, "Đã nhận cọc", "Nhà hàng đã nhận được tiền cọc của bạn."), CancellationToken.None)).Result!;
        var id = (await db.SupportQuickReplies.SingleAsync()).SupportQuickReplyId;
        Assert.IsType<NotFoundObjectResult>((await controller.UpdateQuickReply(id, new SupportChatController.QuickReplyRequest(2, "x", "y"), CancellationToken.None)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.DeleteQuickReply(id, 2, CancellationToken.None)).Result);
        Assert.Equal(200, created.StatusCode ?? 200);
    }
}
