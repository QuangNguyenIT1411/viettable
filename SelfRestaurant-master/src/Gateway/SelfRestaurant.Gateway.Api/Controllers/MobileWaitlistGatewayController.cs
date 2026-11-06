using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>
/// "Danh sách chờ" (owner 06/11): join when a time is full, see the place in the queue, take a held table before its
/// deadline (a real booking, with a deposit when the party needs one), or stop waiting.
/// </summary>
[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1/reservations/waitlist")]
public sealed class MobileWaitlistGatewayController(CustomersClient customers, CatalogClient catalog, IdentityClient identity,
    WaitlistCoordinator waitlist, ReservationDepositCoordinator deposits, CustomerInboxNotifier inbox,
    StaffBranchEvents staffEvents, IConfiguration configuration) : ControllerBase
{
    public sealed record JoinWaitlistMobileRequest(int BranchId, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int PartySize,
        string? Note = null, string? ContactName = null, string? ContactPhone = null);

    [HttpGet]
    public async Task<ActionResult<object>> Mine(CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try { return Ok(new { items = await WithBranchNames(await waitlist.MineAsync(actor.Value, ct), ct), offerMinutes = waitlist.OfferMinutes }); }
        catch (ApiClientException ex) { return Downstream(ex, "get_waitlist_failed"); }
    }

    [HttpPost]
    public async Task<ActionResult<object>> Join(JoinWaitlistMobileRequest request, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        if (request.BranchId <= 0) return Error("validation_failed", "Chi nhánh không hợp lệ.", 400);
        var openFrom = ReservationRules.ConfiguredTime(configuration, "Reservations:OpenFrom", ReservationRules.DefaultOpenFrom);
        var lastSlot = ReservationRules.ConfiguredTime(configuration, "Reservations:LastSlot", ReservationRules.DefaultLastSlot);
        if (WaitlistSlots.Candidates(request.WindowStart.UtcDateTime, request.WindowEnd.UtcDateTime, DateTimeOffset.UtcNow,
                openFrom, lastSlot, ReservationRules.RestaurantTimeZone).Count == 0)
            return Error("validation_failed",
                $"Khoảng giờ cần nằm trong giờ nhận đặt bàn ({openFrom:HH:mm}–{lastSlot:HH:mm}) và cách hiện tại ít nhất 15 phút.", 400);
        var phone = ReservationRules.NormalizePhone(request.ContactPhone);
        if (!string.IsNullOrWhiteSpace(request.ContactPhone) && phone is null)
            return Error("contact_phone_invalid", "Số điện thoại liên hệ không hợp lệ.", 400);
        var profile = await identity.GetMobileProfileAsync(ct);
        phone ??= ReservationRules.NormalizePhone(profile?.PhoneNumber);
        if (phone is null) return Error("contact_phone_required", "Vui lòng nhập số điện thoại liên hệ.", 422);
        var name = string.IsNullOrWhiteSpace(request.ContactName) ? profile?.Name : request.ContactName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Error("validation_failed", "Vui lòng nhập họ tên liên hệ.", 422);
        try
        {
            var entry = await customers.JoinWaitlistAsync(new
            {
                customerId = actor.Value, contactName = name, phoneNumber = phone, branchId = request.BranchId,
                windowStartUtc = request.WindowStart.UtcDateTime, windowEndUtc = request.WindowEnd.UtcDateTime,
                partySize = request.PartySize, note = request.Note
            }, ct);
            // A table may already be free inside the window: offer it at once.
            if (await waitlist.ProcessBranchQuietlyAsync(request.BranchId, ct) > 0) await NudgeCashier(request.BranchId);
            var id = entry.GetProperty("waitlistEntryId").GetInt32();
            var mine = await customers.GetMyWaitlistAsync(actor.Value, ct);
            var fresh = mine.EnumerateArray().FirstOrDefault(x => x.GetProperty("waitlistEntryId").GetInt32() == id);
            return Ok(fresh.ValueKind == JsonValueKind.Undefined ? entry : fresh);
        }
        catch (ApiClientException ex) { return Downstream(ex, "join_waitlist_failed"); }
    }

    /// <summary>Takes the held table: a real booking (deposit rules apply). Answers the booking.</summary>
    [HttpPost("{entryId:int}/accept")]
    public async Task<ActionResult<ReservationDto>> Accept(int entryId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var mine = await customers.GetMyWaitlistAsync(actor.Value, ct);
            var entry = mine.EnumerateArray().FirstOrDefault(x => x.GetProperty("waitlistEntryId").GetInt32() == entryId);
            if (entry.ValueKind == JsonValueKind.Undefined) return Error("waitlist_not_found", "Không tìm thấy lượt chờ.", 404);
            var party = entry.GetProperty("partySize").GetInt32();
            var deposit = deposits.RequiredAmount(party);
            var accepted = await customers.AcceptWaitlistAsync(entryId, new
            {
                customerId = actor.Value,
                initialStatus = deposit > 0 ? SelfRestaurant.Deposits.DepositRules.ReservationPendingDeposit : null,
                depositAmount = deposit > 0 ? deposit : (decimal?)null,
                depositDueAtUtc = deposit > 0 ? SelfRestaurant.Deposits.DepositRules.DueAt(DateTime.UtcNow, deposits.Options) : (DateTime?)null
            }, ct);
            var reservationId = accepted.GetProperty("reservationId").GetInt32();
            var reservation = await customers.GetReservationByIdAsync(reservationId, ct);
            if (reservation is null) return Error("reservation_not_found", "Không tìm thấy đặt bàn.", 404);
            try { await deposits.EnsureCreatedAsync(reservation, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { }
            await inbox.PushAsync(actor.Value, ct);
            await NudgeCashier(reservation.BranchId);
            return Ok(reservation);
        }
        catch (ApiClientException ex) { return Downstream(ex, "accept_waitlist_failed"); }
    }

    /// <summary>"Xếp lại hàng chờ" after an offer lapsed: a new place at the end of the queue, same window.</summary>
    [HttpPost("{entryId:int}/rejoin")]
    public async Task<ActionResult<object>> Rejoin(int entryId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var entry = await customers.RejoinWaitlistAsync(entryId, actor.Value, ct);
            var branchId = entry.GetProperty("branchId").GetInt32();
            if (await waitlist.ProcessBranchQuietlyAsync(branchId, ct) > 0) await NudgeCashier(branchId);
            var id = entry.GetProperty("waitlistEntryId").GetInt32();
            var mine = await customers.GetMyWaitlistAsync(actor.Value, ct);
            var fresh = mine.EnumerateArray().FirstOrDefault(x => x.GetProperty("waitlistEntryId").GetInt32() == id);
            return Ok(fresh.ValueKind == JsonValueKind.Undefined ? entry : fresh);
        }
        catch (ApiClientException ex) { return Downstream(ex, "rejoin_waitlist_failed"); }
    }

    [HttpPost("{entryId:int}/cancel")]
    public async Task<ActionResult<object>> Cancel(int entryId, CancellationToken ct)
    {
        var actor = CurrentCustomerId();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var result = await customers.CancelWaitlistAsync(entryId, actor.Value, ct);
            var branchId = result.GetProperty("branchId").GetInt32();
            // A released hold goes straight to the next guest.
            await waitlist.ProcessBranchQuietlyAsync(branchId, ct);
            await NudgeCashier(branchId);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "cancel_waitlist_failed"); }
    }

    private async Task<object> WithBranchNames(JsonElement entries, CancellationToken ct)
    {
        var names = (await catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>()).ToDictionary(x => x.BranchId, x => x.Name);
        if (entries.ValueKind != JsonValueKind.Array) return Array.Empty<object>();
        return entries.EnumerateArray().Select(x =>
        {
            var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x.GetRawText())!;
            var name = names.TryGetValue(x.GetProperty("branchId").GetInt32(), out var n) ? n : null;
            return (object)new Dictionary<string, object?>(row.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)))
            {
                ["branchName"] = name
            };
        }).ToArray();
    }

    private async Task NudgeCashier(int branchId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await staffEvents.PublishAsync(branchId, new[] { StaffBranchEvents.Reservations }, timeout.Token);
        }
        catch { /* the cashier panel also reloads on its own */ }
    }

    private int? CurrentCustomerId() =>
        int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;

    private ObjectResult Error(string code, string message, int status)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, null));
    }

    private ObjectResult Downstream(ApiClientException ex, string fallback) =>
        Error(string.IsNullOrWhiteSpace(ex.Code) ? fallback : ex.Code!, ex.Message, ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 502);
}
