using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Text.RegularExpressions;

namespace SelfRestaurant.Customers.Api.Controllers;

[ApiController]
public sealed class CustomersController : ControllerBase
{
    private static readonly HashSet<string> ClosedReservationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cancelled",
        "NoShow",
        "CheckedIn",
        "Completed",
        "Rejected"
    };
    /// <summary>Written on a booking the system confirmed because the guest checked in while it was still pending.</summary>
    public const string AutoConfirmedAtCheckIn = "Tự xác nhận khi check-in";
    private static readonly Regex ReservationPhoneNumberRegex = new(@"^\d{10}$", RegexOptions.Compiled);

    private readonly CustomersDbContext _db;
    private readonly OrdersQueryClient _ordersQueryClient;
    private readonly IHostEnvironment _environment;

    public CustomersController(CustomersDbContext db, OrdersQueryClient ordersQueryClient, IHostEnvironment environment)
    {
        _db = db;
        _ordersQueryClient = ordersQueryClient;
        _environment = environment;
    }

    [HttpPost("api/customers/reservations")]
    public async Task<ActionResult<object>> CreateReservation(
        [FromBody] CreateReservationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Dữ liệu đặt bàn không hợp lệ." });
        }

        var customerName = NormalizeRequired(request.CustomerName);
        var phoneNumber = NormalizeRequired(request.PhoneNumber);
        var note = NormalizeOptional(request.Note, 1000);
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey, 100);

        if (customerName is null)
        {
            return BadRequest(new { message = "Tên khách hàng là bắt buộc." });
        }
        if (phoneNumber is null)
        {
            return BadRequest(new { message = "Số điện thoại là bắt buộc." });
        }
        if (!ReservationPhoneNumberRegex.IsMatch(phoneNumber))
        {
            return BadRequest(new { message = "Số điện thoại phải gồm đúng 10 chữ số." });
        }
        if (request.BranchId <= 0)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }
        if (request.PartySize is < 1 or > 30)
        {
            return BadRequest(new { message = "Số khách phải từ 1 đến 30 người." });
        }
        if (request.ReservedAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-5))
        {
            return BadRequest(new { message = "Thời gian đặt bàn không được ở quá khứ." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (idempotencyKey is not null)
        {
            var existing = await _db.Reservations
                .AsNoTracking()
                .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, request.RequestHash, StringComparison.Ordinal))
                    return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
                return Ok(MapReservation(existing));
            }
        }

        if (request.TableId is > 0 && await _db.Reservations.AnyAsync(x =>
                x.BranchId == request.BranchId && x.TableId == request.TableId
                && x.ReservedAt < request.ReservedAt.AddMinutes(120)
                && x.ReservedAt.AddMinutes(120) > request.ReservedAt
                && !ClosedReservationStatuses.Contains(x.Status), cancellationToken))
            return Conflict(new { code = "reservation_slot_unavailable", message = "Bàn đã được giữ trong khung giờ này." });

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            ReservationCode = await GenerateReservationCodeAsync(request.ReservedAt, cancellationToken),
            CustomerId = request.CustomerId is > 0 ? request.CustomerId : null,
            CustomerName = customerName,
            PhoneNumber = phoneNumber,
            BranchId = request.BranchId,
            TableId = request.TableId,
            PartySize = request.PartySize,
            ReservedAt = request.ReservedAt,
            ArrivalWindowMinutes = 30,
            Status = string.Equals(request.InitialStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ? "Confirmed" : "Pending",
            Note = note,
            CreatedAtUtc = now,
            IdempotencyKey = idempotencyKey,
            RequestHash = NormalizeOptional(request.RequestHash, 64)
        };

        _db.Reservations.Add(reservation);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            // Needs the generated id; same transaction, so the booking and its notification commit together.
            await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationCreated, reservation, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (_db.Database.IsNpgsql()
            && (exception is Npgsql.PostgresException { SqlState: "40001" }
                || exception.InnerException is Npgsql.PostgresException { SqlState: "40001" }))
        {
            // PostgreSQL may detect the competing reservation at commit, not at INSERT.
            // Keep SERIALIZABLE: the aborted command must not be reported as a success.
            // Disposal rolls back an INSERT failure. A failed COMMIT has already
            // completed the Npgsql transaction, so a second Rollback would throw.
            return Conflict(new { code = "command_in_progress", message = "Bàn đang được cập nhật. Vui lòng kiểm tra lại tình trạng bàn và thử lại." });
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.Entry(reservation).State = EntityState.Detached;
            var existing = await _db.Reservations
                .AsNoTracking()
                .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, request.RequestHash, StringComparison.Ordinal))
                    return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
                return Ok(MapReservation(existing));
            }

            throw;
        }

        return CreatedAtAction(nameof(GetReservationByCode), new { reservationCode = reservation.ReservationCode }, MapReservation(reservation));
    }

    [HttpGet("api/customers/reservations/{reservationCode}")]
    public async Task<ActionResult<object>> GetReservationByCode(
        string reservationCode,
        CancellationToken cancellationToken)
    {
        var code = NormalizeOptional(reservationCode, 40);
        if (code is null)
        {
            return BadRequest(new { message = "Mã đặt bàn không hợp lệ." });
        }

        var reservation = await _db.Reservations
            .AsNoTracking()
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationCode == code, cancellationToken);

        return reservation is null ? NotFound(new { message = "Không tìm thấy đặt bàn." }) : Ok(MapReservation(reservation));
    }

    [HttpGet("api/customers/reservations/by-id/{reservationId:int}")]
    public async Task<ActionResult<object>> GetReservationById(
        int reservationId,
        CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return BadRequest(new { message = "Đặt bàn không hợp lệ." });
        }

        await MarkNoShowsAsync(null, null, reservationId, cancellationToken);
        var reservation = await _db.Reservations
            .AsNoTracking()
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);

        return reservation is null ? NotFound(new { message = "Không tìm thấy đặt bàn." }) : Ok(MapReservation(reservation));
    }

    [HttpGet("api/internal/reservations/by-idempotency/{key}")]
    public async Task<ActionResult<object>> GetReservationByIdempotencyKey(string key, CancellationToken cancellationToken)
    {
        var normalized = NormalizeOptional(key, 100);
        if (normalized is null) return BadRequest(new { code = "validation_failed", message = "Idempotency key không hợp lệ." });
        var reservation = await _db.Reservations.AsNoTracking().Include(x => x.PreOrderItems).Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == normalized, cancellationToken);
        return reservation is null ? NotFound() : Ok(MapReservation(reservation));
    }

    [HttpGet("api/customers/reservations/today")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetTodayReservations(
        [FromQuery] int? branchId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var restaurantTimeZone = GetRestaurantTimeZone();
        var restaurantDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, restaurantTimeZone));
        var localStart = restaurantDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = restaurantDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, restaurantTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, restaurantTimeZone);
        var normalizedStatus = NormalizeOptional(status, 30);
        await MarkNoShowsAsync(null, branchId, null, cancellationToken);

        var query = _db.Reservations
            .AsNoTracking()
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .Where(x => x.ReservedAt >= utcStart && x.ReservedAt < utcEnd);

        if (branchId is > 0)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        if (normalizedStatus is not null)
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var reservations = await query
            .OrderBy(x => x.ReservedAt)
            .ThenBy(x => x.ReservationId)
            .ToListAsync(cancellationToken);

        return Ok(reservations.Select(MapReservation).ToArray());
    }

    [HttpGet("api/customers/{customerId:int}/reservations")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetCustomerReservations(
        int customerId,
        CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            return BadRequest(new { message = "Khách hàng không hợp lệ." });
        }

        await MarkNoShowsAsync(customerId, null, null, cancellationToken);
        var reservations = await _db.Reservations
            .AsNoTracking()
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.ReservedAt)
            .ThenByDescending(x => x.ReservationId)
            .ToListAsync(cancellationToken);

        return Ok(reservations.Select(MapReservation).ToArray());
    }

    [HttpGet("api/internal/reservations/occupied-tables")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetOccupiedTableIds(
        [FromQuery] int branchId, [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc,
        [FromQuery] int? excludeReservationId, CancellationToken cancellationToken)
    {
        if (branchId <= 0 || fromUtc >= toUtc) return BadRequest(new { code = "validation_failed", message = "Khoảng thời gian không hợp lệ." });
        var query = _db.Reservations.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.TableId != null
                && x.ReservedAt < toUtc && x.ReservedAt.AddMinutes(120) > fromUtc
                && !ClosedReservationStatuses.Contains(x.Status));
        if (excludeReservationId is > 0) query = query.Where(x => x.ReservationId != excludeReservationId.Value);
        return Ok(await query.Select(x => x.TableId!.Value).Distinct().ToListAsync(cancellationToken));
    }

    /// <summary>
    /// The earliest open reservation (Pending/Confirmed/CheckingIn) of one table starting in [fromUtc, toUtc], or 404.
    /// A walk-in must not take a table that a booked guest is about to use.
    /// </summary>
    [HttpGet("api/internal/reservations/table-hold")]
    public async Task<IActionResult> GetTableHold([FromQuery] int branchId, [FromQuery] int tableId,
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken cancellationToken)
    {
        if (branchId <= 0 || tableId <= 0 || fromUtc >= toUtc)
            return BadRequest(new { code = "validation_failed", message = "Khoảng thời gian không hợp lệ." });
        var hold = await _db.Reservations.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.TableId == tableId && x.ReservedAt >= fromUtc && x.ReservedAt <= toUtc
                && (x.Status == "Pending" || x.Status == "Confirmed" || x.Status == "CheckingIn"))
            .OrderBy(x => x.ReservedAt)
            .Select(x => new { x.ReservationId, x.CustomerId, x.ReservedAt })
            .FirstOrDefaultAsync(cancellationToken);
        return hold is null ? NotFound() : Ok(hold);
    }

    [HttpPut("api/internal/reservations/{reservationId:int}")]
    public async Task<ActionResult<object>> UpdateReservation(
        int reservationId, [FromBody] UpdateReservationRequest? request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Dữ liệu thay đổi không hợp lệ." });
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (_db.Database.IsRelational())
            await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(_db, ReservationLock(reservationId), cancellationToken);
        var reservation = await _db.Reservations.Include(x => x.PreOrderItems).Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);
        if (reservation is null) return NotFound(new { code = "reservation_not_found", message = "Không tìm thấy đặt bàn." });
        if (!string.Equals(reservation.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { code = "reservation_state_conflict", message = "Trạng thái hiện tại không cho phép thay đổi đặt bàn." });
        if (!string.IsNullOrWhiteSpace(request.MutationKey) && string.Equals(reservation.LastMutationKey, request.MutationKey, StringComparison.Ordinal))
        {
            if (!string.Equals(reservation.LastMutationHash, request.MutationHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
            return Ok(MapReservation(reservation));
        }
        if (request.PartySize is < 1 or > 30 || request.ReservedAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest(new { code = "validation_failed", message = "Số khách hoặc thời gian đặt bàn không hợp lệ." });
        if (request.TableId is > 0 && await _db.Reservations.AnyAsync(x => x.ReservationId != reservationId
                && x.BranchId == reservation.BranchId && x.TableId == request.TableId
                && x.ReservedAt < request.ReservedAt.AddMinutes(120)
                && x.ReservedAt.AddMinutes(120) > request.ReservedAt
                && !ClosedReservationStatuses.Contains(x.Status), cancellationToken))
            return Conflict(new { code = "reservation_slot_unavailable", message = "Bàn đã được giữ trong khung giờ này." });

        reservation.TableId = request.TableId;
        reservation.PartySize = request.PartySize;
        reservation.ReservedAt = request.ReservedAt;
        reservation.Note = NormalizeOptional(request.Note, 1000);
        reservation.LastMutationKey = NormalizeOptional(request.MutationKey, 140);
        reservation.LastMutationHash = NormalizeOptional(request.MutationHash, 64);
        reservation.UpdatedAtUtc = DateTime.UtcNow;
        reservation.ReservationTables.Clear();
        if (request.TableId is > 0)
            reservation.ReservationTables.Add(new ReservationTable { TableId = request.TableId.Value, IsPrimary = true, CreatedAtUtc = DateTime.UtcNow });
        await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationUpdated, reservation, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(MapReservation(reservation));
    }

    [HttpPost("api/internal/reservations/{reservationId:int}/preorder/submit")]
    public async Task<ActionResult<object>> SubmitPreOrder(
        int reservationId, [FromBody] SubmitPreOrderRequest? request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Dữ liệu xác nhận không hợp lệ." });
        var reservation = await _db.Reservations.Include(x => x.PreOrderItems).Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);
        if (reservation is null) return NotFound(new { code = "reservation_not_found", message = "Không tìm thấy đặt bàn." });
        if (!string.Equals(reservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { code = "reservation_state_conflict", message = "Chỉ đặt bàn đã xác nhận mới được gửi món trước." });
        if (string.Equals(reservation.LastMutationKey, request.MutationKey, StringComparison.Ordinal))
        {
            if (!string.Equals(reservation.LastMutationHash, request.MutationHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
            return Ok(MapReservation(reservation));
        }
        if (reservation.PreOrderItems.Count == 0)
            return UnprocessableEntity(new { code = "preorder_empty", message = "Giỏ món đặt trước đang trống." });
        var now = DateTime.UtcNow;
        foreach (var item in reservation.PreOrderItems.Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
        {
            item.Status = "Submitted";
            item.UpdatedAtUtc = now;
        }
        reservation.PreOrderSubmittedAtUtc = now;
        reservation.LastMutationKey = NormalizeOptional(request.MutationKey, 140);
        reservation.LastMutationHash = NormalizeOptional(request.MutationHash, 64);
        reservation.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapReservation(reservation));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/preorder-items")]
    public async Task<ActionResult<object>> ReplacePreOrderItems(
        int reservationId,
        [FromBody] ReplacePreOrderItemsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Dữ liệu món đặt trước không hợp lệ." });
        }

        var reservation = await _db.Reservations
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);

        if (reservation is null)
        {
            return NotFound(new { message = "Không tìm thấy đặt bàn." });
        }
        if (ClosedReservationStatuses.Contains(reservation.Status))
        {
            return Conflict(new { message = "Không thể cập nhật món đặt trước cho đặt bàn đã đóng." });
        }

        var items = request.Items ?? Array.Empty<PreOrderItemRequest>();
        var normalizedItems = new List<ReservationPreOrderItem>();
        foreach (var item in items)
        {
            var dishName = NormalizeRequired(item.DishNameSnapshot);
            if (item.DishId <= 0 || item.Quantity <= 0 || dishName is null || item.UnitPriceSnapshot < 0)
            {
                return BadRequest(new { message = "Món đặt trước không hợp lệ." });
            }

            normalizedItems.Add(new ReservationPreOrderItem
            {
                ReservationId = reservation.ReservationId,
                DishId = item.DishId,
                DishNameSnapshot = dishName,
                UnitPriceSnapshot = item.UnitPriceSnapshot,
                Quantity = item.Quantity,
                Note = NormalizeOptional(item.Note, 1000),
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var pendingItems = reservation.PreOrderItems
            .Where(x => x.ConvertedAtUtc is null && !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pendingItems.Count > 0)
        {
            _db.ReservationPreOrderItems.RemoveRange(pendingItems);
        }

        reservation.UpdatedAtUtc = DateTime.UtcNow;
        reservation.PreOrderSubmittedAtUtc = null;
        _db.ReservationPreOrderItems.AddRange(normalizedItems);
        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.Reservations
            .AsNoTracking()
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstAsync(x => x.ReservationId == reservationId, cancellationToken);

        return Ok(MapReservation(saved));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/cancel")]
    public async Task<ActionResult<object>> CancelReservation(int reservationId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginReservationLockAsync(reservationId, cancellationToken);
        var reservation = await _db.Reservations
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);

        if (reservation is null)
        {
            return NotFound(new { message = "Không tìm thấy đặt bàn." });
        }
        if (string.Equals(reservation.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapReservation(reservation)); // a repeated cancel changes nothing
        }
        if (string.Equals(reservation.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { code = "reservation_state_conflict", message = "Không thể hủy đặt bàn đang check-in, đã check-in hoặc đã hoàn tất." });
        }
        if (string.Equals(reservation.Status, "NoShow", StringComparison.OrdinalIgnoreCase) || IsOverdue(reservation, DateTime.UtcNow))
        {
            return Conflict(new { code = "reservation_expired", message = "Đặt bàn đã quá giờ hẹn nên không thể hủy." });
        }

        var now = DateTime.UtcNow;
        reservation.Status = "Cancelled";
        reservation.CancelledAtUtc = now;
        reservation.UpdatedAtUtc = now;

        foreach (var item in reservation.PreOrderItems.Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
        {
            item.Status = "Cancelled";
            item.UpdatedAtUtc = now;
        }
        await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationCancelled, reservation, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(MapReservation(reservation));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/begin-check-in")]
    public async Task<ActionResult<object>> BeginReservationCheckIn(
        int reservationId,
        [FromQuery] int? employeeId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginReservationLockAsync(reservationId, cancellationToken);
        var reservation = await LoadReservationForStateChangeAsync(reservationId, cancellationToken);
        if (reservation.Result is not null) return reservation.Result;
        var entity = reservation.Value!;

        if (string.Equals(entity.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapReservation(entity));
        }

        if (!string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entity.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Không thể bắt đầu check-in cho đặt bàn ở trạng thái hiện tại." });
        }

        var now = DateTime.UtcNow;
        AutoConfirmIfPending(entity, employeeId, now);
        entity.Status = "CheckingIn";
        entity.CheckInStartedAtUtc = now;
        entity.CheckInIdempotencyKey = BuildCheckInIdempotencyKey(entity.ReservationId);
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(MapReservation(entity));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/complete-check-in")]
    public async Task<ActionResult<object>> CompleteReservationCheckIn(
        int reservationId,
        [FromBody] CheckInReservationRequest? request,
        CancellationToken cancellationToken)
    {
        var reservation = await LoadReservationForStateChangeAsync(reservationId, cancellationToken);
        if (reservation.Result is not null) return reservation.Result;
        var entity = reservation.Value!;

        if (string.Equals(entity.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            if (request?.ConvertedOrderId is int requestOrderId
                && entity.ConvertedOrderId is int existingOrderId
                && requestOrderId != existingOrderId)
            {
                return Conflict(new { message = "Đặt bàn đã check-in với đơn hàng khác." });
            }

            return Ok(MapReservation(entity));
        }

        if (!string.Equals(entity.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Đặt bàn chưa ở trạng thái đang check-in." });
        }

        CompleteCheckIn(entity, request);
        SyncReservationTables(entity, request);
        await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationCheckedIn, entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapReservation(entity));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/fail-check-in")]
    public async Task<ActionResult<object>> FailReservationCheckIn(int reservationId, CancellationToken cancellationToken)
    {
        var reservation = await LoadReservationForStateChangeAsync(reservationId, cancellationToken);
        if (reservation.Result is not null) return reservation.Result;
        var entity = reservation.Value!;

        if (!string.Equals(entity.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapReservation(entity));
        }

        entity.Status = "Confirmed";
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapReservation(entity));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/check-in")]
    public async Task<ActionResult<object>> MarkReservationCheckedIn(
        int reservationId,
        [FromBody] CheckInReservationRequest? request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginReservationLockAsync(reservationId, cancellationToken);
        var reservation = await LoadReservationForStateChangeAsync(reservationId, cancellationToken);
        if (reservation.Result is not null) return reservation.Result;
        var entity = reservation.Value!;

        if (string.Equals(entity.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapReservation(entity));
        }

        if (string.Equals(entity.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "NoShow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Không thể check-in đặt bàn đã hủy, bị từ chối, không đến hoặc đã hoàn tất." });
        }

        AutoConfirmIfPending(entity, request?.CheckedInByEmployeeId, DateTime.UtcNow);
        entity.Status = "CheckingIn";
        entity.CheckInStartedAtUtc ??= DateTime.UtcNow;
        entity.CheckInIdempotencyKey ??= BuildCheckInIdempotencyKey(entity.ReservationId);
        CompleteCheckIn(entity, request);
        SyncReservationTables(entity, request);
        await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationCheckedIn, entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(MapReservation(entity));
    }

    /// <summary>Pending bookings of one branch from now on (all days), soonest first, for the cashier to decide.</summary>
    [HttpGet("api/internal/reservations/pending")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetPendingReservations([FromQuery] int branchId, CancellationToken cancellationToken)
    {
        if (branchId <= 0) return BadRequest(new { code = "validation_failed", message = "Chi nhánh không hợp lệ." });
        await MarkNoShowsAsync(null, branchId, null, cancellationToken);
        var reservations = await _db.Reservations.AsNoTracking()
            .Include(x => x.PreOrderItems).Include(x => x.ReservationTables)
            .Where(x => x.BranchId == branchId && x.Status == "Pending")
            .OrderBy(x => x.ReservedAt).ThenBy(x => x.ReservationId)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Ok(reservations.Select(MapReservation).ToArray());
    }

    [HttpPost("api/internal/reservations/{reservationId:int}/confirm")]
    public Task<ActionResult<object>> ConfirmReservation(int reservationId, [FromBody] ReservationDecisionRequest? request,
        CancellationToken cancellationToken) =>
        DecideAsync(reservationId, "Confirmed", request?.EmployeeId, null, cancellationToken);

    [HttpPost("api/internal/reservations/{reservationId:int}/reject")]
    public Task<ActionResult<object>> RejectReservation(int reservationId, [FromBody] ReservationDecisionRequest? request,
        CancellationToken cancellationToken) =>
        DecideAsync(reservationId, "Rejected", request?.EmployeeId, NormalizeOptional(request?.Reason, 300), cancellationToken);

    /// <summary>"Khách không đến": the cashier closes a booking whose time has come, before the automatic 30 minutes.</summary>
    [HttpPost("api/internal/reservations/{reservationId:int}/no-show")]
    public Task<ActionResult<object>> MarkReservationNoShow(int reservationId, [FromBody] ReservationDecisionRequest? request,
        CancellationToken cancellationToken) =>
        DecideAsync(reservationId, "NoShow", request?.EmployeeId, null, cancellationToken);

    /// <summary>
    /// The restaurant's decision on a booking. Under the reservation lock, so it cannot cross a check-in, a guest
    /// cancel/change or a second cashier; repeating the same decision returns the booking unchanged.
    /// </summary>
    private async Task<ActionResult<object>> DecideAsync(int reservationId, string target, int? employeeId, string? reason,
        CancellationToken ct)
    {
        if (target == "Rejected" && reason is null)
            return BadRequest(new { code = "reason_required", message = "Vui lòng chọn lý do từ chối." });
        await using var transaction = await BeginReservationLockAsync(reservationId, ct);
        var loaded = await LoadReservationForStateChangeAsync(reservationId, ct);
        if (loaded.Result is not null) return loaded.Result;
        var entity = loaded.Value!;
        if (string.Equals(entity.Status, target, StringComparison.OrdinalIgnoreCase)) return Ok(MapReservation(entity));

        var now = DateTime.UtcNow;
        var pending = string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase);
        var confirmed = string.Equals(entity.Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        switch (target)
        {
            case "Confirmed" or "Rejected" when !pending:
                return Conflict(new { code = "reservation_state_conflict", message = "Đặt bàn không còn chờ xác nhận. Vui lòng tải lại danh sách." });
            case "Confirmed" when IsOverdue(entity, now):
                return Conflict(new { code = "reservation_expired", message = "Đặt bàn đã quá giờ hẹn nên không thể xác nhận." });
            case "NoShow" when !pending && !confirmed:
                return Conflict(new { code = "reservation_state_conflict", message = "Chỉ đánh dấu khách không đến cho đặt bàn đang chờ hoặc đã xác nhận." });
            case "NoShow" when entity.ReservedAt > now:
                return Conflict(new { code = "reservation_not_due", message = "Chưa tới giờ hẹn nên chưa thể đánh dấu khách không đến." });
        }

        entity.Status = target;
        entity.StatusChangedAtUtc = now;
        entity.StatusChangedByEmployeeId = employeeId is > 0 ? employeeId : null;
        entity.StatusReason = reason;
        entity.UpdatedAtUtc = now;
        if (target != "Confirmed")
            foreach (var item in entity.PreOrderItems.Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
            {
                item.Status = "Cancelled";
                item.UpdatedAtUtc = now;
            }
        await CustomerInbox.AddReservationAsync(_db, target switch
        {
            "Confirmed" => CustomerInbox.ReservationConfirmed,
            "Rejected" => CustomerInbox.ReservationRejected,
            _ => CustomerInbox.ReservationNoShow,
        }, entity, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(MapReservation(entity));
    }

    /// <summary>A guest may check in before the cashier confirmed: the check-in confirms the booking (owner, 26/10).</summary>
    private static void AutoConfirmIfPending(Reservation entity, int? employeeId, DateTime now)
    {
        if (!string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase)) return;
        entity.Status = "Confirmed";
        entity.StatusChangedAtUtc = now;
        entity.StatusChangedByEmployeeId = employeeId is > 0 ? employeeId : null;
        entity.StatusReason = AutoConfirmedAtCheckIn;
    }

    private static string ReservationLock(int reservationId) => "reservation:" + reservationId;

    /// <summary>One state change of a reservation at a time (null on the in-memory test provider).</summary>
    private async Task<IDbContextTransaction?> BeginReservationLockAsync(int reservationId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return null;
        var transaction = await _db.Database.BeginTransactionAsync(ct);
        await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(_db, ReservationLock(reservationId), ct);
        return transaction;
    }

    [HttpGet("api/customers/{customerId:int}/orders")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetOrders(
        int customerId,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        var orders = await _ordersQueryClient.GetCustomerOrdersAsync(customerId, take, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("api/customers/{customerId:int}/ready-notifications")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetReadyNotifications(
        int customerId,
        [FromQuery] int? tableId = null,
        [FromQuery] string status = "OPEN",
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "OPEN" : status.Trim().ToUpperInvariant();

        var query = _db.ReadyDishNotifications
            .AsNoTracking()
            // Only "dish ready" rows: this table-wide feed must never show payments or reservations.
            .Where(x => x.Status == normalizedStatus && x.EventName == CustomerInbox.OrderReady)
            .AsQueryable();

        if (tableId is > 0)
        {
            query = query.Where(x => x.TableId == tableId.Value || x.CustomerId == customerId);
        }
        else
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                notificationId = x.ReadyDishNotificationId,
                orderId = x.OrderId,
                orderItemId = x.OrderItemId,
                dishId = x.DishId,
                dishName = x.DishName,
                customerId = x.CustomerId,
                tableId = x.TableId,
                eventName = x.EventName,
                message = x.Message,
                status = x.Status,
                createdAt = x.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("api/customers/{customerId:int}/ready-notifications/{notificationId:long}/resolve")]
    public async Task<ActionResult<object>> ResolveReadyNotification(
        int customerId,
        long notificationId,
        [FromQuery] int? tableId,
        CancellationToken cancellationToken)
    {
        var entity = await _db.ReadyDishNotifications
            .FirstOrDefaultAsync(
                x => x.ReadyDishNotificationId == notificationId
                     && (
                         x.CustomerId == customerId
                         || (x.CustomerId == null && tableId != null && x.TableId == tableId.Value)
                     ),
                cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy thông báo." });
        }

        var resolvedAtUtc = DateTime.UtcNow;
        var siblingNotifications = await _db.ReadyDishNotifications
            .Where(x =>
                x.Status == "OPEN"
                && x.OrderId == entity.OrderId
                && x.OrderItemId == entity.OrderItemId
                && (
                    x.CustomerId == customerId
                    || (x.CustomerId == null && tableId != null && x.TableId == tableId.Value)
                ))
            .ToListAsync(cancellationToken);

        foreach (var notification in siblingNotifications)
        {
            notification.Status = "RESOLVED";
            notification.ResolvedAtUtc = resolvedAtUtc;
        }

        if (siblingNotifications.Count == 0)
        {
            entity.Status = "RESOLVED";
            entity.ResolvedAtUtc = resolvedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            readyDishNotificationId = entity.ReadyDishNotificationId,
            orderId = entity.OrderId,
            resolvedCount = siblingNotifications.Count == 0 ? 1 : siblingNotifications.Count,
            status = "RESOLVED",
        });
    }

    [HttpPost("api/dev/reset-test-state")]
    public async Task<ActionResult<object>> ResetDevTestState(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var readyNotifications = await _db.ReadyDishNotifications.ToListAsync(cancellationToken);
        var inboxEvents = await _db.InboxEvents.ToListAsync(cancellationToken);
        var reservationItems = await _db.ReservationPreOrderItems.ToListAsync(cancellationToken);
        var reservations = await _db.Reservations.ToListAsync(cancellationToken);
        var favorites = await _db.CustomerFavorites.ToListAsync(cancellationToken);
        var reviews = await _db.CustomerReviews.ToListAsync(cancellationToken);
        var supportRequests = await _db.SupportRequests.ToListAsync(cancellationToken);

        if (favorites.Count > 0) _db.CustomerFavorites.RemoveRange(favorites);
        if (reviews.Count > 0) _db.CustomerReviews.RemoveRange(reviews);
        if (supportRequests.Count > 0) _db.SupportRequests.RemoveRange(supportRequests);

        if (readyNotifications.Count > 0)
        {
            _db.ReadyDishNotifications.RemoveRange(readyNotifications);
        }

        if (inboxEvents.Count > 0)
        {
            _db.InboxEvents.RemoveRange(inboxEvents);
        }
        if (reservationItems.Count > 0)
        {
            _db.ReservationPreOrderItems.RemoveRange(reservationItems);
        }
        if (reservations.Count > 0)
        {
            _db.Reservations.RemoveRange(reservations);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            clearedReadyNotifications = readyNotifications.Count,
            clearedInboxEvents = inboxEvents.Count,
            clearedReservations = reservations.Count,
            clearedReservationPreOrderItems = reservationItems.Count
        });
    }

    private static string? NormalizeRequired(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<ActionResult<Reservation>> LoadReservationForStateChangeAsync(int reservationId, CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return BadRequest(new { message = "Đặt bàn không hợp lệ." });
        }

        var reservation = await _db.Reservations
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);

        if (reservation is null)
        {
            return NotFound(new { message = "Không tìm thấy đặt bàn." });
        }

        return reservation;
    }

    private static void CompleteCheckIn(Reservation reservation, CheckInReservationRequest? request)
    {
        var now = DateTime.UtcNow;
        reservation.Status = "CheckedIn";
        var selectedTableIds = NormalizeTableIds(request?.TableIds);
        var primaryTableId = ResolvePrimaryTableId(request?.TableId, selectedTableIds);
        if (primaryTableId is > 0)
        {
            reservation.TableId = primaryTableId;
        }
        reservation.ConvertedOrderId = request?.ConvertedOrderId;
        reservation.DiningSessionCode = NormalizeOptional(request?.DiningSessionCode, 64);
        reservation.CheckedInAtUtc = now;
        reservation.CheckedInByEmployeeId = request?.CheckedInByEmployeeId is > 0 ? request.CheckedInByEmployeeId : null;
        reservation.UpdatedAtUtc = now;
        reservation.CheckInIdempotencyKey ??= BuildCheckInIdempotencyKey(reservation.ReservationId);

        foreach (var item in reservation.PreOrderItems.Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
        {
            item.Status = "ConvertedToOrder";
            item.ConvertedAtUtc = request?.ConvertedAtUtc ?? now;
            item.UpdatedAtUtc = now;
        }
    }

    private static void SyncReservationTables(Reservation reservation, CheckInReservationRequest? request)
    {
        var selectedTableIds = NormalizeTableIds(request?.TableIds);
        if (selectedTableIds.Count == 0)
        {
            var fallbackTableId = request?.TableId is > 0 ? request.TableId.Value : reservation.TableId;
            if (fallbackTableId is > 0)
            {
                selectedTableIds = [fallbackTableId.Value];
            }
        }

        if (selectedTableIds.Count == 0)
        {
            return;
        }

        var primaryTableId = ResolvePrimaryTableId(request?.TableId, selectedTableIds) ?? selectedTableIds[0];
        reservation.TableId = primaryTableId;
        reservation.ReservationTables.Clear();
        var now = DateTime.UtcNow;
        foreach (var tableId in selectedTableIds)
        {
            reservation.ReservationTables.Add(new ReservationTable
            {
                ReservationId = reservation.ReservationId,
                TableId = tableId,
                IsPrimary = tableId == primaryTableId,
                CreatedAtUtc = now
            });
        }
    }

    private static List<int> NormalizeTableIds(IReadOnlyList<int>? tableIds)
    {
        var result = new List<int>();
        if (tableIds is null)
        {
            return result;
        }

        foreach (var tableId in tableIds)
        {
            if (tableId <= 0 || result.Contains(tableId))
            {
                continue;
            }

            result.Add(tableId);
        }

        return result;
    }

    private static int? ResolvePrimaryTableId(int? requestedPrimaryTableId, IReadOnlyList<int> selectedTableIds)
    {
        if (requestedPrimaryTableId is > 0)
        {
            if (selectedTableIds.Count == 0 || selectedTableIds.Contains(requestedPrimaryTableId.Value))
            {
                return requestedPrimaryTableId.Value;
            }
        }

        return selectedTableIds.Count > 0 ? selectedTableIds[0] : null;
    }

    private static string BuildCheckInIdempotencyKey(int reservationId) => $"reservation-checkin-{reservationId}";

    // Every reservation is created with a 30-minute arrival window; the query uses that constant so it translates
    // on SQL Server and PostgreSQL, and IsOverdue re-checks the stored window.
    private const int DefaultArrivalWindowMinutes = 30;

    /// <summary>Pending/Confirmed but the arrival window (reserved time + window) is over: the guest did not come.</summary>
    public static bool IsOverdue(Reservation x, DateTime nowUtc) =>
        (string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
        && x.ReservedAt.AddMinutes(Math.Max(1, x.ArrivalWindowMinutes)) < nowUtc;

    /// <summary>
    /// Nothing else marks a missed reservation, so it stayed "Confirmed" (and "Sắp tới") forever. Reads that list
    /// reservations mark the overdue ones NoShow first. The status condition is part of the UPDATE, so a check-in
    /// that started meanwhile (CheckingIn) is never overwritten.
    /// </summary>
    private async Task MarkNoShowsAsync(int? customerId, int? branchId, int? reservationId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return;
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-DefaultArrivalWindowMinutes);
        var query = _db.Reservations.Where(x => (x.Status == "Pending" || x.Status == "Confirmed") && x.ReservedAt < cutoff);
        if (customerId is > 0) query = query.Where(x => x.CustomerId == customerId.Value);
        if (branchId is > 0) query = query.Where(x => x.BranchId == branchId.Value);
        if (reservationId is > 0) query = query.Where(x => x.ReservationId == reservationId.Value);
        var candidates = await query.Select(x => x.ReservationId).ToListAsync(ct);
        if (candidates.Count == 0) return;
        var changed = await _db.Reservations.Where(x => candidates.Contains(x.ReservationId)
                && (x.Status == "Pending" || x.Status == "Confirmed") && x.ReservedAt < cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "NoShow").SetProperty(x => x.UpdatedAtUtc, now), ct);
        if (changed == 0) return;
        // Tell each guest once; rows another read marked NoShow meanwhile are found by the inbox duplicate check.
        var missed = await _db.Reservations.AsNoTracking()
            .Where(x => candidates.Contains(x.ReservationId) && x.Status == "NoShow" && x.CustomerId != null).ToListAsync(ct);
        foreach (var reservation in missed)
            await CustomerInbox.AddReservationAsync(_db, CustomerInbox.ReservationNoShow, reservation, ct);
        await _db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo GetRestaurantTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private async Task<string> GenerateReservationCodeAsync(DateTime reservedAt, CancellationToken cancellationToken)
    {
        var datePart = reservedAt.ToString("yyyyMMdd");
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Random.Shared.Next(0, 10000).ToString("D4");
            var code = $"RSV-{datePart}-{suffix}";
            if (!await _db.Reservations.AnyAsync(x => x.ReservationCode == code, cancellationToken))
            {
                return code;
            }
        }

        return $"RSV-{datePart}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
    }

    private static object MapReservation(Reservation reservation)
    {
        var items = reservation.PreOrderItems
            .OrderBy(x => x.ReservationItemId)
            .Select(x => new
            {
                reservationItemId = x.ReservationItemId,
                reservationId = x.ReservationId,
                dishId = x.DishId,
                dishNameSnapshot = x.DishNameSnapshot,
                unitPriceSnapshot = x.UnitPriceSnapshot,
                quantity = x.Quantity,
                note = x.Note,
                status = x.Status,
                createdAtUtc = x.CreatedAtUtc,
                updatedAtUtc = x.UpdatedAtUtc,
                convertedAtUtc = x.ConvertedAtUtc
            })
            .ToArray();

        var assignedTables = reservation.ReservationTables.Count > 0
            ? reservation.ReservationTables
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.ReservationTableId)
                .Select(x => new { tableId = x.TableId, isPrimary = x.IsPrimary })
                .ToArray()
            : reservation.TableId is > 0
                ? [new { tableId = reservation.TableId.Value, isPrimary = true }]
                : [];

        return new
        {
            reservationId = reservation.ReservationId,
            reservationCode = reservation.ReservationCode,
            customerId = reservation.CustomerId,
            customerName = reservation.CustomerName,
            phoneNumber = reservation.PhoneNumber,
            branchId = reservation.BranchId,
            tableId = reservation.TableId,
            partySize = reservation.PartySize,
            reservedAt = reservation.ReservedAt,
            arrivalWindowMinutes = reservation.ArrivalWindowMinutes,
            status = reservation.Status,
            note = reservation.Note,
            convertedOrderId = reservation.ConvertedOrderId,
            diningSessionCode = reservation.DiningSessionCode,
            checkInStartedAtUtc = reservation.CheckInStartedAtUtc,
            checkInIdempotencyKey = reservation.CheckInIdempotencyKey,
            checkedInAtUtc = reservation.CheckedInAtUtc,
            checkedInByEmployeeId = reservation.CheckedInByEmployeeId,
            cancelledAtUtc = reservation.CancelledAtUtc,
            createdAtUtc = reservation.CreatedAtUtc,
            updatedAtUtc = reservation.UpdatedAtUtc,
            preOrderSubmittedAtUtc = reservation.PreOrderSubmittedAtUtc,
            statusChangedAtUtc = reservation.StatusChangedAtUtc,
            statusChangedByEmployeeId = reservation.StatusChangedByEmployeeId,
            statusReason = reservation.StatusReason,
            assignedTables,
            preOrderItems = items
        };
    }

    public sealed record CreateReservationRequest(
        string? CustomerName,
        string? PhoneNumber,
        int? CustomerId,
        int BranchId,
        int? TableId,
        int PartySize,
        DateTime ReservedAt,
        string? Note,
        string? IdempotencyKey,
        string? RequestHash = null,
        string? InitialStatus = null);

    public sealed record UpdateReservationRequest(int? TableId, int PartySize, DateTime ReservedAt,
        string? Note, string? MutationKey, string? MutationHash);

    public sealed record ReservationDecisionRequest(int? EmployeeId, string? Reason = null);

    public sealed record SubmitPreOrderRequest(string MutationKey, string MutationHash);

    public sealed record ReplacePreOrderItemsRequest(IReadOnlyList<PreOrderItemRequest>? Items);

    public sealed record PreOrderItemRequest(
        int DishId,
        string? DishNameSnapshot,
        decimal UnitPriceSnapshot,
        int Quantity,
        string? Note);

    public sealed record CheckInReservationRequest(
        int? ConvertedOrderId,
        string? DiningSessionCode,
        int? CheckedInByEmployeeId,
        DateTime? ConvertedAtUtc,
        int? TableId = null,
        IReadOnlyList<int>? TableIds = null);
}
