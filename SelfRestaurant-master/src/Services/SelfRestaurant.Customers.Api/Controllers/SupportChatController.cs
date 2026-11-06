using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Controllers;

/// <summary>
/// "Chat với nhân viên hỗ trợ" (owner 06/11). Internal API for the Gateway, which checks who is asking (the customer
/// from the token, staff and their branch from the staff session) and passes it here; this service keeps the data and
/// enforces that a customer sees only their conversations and staff only their branch.
/// </summary>
[ApiController]
[Route("api/internal/support")]
public sealed class SupportChatController(CustomersDbContext db) : ControllerBase
{
    public const string ReplyEvent = CustomerInbox.SupportReply;

    public sealed record StartRequest(int CustomerId, string? CustomerName, int BranchId, string? Topic, string? ContextType,
        int? ContextId, string? ContextLabel, string? Message, string? BranchOpeningHours);
    public sealed record MessageRequest(int? CustomerId, int? StaffBranchId, int? EmployeeId, string? EmployeeName, string? Body,
        string? BranchOpeningHours);
    public sealed record ReadRequest(int? CustomerId, int? StaffBranchId, long LastMessageId);
    public sealed record ResolveRequest(int StaffBranchId, int EmployeeId, string? EmployeeName);
    public sealed record QuickReplyRequest(int? BranchId, string? Title, string? Body, int SortOrder = 0, bool IsActive = true);
    public sealed record HoursRequest(string? OpenFrom, string? OpenTo, string? UpdatedByName);

    [HttpPost("conversations")]
    public async Task<ActionResult<object>> Start([FromBody] StartRequest? r, CancellationToken ct)
    {
        if (r is null || r.CustomerId <= 0 || r.BranchId <= 0) return Bad("Thiếu thông tin cuộc trò chuyện.");
        var topic = SupportRules.FindTopic(r.Topic);
        if (topic is null) return Bad("Chủ đề không hợp lệ.");
        var contextType = string.IsNullOrWhiteSpace(r.ContextType) ? "NONE" : r.ContextType.Trim().ToUpperInvariant();
        if (!SupportRules.ContextTypes.Contains(contextType) || (contextType != "NONE" && r.ContextId is not > 0)) return Bad("Thông tin đính kèm không hợp lệ.");
        var (body, error) = SupportRules.CleanBody(r.Message);
        if (error is not null) return Bad(error);
        var now = DateTime.UtcNow;
        var recent = await db.SupportConversations.CountAsync(x => x.CustomerId == r.CustomerId && x.CreatedAtUtc > now.AddHours(-1), ct);
        var tooMany = SupportRules.WhyNoNewConversation(recent);
        if (tooMany is not null) return StatusCode(429, new { code = "support_rate_limited", message = tooMany });
        var name = string.IsNullOrWhiteSpace(r.CustomerName) ? "Khách" : r.CustomerName.Trim();
        var conversation = new SupportConversation
        {
            CustomerId = r.CustomerId, CustomerNameSnapshot = name.Length > 200 ? name[..200] : name, BranchId = r.BranchId,
            Topic = topic.Code, ContextType = contextType, ContextId = contextType == "NONE" ? null : r.ContextId,
            ContextLabel = Short(r.ContextLabel, 300), Status = SupportRules.Waiting, CreatedAtUtc = now, UpdatedAtUtc = now,
            LastMessageAtUtc = now
        };
        SupportRules.OnCustomerMessage(conversation, now);
        db.SupportConversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        db.SupportMessages.Add(new SupportMessage { SupportConversationId = conversation.SupportConversationId, SenderType = SupportRules.Customer, Body = body!, CreatedAtUtc = now });
        db.SupportMessages.Add(new SupportMessage { SupportConversationId = conversation.SupportConversationId, SenderType = SupportRules.Auto,
            Body = SupportRules.AutoReply(await HoursAsync(r.BranchId, r.BranchOpeningHours, ct), now, Zone()), CreatedAtUtc = now.AddMilliseconds(1) });
        await db.SaveChangesAsync(ct);
        return Ok(await MapDetailAsync(conversation, null, 40, forStaff: false, ct));
    }

