using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
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
        "Completed"
    };
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

    [HttpPut("api/internal/reservations/{reservationId:int}")]
    public async Task<ActionResult<object>> UpdateReservation(
        int reservationId, [FromBody] UpdateReservationRequest? request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Dữ liệu thay đổi không hợp lệ." });
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
        var reservation = await _db.Reservations
            .Include(x => x.PreOrderItems)
                .Include(x => x.ReservationTables)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, cancellationToken);

        if (reservation is null)
        {
            return NotFound(new { message = "Không tìm thấy đặt bàn." });
        }
        if (string.Equals(reservation.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Không thể hủy đặt bàn đã check-in hoặc hoàn tất." });
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

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapReservation(reservation));
    }

    [HttpPost("api/customers/reservations/{reservationId:int}/begin-check-in")]
    public async Task<ActionResult<object>> BeginReservationCheckIn(
        int reservationId,
        CancellationToken cancellationToken)
    {
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
            return Conflict(new { message = "Kh?ng th? b?t ??u check-in cho ??t b?n ? tr?ng th?i hi?n t?i." });
        }

        var now = DateTime.UtcNow;
        entity.Status = "CheckingIn";
        entity.CheckInStartedAtUtc = now;
        entity.CheckInIdempotencyKey = BuildCheckInIdempotencyKey(entity.ReservationId);
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
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
                return Conflict(new { message = "??t b?n ?? check-in v?i ??n h?ng kh?c." });
            }

            return Ok(MapReservation(entity));
        }

        if (!string.Equals(entity.Status, "CheckingIn", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "??t b?n ch?a ? tr?ng th?i ?ang check-in." });
        }

        CompleteCheckIn(entity, request);
        SyncReservationTables(entity, request);
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
        var reservation = await LoadReservationForStateChangeAsync(reservationId, cancellationToken);
        if (reservation.Result is not null) return reservation.Result;
        var entity = reservation.Value!;

        if (string.Equals(entity.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapReservation(entity));
        }

        if (string.Equals(entity.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "NoShow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Kh?ng th? check-in ??t b?n ?? h?y, kh?ng ??n ho?c ho?n t?t." });
        }

        entity.Status = "CheckingIn";
        entity.CheckInStartedAtUtc ??= DateTime.UtcNow;
        entity.CheckInIdempotencyKey ??= BuildCheckInIdempotencyKey(entity.ReservationId);
        CompleteCheckIn(entity, request);
        SyncReservationTables(entity, request);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapReservation(entity));
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
            .Where(x => x.Status == normalizedStatus)
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
