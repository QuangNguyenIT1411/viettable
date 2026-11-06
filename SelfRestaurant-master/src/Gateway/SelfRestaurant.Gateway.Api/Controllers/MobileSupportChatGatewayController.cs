using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>
/// "Chat với nhân viên hỗ trợ" for the app (owner 06/11). The customer is the token's subject; every item they attach
/// is checked against their own data. A new customer message nudges the branch's cashier screen ("support").
/// </summary>
[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1/support/chat")]
public sealed class MobileSupportChatGatewayController(CustomersClient customers, CatalogClient catalog, IdentityClient identity,
    SupportChatContext context, StaffBranchEvents staffEvents) : ControllerBase
{
    public const string StaffTopic = "support";

    public sealed record StartRequest(int BranchId, string? Topic, int? ContextId, string? Message);
    public sealed record MessageRequest(string? Body);
    public sealed record ReadRequest(long LastMessageId);

    /// <summary>The topic cards, in order, and what each asks the customer to pick.</summary>
    [HttpGet("topics")]
    public ActionResult<object> Topics() => Ok(new
    {
        items = SupportTopics.All.Select(t => new { code = t.Code, label = t.Label, contextType = t.ContextType, checksPayment = t.ChecksPayment })
    });

    /// <summary>The customer's own items for a topic (bookings, deposits, transfers, invoices).</summary>
    [HttpGet("options")]
    public async Task<ActionResult<object>> Options([FromQuery] string? topic, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var t = SupportTopics.Find(topic);
        if (t is null) return Error("validation_failed", "Chủ đề không hợp lệ.", 400);
        if (t.ContextType is null) return Ok(new { items = Array.Empty<object>() });
        try
        {
            var options = await context.OptionsAsync(actor.Value, t.ContextType, ct);
            return Ok(new { items = options.Select(o => new { contextType = o.ContextType, contextId = o.ContextId, branchId = o.BranchId, label = o.Label, detail = o.Detail }) });
        }
        catch (ApiClientException ex) { return Downstream(ex, "support_options_failed"); }
    }

    /// <summary>Topics 1 and 2: is this deposit / transfer already confirmed? Answered from the server's real status.</summary>
    [HttpGet("check")]
    public async Task<ActionResult<object>> Check([FromQuery] string? topic, [FromQuery] int contextId, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var t = SupportTopics.Find(topic);
        if (t is null || !t.ChecksPayment || contextId <= 0) return Error("validation_failed", "Chủ đề không hợp lệ.", 400);
        try
        {
            var check = await context.CheckAsync(actor.Value, t.Code, contextId, ct);
            if (check is null) return Error("support_context_not_found", "Không tìm thấy khoản này trong tài khoản của bạn.", 404);
            return Ok(new
            {
                confirmed = check.Confirmed, confirmedAtUtc = check.ConfirmedAtUtc, amount = check.Amount, transferContent = check.TransferContent,
                status = check.Status, statusText = check.StatusText, branchId = check.BranchId
            });
        }
        catch (ApiClientException ex) { return Downstream(ex, "support_check_failed"); }
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try { return Ok(await customers.GetMySupportAsync(actor.Value, ct)); }
        catch (ApiClientException ex) { return Downstream(ex, "support_list_failed"); }
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<object>> Start(StartRequest request, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var t = SupportTopics.Find(request.Topic);
        if (t is null) return Error("validation_failed", "Chủ đề không hợp lệ.", 400);
        try
        {
            var branches = await catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>();
            var branch = branches.FirstOrDefault(b => b.BranchId == request.BranchId);
            if (branch is null) return Error("branch_not_found", "Không tìm thấy chi nhánh.", 404);
            string? contextType = null, label = null;
            int? contextId = null;
            if (t.ContextType is not null && request.ContextId is > 0)
            {
                // Only an item of the customer's own data can be attached (never trust the id alone).
                var option = (await context.OptionsAsync(actor.Value, t.ContextType, ct)).FirstOrDefault(o => o.ContextId == request.ContextId);
                if (option is null) return Error("support_context_not_found", "Không tìm thấy mục này trong tài khoản của bạn.", 404);
                contextType = option.ContextType; contextId = option.ContextId; label = option.Label;
            }
            var profile = await identity.GetMobileProfileAsync(ct);
            var result = await customers.StartSupportAsync(new
            {
                customerId = actor.Value, customerName = profile?.Name, branchId = branch.BranchId, topic = t.Code,
                contextType = contextType ?? "NONE", contextId, contextLabel = label, message = request.Message,
                branchOpeningHours = branch.OpeningHours
            }, ct);
            await NudgeStaff(branch.BranchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "support_start_failed"); }
    }

    [HttpGet("conversations/{id:int}")]
    public async Task<ActionResult<object>> Get(int id, [FromQuery] long? beforeId, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var result = await customers.GetSupportAsync(id, actor.Value, null, beforeId, ct);
            return result.ValueKind == JsonValueKind.Undefined ? Error("support_not_found", "Không tìm thấy cuộc trò chuyện.", 404) : Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "support_get_failed"); }
    }

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<ActionResult<object>> Send(int id, MessageRequest request, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var current = await customers.GetSupportAsync(id, actor.Value, null, null, ct);
            if (current.ValueKind == JsonValueKind.Undefined) return Error("support_not_found", "Không tìm thấy cuộc trò chuyện.", 404);
            var branchId = SupportChatContext.Int(current.GetProperty("conversation"), "branchId");
            var branch = (await catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>()).FirstOrDefault(b => b.BranchId == branchId);
            var result = await customers.SendSupportAsync(id, new { customerId = actor.Value, body = request.Body, branchOpeningHours = branch?.OpeningHours }, ct);
            await NudgeStaff(branchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "support_send_failed"); }
    }

    [HttpPost("conversations/{id:int}/read")]
    public async Task<ActionResult<object>> Read(int id, ReadRequest request, CancellationToken ct)
    {
        var actor = CustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try { return Ok(await customers.ReadSupportAsync(id, new { customerId = actor.Value, lastMessageId = request.LastMessageId }, ct)); }
        catch (ApiClientException ex) { return Downstream(ex, "support_read_failed"); }
    }

    private async Task NudgeStaff(int branchId)
    {
        if (branchId <= 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await staffEvents.PublishAsync(branchId, new[] { StaffTopic }, timeout.Token);
        }
        catch (Exception) { /* best effort: the cashier screen also polls */ }
    }

    private int? CustomerId() =>
        int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;

    private ObjectResult Error(string code, string message, int status)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, null));
    }

    private ObjectResult Downstream(ApiClientException ex, string fallback) =>
        Error(string.IsNullOrWhiteSpace(ex.Code) ? fallback : ex.Code!, ex.Message, ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 502);
}

/// <summary>The same topic list the Customers service validates (kept here for the app's cards and context picker).</summary>
public static class SupportTopics
{
    public sealed record Topic(string Code, string Label, string? ContextType, bool ChecksPayment);

    public static readonly IReadOnlyList<Topic> All = new[]
    {
        new Topic("DEPOSIT_TRANSFER", "Đã chuyển khoản đặt cọc nhưng chưa được xác nhận", "DEPOSIT", true),
        new Topic("BILL_TRANSFER", "Đã chuyển khoản thanh toán nhưng chưa được xác nhận", "PAYMENT", true),
        new Topic("BOOKING_CHANGE", "Đổi / huỷ đặt bàn", "RESERVATION", false),
        new Topic("DEPOSIT_REFUND", "Hoàn tiền cọc", "DEPOSIT", false),
        new Topic("BILL_WRONG", "Hoá đơn sai món / sai tiền", "INVOICE", false),
        new Topic("LOYALTY_PROMO", "Điểm thưởng / voucher / khuyến mãi không áp được", "INVOICE", false),
        new Topic("OTHER", "Khác", null, false),
    };

    public static Topic? Find(string? code) => All.FirstOrDefault(t => string.Equals(t.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
}