    [HttpGet("customers/{customerId:int}/conversations")]
    public async Task<ActionResult<object>> Mine(int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return Bad("Khách hàng không hợp lệ.");
        var rows = await db.SupportConversations.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.LastMessageAtUtc).Take(50).ToListAsync(ct);
        return Ok(new { items = await MapListAsync(rows, forStaff: false, ct) });
    }

    [HttpGet("branches/{branchId:int}/conversations")]
    public async Task<ActionResult<object>> ForBranch(int branchId, [FromQuery] bool includeResolved, CancellationToken ct)
    {
        if (branchId <= 0) return Bad("Chi nhánh không hợp lệ.");
        var since = DateTime.UtcNow.AddDays(-7);
        var rows = await db.SupportConversations.AsNoTracking()
            .Where(x => x.BranchId == branchId && (x.Status != SupportRules.Resolved || (includeResolved && x.UpdatedAtUtc >= since)))
            .Take(200).ToListAsync(ct);
        var ordered = SupportRules.StaffOrder(rows).ToList();
        return Ok(new { items = await MapListAsync(ordered, forStaff: true, ct) });
    }

    /// <summary>One conversation with its newest messages, or the page before <paramref name="beforeId"/> (older ones).</summary>
    [HttpGet("conversations/{id:int}")]
    public async Task<ActionResult<object>> Get(int id, [FromQuery] int? customerId, [FromQuery] int? staffBranchId,
        [FromQuery] long? beforeId, [FromQuery] int take = 40, CancellationToken ct = default)
    {
        var c = await db.SupportConversations.AsNoTracking().FirstOrDefaultAsync(x => x.SupportConversationId == id, ct);
        if (c is null || !SupportRules.CanSee(c, customerId, staffBranchId)) return NotFound(new { code = "support_not_found", message = "Không tìm thấy cuộc trò chuyện." });
        return Ok(await MapDetailAsync(c, beforeId, Math.Clamp(take, 1, 100), forStaff: staffBranchId is > 0, ct));
    }

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<ActionResult<object>> Send(int id, [FromBody] MessageRequest? r, CancellationToken ct)
    {
        if (r is null) return Bad("Thiếu nội dung.");
        var c = await db.SupportConversations.FirstOrDefaultAsync(x => x.SupportConversationId == id, ct);
        if (c is null || !SupportRules.CanSee(c, r.CustomerId, r.StaffBranchId)) return NotFound(new { code = "support_not_found", message = "Không tìm thấy cuộc trò chuyện." });
        var (body, error) = SupportRules.CleanBody(r.Body);
        if (error is not null) return Bad(error);
        var now = DateTime.UtcNow;
        var fromStaff = r.StaffBranchId is > 0;
        if (fromStaff && r.EmployeeId is not > 0) return Bad("Thiếu nhân viên.");
        if (!fromStaff)
        {
            var recent = await db.SupportMessages.AsNoTracking()
                .Where(x => x.SupportConversationId == id && x.SenderType == SupportRules.Customer && x.CreatedAtUtc > now - SupportRules.BurstWindow)
                .Select(x => x.CreatedAtUtc).ToListAsync(ct);
            var tooFast = SupportRules.WhyTooFast(recent, now);
            if (tooFast is not null) return StatusCode(429, new { code = "support_rate_limited", message = tooFast });
        }
        var wasAnswered = c.WaitingSinceUtc is null; // staff had answered (or nobody waits): a new wait starts → automatic reply
        var message = new SupportMessage
        {
            SupportConversationId = id, SenderType = fromStaff ? SupportRules.Staff : SupportRules.Customer,
            SenderEmployeeId = fromStaff ? r.EmployeeId : null, SenderName = fromStaff ? Short(r.EmployeeName, 200) : null,
            Body = body!, CreatedAtUtc = now
        };
        db.SupportMessages.Add(message);
        if (fromStaff) SupportRules.OnStaffMessage(c, now); else SupportRules.OnCustomerMessage(c, now);
        c.Version++;
        await db.SaveChangesAsync(ct);
        if (fromStaff)
        {
            c.StaffLastReadMessageId = SupportRules.Advance(c.StaffLastReadMessageId, message.SupportMessageId);
            await CustomerInbox.AddAsync(db, c.CustomerId, ReplyEvent, (int)Math.Min(int.MaxValue, message.SupportMessageId),
                $"Nhân viên đã trả lời: {Short(body, 120)}", 0, ct);
        }
        else
        {
            c.CustomerLastReadMessageId = SupportRules.Advance(c.CustomerLastReadMessageId, message.SupportMessageId);
            if (wasAnswered)
                db.SupportMessages.Add(new SupportMessage { SupportConversationId = id, SenderType = SupportRules.Auto,
                    Body = SupportRules.AutoReply(await HoursAsync(c.BranchId, r.BranchOpeningHours, ct), now, Zone()), CreatedAtUtc = now.AddMilliseconds(1) });
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { conversation = await MapOneAsync(c, forStaff: fromStaff, ct), message = MapMessage(message) });
    }

    [HttpPost("conversations/{id:int}/read")]
    public async Task<ActionResult<object>> Read(int id, [FromBody] ReadRequest? r, CancellationToken ct)
    {
        if (r is null) return Bad("Thiếu thông tin.");
        var c = await db.SupportConversations.FirstOrDefaultAsync(x => x.SupportConversationId == id, ct);
        if (c is null || !SupportRules.CanSee(c, r.CustomerId, r.StaffBranchId)) return NotFound(new { code = "support_not_found", message = "Không tìm thấy cuộc trò chuyện." });
        var newest = await db.SupportMessages.Where(x => x.SupportConversationId == id).MaxAsync(x => (long?)x.SupportMessageId, ct) ?? 0;
        var seen = Math.Min(r.LastMessageId <= 0 ? newest : r.LastMessageId, newest);
        if (r.StaffBranchId is > 0) c.StaffLastReadMessageId = SupportRules.Advance(c.StaffLastReadMessageId, seen);
        else c.CustomerLastReadMessageId = SupportRules.Advance(c.CustomerLastReadMessageId, seen);
        await db.SaveChangesAsync(ct);
        return Ok(await MapOneAsync(c, forStaff: r.StaffBranchId is > 0, ct));
    }

    [HttpPost("conversations/{id:int}/resolve")]
    public async Task<ActionResult<object>> Resolve(int id, [FromBody] ResolveRequest? r, CancellationToken ct)
    {
        if (r is null || r.StaffBranchId <= 0 || r.EmployeeId <= 0) return Bad("Thiếu nhân viên.");
        var c = await db.SupportConversations.FirstOrDefaultAsync(x => x.SupportConversationId == id, ct);
        if (c is null || !SupportRules.CanSee(c, null, r.StaffBranchId)) return NotFound(new { code = "support_not_found", message = "Không tìm thấy cuộc trò chuyện." });
        if (c.Status != SupportRules.Resolved)
        {
            var now = DateTime.UtcNow;
            SupportRules.Resolve(c, r.EmployeeId, Short(r.EmployeeName, 200), now);
            c.Version++;
            await db.SaveChangesAsync(ct);
        }
        return Ok(await MapOneAsync(c, forStaff: true, ct));
    }

    // ---------- quick replies and hours (admin) ----------

    [HttpGet("quick-replies")]
    public async Task<ActionResult<object>> QuickReplies([FromQuery] int? branchId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var rows = await db.SupportQuickReplies.AsNoTracking()
            .Where(x => (x.BranchId == null || x.BranchId == branchId) && (includeInactive || x.IsActive))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.SupportQuickReplyId).ToListAsync(ct);
        return Ok(new { items = rows.Select(x => new { x.SupportQuickReplyId, x.BranchId, x.Title, x.Body, x.SortOrder, x.IsActive }) });
    }

    [HttpPost("quick-replies")]
    public async Task<ActionResult<object>> CreateQuickReply([FromBody] QuickReplyRequest? r, CancellationToken ct) => await SaveQuickReply(null, r, ct);

    [HttpPut("quick-replies/{id:int}")]
    public async Task<ActionResult<object>> UpdateQuickReply(int id, [FromBody] QuickReplyRequest? r, CancellationToken ct) => await SaveQuickReply(id, r, ct);

    [HttpDelete("quick-replies/{id:int}")]
    public async Task<ActionResult<object>> DeleteQuickReply(int id, [FromQuery] int? branchId, CancellationToken ct)
    {
        var row = await db.SupportQuickReplies.FirstOrDefaultAsync(x => x.SupportQuickReplyId == id, ct);
        if (row is null || (branchId is > 0 && row.BranchId is not null && row.BranchId != branchId)) return NotFound(new { code = "not_found", message = "Không tìm thấy câu trả lời mẫu." });
        db.SupportQuickReplies.Remove(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { deleted = id });
    }

    private async Task<ActionResult<object>> SaveQuickReply(int? id, QuickReplyRequest? r, CancellationToken ct)
    {
        var title = r?.Title?.Trim();
        var (body, error) = SupportRules.CleanBody(r?.Body);
        if (string.IsNullOrEmpty(title) || title.Length > 100) return Bad("Tiêu đề từ 1 đến 100 ký tự.");
        if (error is not null) return Bad(error);
        var now = DateTime.UtcNow;
        SupportQuickReply? row;
        if (id is null) { row = new SupportQuickReply { CreatedAtUtc = now }; db.SupportQuickReplies.Add(row); }
        else
        {
            row = await db.SupportQuickReplies.FirstOrDefaultAsync(x => x.SupportQuickReplyId == id, ct);
            if (row is null || (r!.BranchId is > 0 && row.BranchId is not null && row.BranchId != r.BranchId)) return NotFound(new { code = "not_found", message = "Không tìm thấy câu trả lời mẫu." });
        }
        row.BranchId = r!.BranchId is > 0 ? r.BranchId : null;
        row.Title = title; row.Body = body!; row.SortOrder = Math.Clamp(r.SortOrder, 0, 999); row.IsActive = r.IsActive; row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return Ok(new { row.SupportQuickReplyId, row.BranchId, row.Title, row.Body, row.SortOrder, row.IsActive });
    }

    [HttpGet("hours/{branchId:int}")]
    public async Task<ActionResult<object>> GetHours(int branchId, CancellationToken ct)
    {
        var row = await db.SupportBranchHours.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        return Ok(new { branchId, openFrom = row?.OpenFrom, openTo = row?.OpenTo, updatedAtUtc = row?.UpdatedAtUtc, updatedByName = row?.UpdatedByName });
    }

    [HttpPut("hours/{branchId:int}")]
    public async Task<ActionResult<object>> SaveHours(int branchId, [FromBody] HoursRequest? r, CancellationToken ct)
    {
        if (branchId <= 0 || r is null) return Bad("Thiếu thông tin.");
        var row = await db.SupportBranchHours.FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (string.IsNullOrWhiteSpace(r.OpenFrom) && string.IsNullOrWhiteSpace(r.OpenTo))
        {
            if (row is not null) { db.SupportBranchHours.Remove(row); await db.SaveChangesAsync(ct); }
            return Ok(new { branchId, openFrom = (string?)null, openTo = (string?)null });
        }
        if (!TimeOnly.TryParseExact(r.OpenFrom?.Trim(), "HH:mm", out var from) || !TimeOnly.TryParseExact(r.OpenTo?.Trim(), "HH:mm", out var to) || from == to)
            return Bad("Giờ hỗ trợ theo dạng HH:mm, giờ mở và giờ đóng khác nhau.");
        row ??= db.SupportBranchHours.Add(new SupportBranchHours { BranchId = branchId }).Entity;
        row.OpenFrom = from.ToString("HH:mm"); row.OpenTo = to.ToString("HH:mm"); row.UpdatedAtUtc = DateTime.UtcNow; row.UpdatedByName = Short(r.UpdatedByName, 200);
        await db.SaveChangesAsync(ct);
        return Ok(new { branchId, openFrom = row.OpenFrom, openTo = row.OpenTo, row.UpdatedAtUtc, row.UpdatedByName });
    }

    // ---------- helpers ----------

    /// <summary>The admin's support hours of the branch, else the branch's opening hours (from Catalog, via the Gateway).</summary>
    private async Task<SupportRules.Hours?> HoursAsync(int branchId, string? branchOpeningHours, CancellationToken ct)
    {
        var row = await db.SupportBranchHours.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (row is not null && TimeOnly.TryParseExact(row.OpenFrom, "HH:mm", out var f) && TimeOnly.TryParseExact(row.OpenTo, "HH:mm", out var t) && f != t)
            return new SupportRules.Hours(f, t);
        return SupportRules.ParseHours(branchOpeningHours);
    }

    private static TimeZoneInfo Zone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }

    private async Task<object> MapOneAsync(SupportConversation c, bool forStaff, CancellationToken ct) =>
        (await MapListAsync(new[] { c }, forStaff, ct))[0];

    private async Task<List<object>> MapListAsync(IReadOnlyList<SupportConversation> rows, bool forStaff, CancellationToken ct)
    {
        var ids = rows.Select(x => x.SupportConversationId).ToArray();
        var minRead = rows.Count == 0 ? 0 : rows.Min(x => Math.Min(x.CustomerLastReadMessageId, x.StaffLastReadMessageId));
        var unreadPool = ids.Length == 0 ? new List<SupportMessage>() : await db.SupportMessages.AsNoTracking()
            .Where(x => ids.Contains(x.SupportConversationId) && x.SupportMessageId > minRead).ToListAsync(ct);
        var lastIds = ids.Length == 0 ? new List<long>() : await db.SupportMessages.AsNoTracking()
            .Where(x => ids.Contains(x.SupportConversationId))
            .GroupBy(x => x.SupportConversationId).Select(g => g.Max(m => m.SupportMessageId)).ToListAsync(ct);
        var last = lastIds.Count == 0 ? new Dictionary<int, SupportMessage>() : (await db.SupportMessages.AsNoTracking()
                .Where(x => lastIds.Contains(x.SupportMessageId)).ToListAsync(ct))
            .ToDictionary(x => x.SupportConversationId);
        return rows.Select(c => (object)new
        {
            supportConversationId = c.SupportConversationId, c.CustomerId, customerName = c.CustomerNameSnapshot, c.BranchId,
            topic = c.Topic, topicLabel = SupportRules.FindTopic(c.Topic)?.Label ?? c.Topic,
            contextType = c.ContextType, contextId = c.ContextId, contextLabel = c.ContextLabel,
            status = c.Status, waitingSinceUtc = c.WaitingSinceUtc, lastMessageAtUtc = c.LastMessageAtUtc,
            unread = forStaff ? SupportRules.UnreadForStaff(c, unreadPool) : SupportRules.UnreadForCustomer(c, unreadPool),
            lastMessage = last.TryGetValue(c.SupportConversationId, out var m) ? MapMessage(m) : null,
            customerLastReadMessageId = c.CustomerLastReadMessageId, staffLastReadMessageId = c.StaffLastReadMessageId,
            resolvedAtUtc = c.ResolvedAtUtc, resolvedByName = c.ResolvedByName, createdAtUtc = c.CreatedAtUtc
        }).ToList();
    }

    private async Task<object> MapDetailAsync(SupportConversation c, long? beforeId, int take, bool forStaff, CancellationToken ct)
    {
        var query = db.SupportMessages.AsNoTracking().Where(x => x.SupportConversationId == c.SupportConversationId);
        if (beforeId is > 0) query = query.Where(x => x.SupportMessageId < beforeId);
        var page = await query.OrderByDescending(x => x.SupportMessageId).Take(take + 1).ToListAsync(ct);
        var hasOlder = page.Count > take;
        var messages = page.Take(take).OrderBy(x => x.SupportMessageId).Select(MapMessage).ToList();
        return new { conversation = await MapOneAsync(c, forStaff, ct), messages, hasOlder };
    }

    private static object MapMessage(SupportMessage m) => new
    {
        supportMessageId = m.SupportMessageId, senderType = m.SenderType, senderName = m.SenderName, body = m.Body, createdAtUtc = m.CreatedAtUtc
    };

    private static string? Short(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        return t.Length > max ? t[..max] : t;
    }

    private ObjectResult Bad(string message) => BadRequest(new { code = "validation_failed", message });
}
