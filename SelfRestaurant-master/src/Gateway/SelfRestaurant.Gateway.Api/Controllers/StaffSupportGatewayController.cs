using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>
/// Cashier "Hỗ trợ" tab (owner 06/11): the support conversations of the cashier's own branch, longest waiting first,
/// each with its attached item's live status. Replies notify the customer (inbox + "supportMessage" realtime).
/// </summary>
[ApiController]
[Route("api/cashier/support")]
public sealed class StaffSupportGatewayController(CustomersClient customers, SupportChatContext context, StaffBranchEvents staffEvents,
    CustomerInboxNotifier inbox, IHubContext<MobileNotificationsHub> mobileHub, ILogger<StaffSupportGatewayController> logger) : ControllerBase
{
    private static readonly string[] CashierRoles = ["CASHIER", "MANAGER", "ADMIN"];
    public const string CustomerEvent = "supportMessage";

    public sealed record ReplyRequest(string? Body);
    public sealed record ReadRequest(long LastMessageId);

    [HttpGet("conversations")]
    public async Task<ActionResult<object>> List([FromQuery] bool includeResolved, CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        try { return Ok(await customers.GetBranchSupportAsync(staff.Value.BranchId, includeResolved, ct)); }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    [HttpGet("conversations/{id:int}")]
    public async Task<ActionResult<object>> Get(int id, [FromQuery] long? beforeId, CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        try
        {
            var detail = await customers.GetSupportAsync(id, null, staff.Value.BranchId, beforeId, ct);
            if (detail.ValueKind == JsonValueKind.Undefined) return NotFound(new { code = "support_not_found", message = "Không tìm thấy cuộc trò chuyện." });
            var c = detail.GetProperty("conversation");
            var live = beforeId is > 0 ? null : await context.LiveAsync(SupportChatContext.Int(c, "customerId"),
                SupportChatContext.Str(c, "contextType") ?? "NONE", SupportChatContext.Int(c, "contextId") is var cid && cid > 0 ? cid : null, ct);
            return Ok(new
            {
                conversation = c, messages = detail.GetProperty("messages"), hasOlder = detail.GetProperty("hasOlder"),
                context = live is null ? null : new { type = live.Type, id = live.Id, label = live.Label, status = live.Status, actionLabel = live.ActionLabel, actionTab = live.ActionTab }
            });
        }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<ActionResult<object>> Reply(int id, ReplyRequest request, CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        var s = staff.Value;
        try
        {
            var result = await customers.SendSupportAsync(id, new { staffBranchId = s.BranchId, employeeId = s.EmployeeId, employeeName = s.Name, body = request.Body }, ct);
            var customerId = SupportChatContext.Int(result.GetProperty("conversation"), "customerId");
            await TellCustomer(customerId, id, ct);
            await NudgeBranch(s.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    [HttpPost("conversations/{id:int}/read")]
    public async Task<ActionResult<object>> Read(int id, ReadRequest request, CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        try
        {
            var result = await customers.ReadSupportAsync(id, new { staffBranchId = staff.Value.BranchId, lastMessageId = request.LastMessageId }, ct);
            // The customer's "đã xem" ticks update at once.
            await Push(SupportChatContext.Int(result, "customerId"), id, ct);
            await NudgeBranch(staff.Value.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    [HttpPost("conversations/{id:int}/resolve")]
    public async Task<ActionResult<object>> Resolve(int id, CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        var s = staff.Value;
        try
        {
            var result = await customers.ResolveSupportAsync(id, new { staffBranchId = s.BranchId, employeeId = s.EmployeeId, employeeName = s.Name }, ct);
            await Push(SupportChatContext.Int(result, "customerId"), id, ct);
            await NudgeBranch(s.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    [HttpGet("quick-replies")]
    public async Task<ActionResult<object>> QuickReplies(CancellationToken ct)
    {
        var staff = Staff();
        if (staff is null) return Unauthorized(new { code = "unauthorized", message = "Bạn cần đăng nhập bằng tài khoản thu ngân." });
        try { return Ok(await customers.GetSupportQuickRepliesAsync(staff.Value.BranchId, false, ct)); }
        catch (ApiClientException ex) { return Fail(ex); }
    }

    private async Task TellCustomer(int customerId, int conversationId, CancellationToken ct)
    {
        await inbox.PushAsync(customerId, ct); // the inbox row was written by Customers in the same step
        await Push(customerId, conversationId, ct);
    }

    private async Task Push(int customerId, int conversationId, CancellationToken ct)
    {
        if (customerId <= 0) return;
        try { await mobileHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId)).SendAsync(CustomerEvent, new { conversationId }, ct); }
        catch (Exception ex) { logger.LogWarning("Support realtime push skipped ({ErrorType}).", ex.GetType().Name); }
    }

    private async Task NudgeBranch(int branchId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await staffEvents.PublishAsync(branchId, new[] { MobileSupportChatGatewayController.StaffTopic }, timeout.Token);
        }
        catch (Exception) { }
    }

    private (int EmployeeId, int BranchId, string Name)? Staff()
    {
        var session = HttpContext.StaffIdentity();
        var employeeId = session.GetInt32(SessionKeys.EmployeeId);
        var branchId = session.GetInt32(SessionKeys.EmployeeBranchId);
        var role = session.GetString(SessionKeys.EmployeeRoleCode);
        if (employeeId is not > 0 || branchId is not > 0 || string.IsNullOrWhiteSpace(role)
            || !CashierRoles.Contains(role, StringComparer.OrdinalIgnoreCase)) return null;
        return (employeeId.Value, branchId.Value, session.GetString(SessionKeys.EmployeeName) ?? "Nhân viên");
    }

    private ObjectResult Fail(ApiClientException ex) =>
        StatusCode(ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 502, new { code = ex.Code ?? "support_failed", message = ex.Message });
}
