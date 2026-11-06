using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Controllers;

/// <summary>
/// Waitlist (owner 06/11). There is no background job on Vercel: expired offers and windows that are over are closed
/// whenever the list is read (the guest's screen, its 15-minute poll, the cashier, or the Gateway after a booking
/// frees a table). The Gateway decides which table to offer (it knows the tables); this service keeps the state.
/// </summary>
[ApiController]
public sealed class WaitlistController : ControllerBase
{
    private readonly CustomersDbContext _db;

    public WaitlistController(CustomersDbContext db) => _db = db;

    public sealed record JoinWaitlistRequest(int CustomerId, string? ContactName, string? PhoneNumber, int BranchId,
        DateTime WindowStartUtc, DateTime WindowEndUtc, int PartySize, string? Note);

    public sealed record CustomerRequest(int CustomerId);

    public sealed record OfferRequest(int TableId, DateTime ReservedAtUtc, int HoldMinutes, IReadOnlyList<int>? AdditionalTableIds = null);

    public sealed record AcceptRequest(int CustomerId, string? InitialStatus = null, decimal? DepositAmount = null, DateTime? DepositDueAtUtc = null);

    [HttpPost("api/customers/waitlist")]
    public async Task<ActionResult<object>> Join([FromBody] JoinWaitlistRequest? request, CancellationToken ct)
    {
        if (request is null || request.CustomerId <= 0 || request.BranchId <= 0)
            return BadRequest(new { code = "validation_failed", message = "Thông tin chờ bàn không hợp lệ." });
        var name = request.ContactName?.Trim();
        var phone = request.PhoneNumber?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone))
            return BadRequest(new { code = "validation_failed", message = "Cần họ tên và số điện thoại liên hệ." });
        var now = DateTime.UtcNow;
        await CloseStaleAsync(x => x.CustomerId == request.CustomerId, now, ct);
        var active = await _db.WaitlistEntries.CountAsync(x => x.CustomerId == request.CustomerId
            && (x.Status == WaitlistRules.Waiting || x.Status == WaitlistRules.Offered), ct);
        var start = Utc(request.WindowStartUtc);
        var end = Utc(request.WindowEndUtc);
        var refusal = WaitlistRules.WhyNotJoinable(start, end, request.PartySize, active, now);
        if (refusal is not null) return Conflict(new { code = "waitlist_not_allowed", message = refusal });
        var entry = new WaitlistEntry
        {
            CustomerId = request.CustomerId, ContactName = name.Length > 200 ? name[..200] : name,
            PhoneNumber = phone.Length > 30 ? phone[..30] : phone, BranchId = request.BranchId,
            WindowStartUtc = start, WindowEndUtc = end, PartySize = request.PartySize,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(500, request.Note.Trim().Length)],
            Status = WaitlistRules.Waiting, CreatedAtUtc = now
        };
        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return Ok(await MapAsync(entry, ct));
    }

    [HttpGet("api/customers/{customerId:int}/waitlist")]
    public async Task<ActionResult<IReadOnlyList<object>>> Mine(int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return BadRequest(new { code = "validation_failed", message = "Khách hàng không hợp lệ." });
        await CloseStaleAsync(x => x.CustomerId == customerId, DateTime.UtcNow, ct);
        var since = DateTime.UtcNow.AddDays(-7);
        var entries = await _db.WaitlistEntries.AsNoTracking()
            .Where(x => x.CustomerId == customerId && (x.Status == WaitlistRules.Waiting || x.Status == WaitlistRules.Offered || x.CreatedAtUtc >= since))
            .OrderByDescending(x => x.CreatedAtUtc).Take(30).ToListAsync(ct);
        return Ok(await MapManyAsync(entries, ct));
    }

    /// <summary>The cashier's view of one branch: everyone still waiting or holding a table, plus today's finished ones.</summary>
    [HttpGet("api/internal/waitlist")]
    public async Task<ActionResult<IReadOnlyList<object>>> ForBranch([FromQuery] int branchId, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest(new { code = "validation_failed", message = "Chi nhánh không hợp lệ." });
        await CloseStaleAsync(x => x.BranchId == branchId, DateTime.UtcNow, ct);
        var since = DateTime.UtcNow.AddHours(-12);
        var entries = await _db.WaitlistEntries.AsNoTracking()
            .Where(x => x.BranchId == branchId && (x.Status == WaitlistRules.Waiting || x.Status == WaitlistRules.Offered
                || (x.UpdatedAtUtc ?? x.CreatedAtUtc) >= since))
            .OrderBy(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        return Ok(await MapManyAsync(entries, ct));
    }

    /// <summary>Guests to offer a table to, in sign-up order (after closing stale entries of the branch).</summary>
    [HttpGet("api/internal/waitlist/queue")]
    public async Task<ActionResult<IReadOnlyList<object>>> Queue([FromQuery] int branchId, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest(new { code = "validation_failed", message = "Chi nhánh không hợp lệ." });
        var now = DateTime.UtcNow;
        await CloseStaleAsync(x => x.BranchId == branchId, now, ct);
        var waiting = await _db.WaitlistEntries.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.Status == WaitlistRules.Waiting).ToListAsync(ct);
        return Ok(WaitlistRules.Queue(waiting, now).Select(x => new
        {
            waitlistEntryId = x.WaitlistEntryId, branchId = x.BranchId, partySize = x.PartySize,
            windowStartUtc = x.WindowStartUtc, windowEndUtc = x.WindowEndUtc, createdAtUtc = x.CreatedAtUtc
        }).ToArray());
    }

    /// <summary>
    /// Holds a table for a waiting guest (the Gateway picked it). One winner: the entry must still be Waiting (version
    /// check) and the table must still be free under the same lock bookings use. 409 otherwise.
    /// </summary>
    [HttpPost("api/internal/waitlist/{entryId:int}/offer")]
    public async Task<ActionResult<object>> Offer(int entryId, [FromBody] OfferRequest? request, CancellationToken ct)
    {
        if (request is null || request.TableId <= 0)
            return BadRequest(new { code = "validation_failed", message = "Bàn không hợp lệ." });
        var holdMinutes = Math.Clamp(request.HoldMinutes, 1, 60);
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(x => x.WaitlistEntryId == entryId, ct);
        if (entry is null) return NotFound(new { code = "waitlist_not_found", message = "Không tìm thấy lượt chờ." });
        if (entry.Status != WaitlistRules.Waiting)
            return Conflict(new { code = "waitlist_state_conflict", message = "Lượt chờ không còn ở trạng thái chờ." });
        var reservedAt = Utc(request.ReservedAtUtc);
        var now = DateTime.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var group = ReservationOccupancy.Group(request.TableId, request.AdditionalTableIds);
        if (_db.Database.IsRelational())
            foreach (var groupTableId in group.OrderBy(x => x))
                await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(_db, $"reservation-table:{entry.BranchId}:{groupTableId}", ct);
        if (await ReservationOccupancy.AnyTakenAsync(_db, entry.BranchId, group, reservedAt, null, now, ct))
            return Conflict(new { code = "reservation_slot_unavailable", message = "Bàn đã được giữ trong khung giờ này." });
        var hold = new Reservation
        {
            ReservationCode = $"WL{entry.WaitlistEntryId}-{now:HHmmss}",
            CustomerId = entry.CustomerId, CustomerName = entry.ContactName, PhoneNumber = entry.PhoneNumber,
            BranchId = entry.BranchId, TableId = request.TableId, PartySize = entry.PartySize, ReservedAt = reservedAt,
            ArrivalWindowMinutes = 30, Status = WaitlistRules.HoldStatus, Note = entry.Note, CreatedAtUtc = now,
            HoldExpiresAtUtc = now.AddMinutes(holdMinutes)
        };
        if (group.Count > 1) ReservationOccupancy.SetGroup(hold, group, now);
        _db.Reservations.Add(hold);
        try
        {
            await _db.SaveChangesAsync(ct);
            entry.Status = WaitlistRules.Offered;
            entry.OfferedReservationId = hold.ReservationId;
            entry.OfferedTableId = request.TableId;
            entry.OfferedReservedAtUtc = reservedAt;
            entry.OfferedAtUtc = now;
            entry.OfferExpiresAtUtc = hold.HoldExpiresAtUtc;
            entry.UpdatedAtUtc = now;
            entry.Version++;
            await CustomerInbox.AddAsync(_db, entry.CustomerId, CustomerInbox.WaitlistOffered, entry.WaitlistEntryId,
                $"Có bàn cho {entry.PartySize} khách lúc {CustomerInbox.When(reservedAt)}. Xác nhận trước {CustomerInbox.When(hold.HoldExpiresAtUtc!.Value)[..5]} để giữ bàn.",
                0, ct);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { code = "waitlist_state_conflict", message = "Lượt chờ vừa được xử lý ở nơi khác." });
        }
        catch (Exception ex) when (_db.Database.IsNpgsql() && (ex is Npgsql.PostgresException { SqlState: "40001" }
            || ex.InnerException is Npgsql.PostgresException { SqlState: "40001" }))
        {
            return Conflict(new { code = "command_in_progress", message = "Bàn đang được cập nhật." });
        }
        return Ok(await MapAsync(entry, ct));
    }

    /// <summary>
    /// The guest takes the held table: the hold becomes a real booking (Pending, or PendingDeposit when the Gateway
    /// asks for a deposit). Accepting twice returns the same booking; a second guest can never accept this entry.
    /// </summary>
    [HttpPost("api/customers/waitlist/{entryId:int}/accept")]
    public async Task<ActionResult<object>> Accept(int entryId, [FromBody] AcceptRequest? request, CancellationToken ct)
    {
        if (request is null || request.CustomerId <= 0) return BadRequest(new { code = "validation_failed", message = "Thiếu khách hàng." });
        var now = DateTime.UtcNow;
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(x => x.WaitlistEntryId == entryId && x.CustomerId == request.CustomerId, ct);
        if (entry is null) return NotFound(new { code = "waitlist_not_found", message = "Không tìm thấy lượt chờ." });
        if (entry.Status == WaitlistRules.Accepted && entry.OfferedReservationId is > 0)
            return Ok(new { entry = await MapAsync(entry, ct), reservationId = entry.OfferedReservationId });
        var refusal = WaitlistRules.WhyNotAcceptable(entry, now);
        if (refusal is not null) return Conflict(new { code = "waitlist_offer_unavailable", message = refusal });
        var hold = await _db.Reservations.FirstOrDefaultAsync(x => x.ReservationId == entry.OfferedReservationId, ct);
        if (hold is null || hold.Status != WaitlistRules.HoldStatus)
            return Conflict(new { code = "waitlist_offer_unavailable", message = "Bàn giữ cho bạn không còn." });
        var deposit = string.Equals(request.InitialStatus, CustomersController.PendingDeposit, StringComparison.OrdinalIgnoreCase)
            && request.DepositAmount is > 0 && request.DepositDueAtUtc is not null;
        hold.Status = deposit ? CustomersController.PendingDeposit : "Pending";
        hold.DepositAmount = deposit ? request.DepositAmount : null;
        hold.DepositDueAtUtc = deposit ? Utc(request.DepositDueAtUtc!.Value) : null;
        hold.HoldExpiresAtUtc = null;
        hold.ReservationCode = await NextCodeAsync(hold.ReservedAt, ct);
        hold.UpdatedAtUtc = now;
        entry.Status = WaitlistRules.Accepted;
        entry.AcceptedAtUtc = now;
        entry.UpdatedAtUtc = now;
        entry.Version++;
        await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationCreated, hold, ct);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { code = "waitlist_state_conflict", message = "Lượt chờ vừa được xử lý ở nơi khác. Vui lòng tải lại." });
        }
        return Ok(new { entry = await MapAsync(entry, ct), reservationId = hold.ReservationId });
    }

    /// <summary>The guest stops waiting; a held table is released at once for the next guest.</summary>
    [HttpPost("api/customers/waitlist/{entryId:int}/cancel")]
    public async Task<ActionResult<object>> Cancel(int entryId, [FromBody] CustomerRequest? request, CancellationToken ct)
    {
        if (request is null || request.CustomerId <= 0) return BadRequest(new { code = "validation_failed", message = "Thiếu khách hàng." });
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(x => x.WaitlistEntryId == entryId && x.CustomerId == request.CustomerId, ct);
        if (entry is null) return NotFound(new { code = "waitlist_not_found", message = "Không tìm thấy lượt chờ." });
        if (!WaitlistRules.IsActive(entry.Status)) return Ok(await MapAsync(entry, ct)); // already over: nothing to do
        var now = DateTime.UtcNow;
        if (entry.Status == WaitlistRules.Offered) await ReleaseHoldAsync(entry, "Khách bỏ lượt giữ bàn", now, ct);
        entry.Status = WaitlistRules.Cancelled;
        entry.ClosedAtUtc = now;
        entry.ClosedReason = "Khách huỷ chờ";
        entry.UpdatedAtUtc = now;
        entry.Version++;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { code = "waitlist_state_conflict", message = "Lượt chờ vừa được xử lý ở nơi khác. Vui lòng tải lại." });
        }
        return Ok(await MapAsync(entry, ct));
    }

    /// <summary>Offers past their time are expired (the held table is freed); windows that are over are closed.</summary>
    private async Task CloseStaleAsync(System.Linq.Expressions.Expression<Func<WaitlistEntry, bool>> scope, DateTime now, CancellationToken ct)
    {
        var limit = now.AddMinutes(15);
        var stale = await _db.WaitlistEntries.Where(scope)
            .Where(x => (x.Status == WaitlistRules.Offered && x.OfferExpiresAtUtc <= now)
                || (x.Status == WaitlistRules.Waiting && x.WindowEndUtc <= limit))
            .ToListAsync(ct);
        if (stale.Count == 0) return;
        foreach (var entry in stale)
        {
            if (WaitlistRules.OfferExpired(entry, now))
            {
                await ReleaseHoldAsync(entry, "Hết thời gian giữ bàn cho danh sách chờ", now, ct);
                entry.Status = WaitlistRules.Expired;
                entry.ClosedReason = "Hết thời gian xác nhận";
                await CustomerInbox.AddAsync(_db, entry.CustomerId, CustomerInbox.WaitlistExpired, entry.WaitlistEntryId,
                    "Bạn chưa xác nhận kịp nên bàn đã được mời cho khách kế tiếp. Bạn có thể đăng ký chờ lại.", 0, ct);
            }
            else
            {
                entry.Status = WaitlistRules.Closed;
                entry.ClosedReason = "Đã hết khoảng giờ chờ";
            }
            entry.ClosedAtUtc = now;
            entry.UpdatedAtUtc = now;
            entry.Version++;
        }
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* another read closed them first; same outcome */ }
    }

    private async Task ReleaseHoldAsync(WaitlistEntry entry, string reason, DateTime now, CancellationToken ct)
    {
        if (entry.OfferedReservationId is not > 0) return;
        var hold = await _db.Reservations.FirstOrDefaultAsync(x => x.ReservationId == entry.OfferedReservationId, ct);
        if (hold is null || hold.Status != WaitlistRules.HoldStatus) return;
        hold.Status = "Cancelled";
        hold.CancelledAtUtc = now;
        hold.StatusReason = reason;
        hold.UpdatedAtUtc = now;
    }

    private async Task<string> NextCodeAsync(DateTime reservedAt, CancellationToken ct)
    {
        // Same shape as other bookings (RSV-yyyyMMdd-####); unique index guards against a clash.
        var prefix = $"RSV-{reservedAt:yyyyMMdd}-";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = prefix + Random.Shared.Next(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!await _db.Reservations.AnyAsync(x => x.ReservationCode == code, ct)) return code;
        }
        return prefix + Guid.NewGuid().ToString("N")[..8];
    }

    private async Task<object> MapAsync(WaitlistEntry entry, CancellationToken ct) => (await MapManyAsync(new[] { entry }, ct))[0];

    private async Task<IReadOnlyList<object>> MapManyAsync(IReadOnlyList<WaitlistEntry> entries, CancellationToken ct)
    {
        var branches = entries.Where(x => x.Status == WaitlistRules.Waiting).Select(x => x.BranchId).Distinct().ToArray();
        var waiting = branches.Length == 0 ? new List<WaitlistEntry>() : await _db.WaitlistEntries.AsNoTracking()
            .Where(x => branches.Contains(x.BranchId) && x.Status == WaitlistRules.Waiting).ToListAsync(ct);
        return entries.Select(x => (object)new
        {
            waitlistEntryId = x.WaitlistEntryId, customerId = x.CustomerId, contactName = x.ContactName, phoneNumber = x.PhoneNumber,
            branchId = x.BranchId, windowStartUtc = x.WindowStartUtc, windowEndUtc = x.WindowEndUtc, partySize = x.PartySize,
            note = x.Note, status = x.Status, createdAtUtc = x.CreatedAtUtc,
            position = WaitlistRules.Position(x, waiting, CustomerInbox.RestaurantTimeZone),
            offeredReservationId = x.OfferedReservationId, offeredTableId = x.OfferedTableId,
            offeredReservedAtUtc = x.OfferedReservedAtUtc, offeredAtUtc = x.OfferedAtUtc, offerExpiresAtUtc = x.OfferExpiresAtUtc,
            acceptedAtUtc = x.AcceptedAtUtc, closedAtUtc = x.ClosedAtUtc, closedReason = x.ClosedReason
        }).ToArray();
    }

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
