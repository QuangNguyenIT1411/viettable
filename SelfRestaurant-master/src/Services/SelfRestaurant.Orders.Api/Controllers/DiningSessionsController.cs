using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using SelfRestaurant.Orders.Api.Domain.ServiceRequests;
using OrderEntity = SelfRestaurant.Orders.Api.Persistence.Entities.Orders;

namespace SelfRestaurant.Orders.Api.Controllers;

[ApiController]
[Route("api/internal/dining-sessions")]
public sealed class DiningSessionsController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly ICatalogReadModel _catalog;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public DiningSessionsController(OrdersDbContext db, ICatalogReadModel catalog,
        IConfiguration configuration, IHostEnvironment environment)
    {
        _db = db;
        _catalog = catalog;
        _configuration = configuration;
        _environment = environment;
    }

    private void MaybeInjectTableReleaseFailure()
    {
        if (_environment.IsDevelopment() && _configuration.GetValue<bool>("Recovery:TestFailTableRelease"))
            throw new InvalidOperationException("Injected failure after session close before table release.");
    }

    [HttpGet("active-table-ids")]
    public async Task<ActionResult<int[]>> ActiveTableIds(CancellationToken ct) =>
        Ok(await _db.DiningSessions.AsNoTracking()
            .Where(x => x.EndedAtUtc == null)
            .Select(x => x.TableId).Distinct().ToArrayAsync(ct));

    [HttpPost("check-in")]
    public async Task<ActionResult<DiningSessionResponse>> CheckIn(CreateDiningSessionRequest request, CancellationToken ct)
    {
        if (request.CustomerId <= 0 || request.BranchId <= 0 || request.TableId <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 140
            || string.IsNullOrWhiteSpace(request.RequestHash) || request.RequestHash.Length != 64)
            return BadRequest(new { code = "validation_failed", message = "Thông tin check-in không hợp lệ." });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var scopedKey = request.IdempotencyKey.Trim();
        var existing = await _db.DiningSessions.FirstOrDefaultAsync(x => x.CheckInIdempotencyKey == scopedKey, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, request.RequestHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
            return Ok(Map(existing, alreadyCheckedIn: true));
        }

        if (request.ReservationId is > 0)
        {
            existing = await _db.DiningSessions.FirstOrDefaultAsync(x => x.ReservationId == request.ReservationId, ct);
            if (existing is not null)
            {
                if (existing.CustomerId != request.CustomerId || existing.BranchId != request.BranchId || existing.TableId != request.TableId)
                    return Conflict(new { code = "reservation_session_conflict", message = "Đặt bàn đã được check-in với phiên khác." });
                return Ok(Map(existing, alreadyCheckedIn: true));
            }
        }

        if (await _db.DiningSessions.AnyAsync(x => x.CustomerId == request.CustomerId && x.EndedAtUtc == null, ct))
            return Conflict(new { code = "customer_session_active", message = "Khách hàng đang có một phiên phục vụ hoạt động." });
        if (await _db.DiningSessions.AnyAsync(x => x.TableId == request.TableId && x.EndedAtUtc == null, ct))
            return Conflict(new { code = "table_session_active", message = "Bàn đang có một phiên phục vụ hoạt động." });
        if (await _db.Orders.AnyAsync(x => x.TableID == request.TableId && (x.IsActive ?? true), ct))
            return Conflict(new { code = "table_order_active", message = "Bàn đang có đơn hàng hoạt động từ phiên cũ." });

        var now = DateTime.UtcNow;
        var session = new DiningSession
        {
            SessionCode = $"DS-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..33].ToUpperInvariant(),
            CustomerId = request.CustomerId,
            BranchId = request.BranchId,
            TableId = request.TableId,
            ReservationId = request.ReservationId is > 0 ? request.ReservationId : null,
            Status = "ACTIVE",
            CheckInIdempotencyKey = scopedKey,
            RequestHash = request.RequestHash,
            StartedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.DiningSessions.Add(session);
        _db.DiningSessionTables.Add(new DiningSessionTables
        {
            DiningSessionCode = session.SessionCode,
            TableID = request.TableId,
            IsPrimary = true,
            CreatedAtUtc = now
        });

        if (request.ReservationId is > 0)
        {
            var preOrder = await _db.ReservationPreOrders.Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.ReservationId == request.ReservationId, ct);
            if (preOrder is not null)
            {
                if (preOrder.CustomerId != request.CustomerId || preOrder.BranchId != request.BranchId)
                    return Conflict(new { code = "preorder_context_conflict", message = "Pre-order không thuộc phiên check-in này." });
                if (preOrder.ConvertedOrderId is > 0)
                    session.ConvertedOrderId = preOrder.ConvertedOrderId;
                else if (string.Equals(preOrder.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) && preOrder.Items.Count > 0)
                {
                    var confirmedStatusId = await _db.OrderStatus.Where(x => x.StatusCode == "CONFIRMED")
                        .Select(x => x.StatusID).FirstAsync(ct);
                    var order = new OrderEntity
                    {
                        DiningSessionCode = session.SessionCode,
                        SubmitIdempotencyKey = $"checkin-preorder-{request.ReservationId}",
                        OrderCode = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..34].ToUpperInvariant(),
                        OrderTime = now,
                        Note = $"Pre-order reservation #{request.ReservationId}",
                        IsActive = true,
                        TableID = request.TableId,
                        CustomerID = request.CustomerId,
                        StatusID = confirmedStatusId
                    };
                    _db.Orders.Add(order);
                    await _db.SaveChangesAsync(ct);
                    _db.OrderItems.AddRange(preOrder.Items.Select(item => new OrderItems
                    {
                        OrderID = order.OrderID,
                        DishID = item.DishId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPriceSnapshot,
                        LineTotal = item.UnitPriceSnapshot * item.Quantity,
                        Note = item.Note,
                        StatusCode = "CONFIRMED"
                    }));
                    preOrder.Status = "CONVERTED";
                    preOrder.ConvertedOrderId = order.OrderID;
                    preOrder.ConvertedAtUtc = now;
                    preOrder.UpdatedAtUtc = now;
                    session.ConvertedOrderId = order.OrderID;
                }
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return Conflict(new { code = "check_in_conflict", message = "Bàn hoặc đặt bàn vừa được check-in bởi yêu cầu khác." });
        }
        return Ok(Map(session, alreadyCheckedIn: false));
    }

    [HttpGet("active")]
    public async Task<ActionResult<DiningSessionResponse>> GetActive([FromQuery] int customerId, CancellationToken ct)
    {
        var entity = await _db.DiningSessions.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.EndedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        return entity is null ? NotFound(new { code = "active_session_not_found", message = "Không có phiên phục vụ đang hoạt động." }) : Ok(Map(entity, false));
    }

    [HttpGet("{sessionId:int}")]
    public async Task<ActionResult<DiningSessionResponse>> Get(int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var entity = await _db.DiningSessions.AsNoTracking().FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (entity is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        return Ok(Map(entity, false));
    }

    [HttpPost("{sessionId:int}/cancel-payment-request")]
    public async Task<ActionResult<DiningSessionResponse>> CancelPaymentRequest(int sessionId, [FromBody] CustomerSessionRequest request, CancellationToken ct)
    {
        var entity = await _db.DiningSessions.FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (entity is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (entity.CustomerId != request.CustomerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        if (entity.EndedAtUtc is not null) return Conflict(new { code = "session_closed", message = "Phiên phục vụ đã kết thúc." });
        if (string.Equals(entity.Status, "PAYMENT_REQUESTED", StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = "ACTIVE";
            entity.PaymentRequestedAtUtc = null;
            entity.CheckoutQuoteId = null;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(Map(entity, false));
    }

    [HttpGet("{sessionId:int}/checkout-context")]
    public async Task<ActionResult<DiningSessionCheckoutContext>> GetCheckoutContext(
        int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var session = await _db.DiningSessions.AsNoTracking().FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (session is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (session.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        if (session.EndedAtUtc is not null) return Conflict(new { code = "session_closed", message = "Phiên phục vụ đã kết thúc." });

        var orders = await _db.Orders.AsNoTracking()
            .Where(x => x.DiningSessionCode == session.SessionCode && (x.IsActive ?? true))
            .OrderBy(x => x.OrderTime).ToListAsync(ct);
        var orderIds = orders.Select(x => x.OrderID).ToArray();
        var items = await _db.OrderItems.AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderID) && x.StatusCode != "CANCELLED")
            .OrderBy(x => x.OrderID).ThenBy(x => x.ItemID).ToListAsync(ct);
        if (items.Count == 0) return Conflict(new { code = "empty_session", message = "Phiên chưa có món để thanh toán." });
        var dishes = await _catalog.GetDishesAsync(items.Select(x => x.DishID).Distinct(), ct)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var names = dishes.ToDictionary(x => x.DishId, x => x.Name);
        var rows = items.Select(x => new CheckoutItem(x.ItemID, x.OrderID, x.DishID,
            names.GetValueOrDefault(x.DishID) ?? $"Món #{x.DishID}", x.Quantity, x.UnitPrice, x.LineTotal)).ToArray();
        return Ok(new DiningSessionCheckoutContext(session.DiningSessionId, session.SessionCode, session.CustomerId,
            session.BranchId, session.TableId, session.Status, orders.Select(x => x.OrderID).ToArray(), rows.Sum(x => x.LineTotal), rows));
    }

    [HttpPost("{sessionId:int}/payment-request")]
    public async Task<ActionResult<DiningSessionResponse>> RequestPayment(
        int sessionId, [FromBody] PaymentSessionRequest request, CancellationToken ct)
    {
        var entity = await _db.DiningSessions.FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (entity is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (entity.CustomerId != request.CustomerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        if (entity.EndedAtUtc is not null) return Conflict(new { code = "session_closed", message = "Phiên phục vụ đã kết thúc." });
        if (entity.Status == "PAYMENT_REQUESTED" && entity.CheckoutQuoteId != request.CheckoutQuoteId)
            return Conflict(new { code = "payment_request_conflict", message = "Phiên đang được thanh toán bằng báo giá khác." });
        entity.Status = "PAYMENT_REQUESTED";
        entity.CheckoutQuoteId = request.CheckoutQuoteId;
        entity.PaymentRequestedAtUtc ??= DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity, false));
    }

    [HttpPost("{sessionId:int}/payment-succeeded")]
    public async Task<ActionResult<DiningSessionResponse>> PaymentSucceeded(
        int sessionId, [FromBody] CompletePaymentRequest request, CancellationToken ct)
    {
        var entity = await _db.DiningSessions.FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (entity is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (entity.CustomerId != request.CustomerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        if (entity.PaymentId == request.PaymentId && entity.CheckoutQuoteId == request.CheckoutQuoteId && entity.EndedAtUtc is not null)
        {
            // Retry a Catalog release that failed after the Orders commit; never release
            // a table already reused by a newer session.
            if (!await _db.DiningSessions.AnyAsync(x => x.TableId == entity.TableId && x.EndedAtUtc == null, ct))
            {
                MaybeInjectTableReleaseFailure();
                await _catalog.ReleaseTableAsync(entity.TableId, ct);
            }
            return Ok(Map(entity, false));
        }
        if (entity.Status != "PAYMENT_REQUESTED" || entity.CheckoutQuoteId != request.CheckoutQuoteId)
            return Conflict(new { code = "session_payment_state_conflict", message = "Phiên không ở trạng thái chờ thanh toán phù hợp." });
        var completedStatusId = await _db.OrderStatus.Where(x => x.StatusCode == "COMPLETED").Select(x => (int?)x.StatusID).FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        var orders = await _db.Orders.Where(x => x.DiningSessionCode == entity.SessionCode && (x.IsActive ?? true)).ToListAsync(ct);
        foreach (var order in orders) { order.IsActive = false; order.CompletedTime = now; if (completedStatusId is not null) order.StatusID = completedStatusId.Value; }
        var openRequests = await _db.ServiceRequests.Where(x => x.DiningSessionId == sessionId && (x.Status == "OPEN" || x.Status == "ACKNOWLEDGED")).ToListAsync(ct);
        foreach (var serviceRequest in openRequests) { serviceRequest.Status = "CANCELLED"; serviceRequest.CancelledAtUtc = now; serviceRequest.UpdatedAtUtc = now; }
        entity.Status = "CLOSED";
        entity.PaymentId = request.PaymentId;
        entity.EndedAtUtc = now;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        MaybeInjectTableReleaseFailure();
        await _catalog.ReleaseTableAsync(entity.TableId, ct);
        return Ok(Map(entity, false));
    }

    [HttpPost("{sessionId:int}/orders")]
    public async Task<ActionResult<MobileOrderResponse>> CreateOrder(int sessionId, CreateMobileOrderRequest request, CancellationToken ct)
    {
        if (request.CustomerId <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || request.IdempotencyKey.Length > 100 || string.IsNullOrWhiteSpace(request.RequestHash)
            || request.RequestHash.Length != 64 || request.Items is null || request.Items.Count is < 1 or > 50
            || request.Items.Any(x => x.DishId <= 0 || x.Quantity is < 1 or > 50))
            return BadRequest(new { code = "validation_failed", message = "Đơn gọi món không hợp lệ." });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var key = request.IdempotencyKey.Trim();
        var replay = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.SubmitIdempotencyKey == key, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.SubmitRequestHash, request.RequestHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với đơn gọi món khác." });
            return Ok(await MapOrder(replay, ct));
        }

        var session = await _db.DiningSessions.FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        var sessionError = ValidateWritableSession(session, request.CustomerId);
        if (sessionError is not null) return sessionError;

        var dishIds = request.Items.Select(x => x.DishId).Distinct().ToArray();
        var dishes = await _catalog.GetDishesAsync(dishIds, ct) ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var dishLookup = dishes.ToDictionary(x => x.DishId);
        if (dishIds.Any(id => !dishLookup.TryGetValue(id, out var dish) || !dish.IsActive || !dish.Available))
            return UnprocessableEntity(new { code = "dish_unavailable", message = "Một hoặc nhiều món không còn phục vụ." });

        var confirmedStatusId = await _db.OrderStatus.Where(x => x.StatusCode == "CONFIRMED")
            .Select(x => x.StatusID).FirstAsync(ct);
        var now = DateTime.UtcNow;
        var order = new OrderEntity
        {
            DiningSessionCode = session!.SessionCode,
            SubmitIdempotencyKey = key,
            SubmitRequestHash = request.RequestHash,
            OrderCode = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..34].ToUpperInvariant(),
            OrderTime = now,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            IsActive = true,
            TableID = session.TableId,
            CustomerID = session.CustomerId,
            StatusID = confirmedStatusId
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        _db.OrderItems.AddRange(request.Items.Select(item =>
        {
            var dish = dishLookup[item.DishId];
            return new OrderItems
            {
                OrderID = order.OrderID,
                DishID = item.DishId,
                Quantity = item.Quantity,
                UnitPrice = dish.Price,
                LineTotal = dish.Price * item.Quantity,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                StatusCode = "CONFIRMED"
            };
        }));
        session.UpdatedAtUtc = now;
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return Conflict(new { code = "command_in_progress", message = "Yêu cầu đang được xử lý. Vui lòng thử lại." });
        }
        // These snapshots already validated this exact new order before commit.
        // Avoid a second remote Catalog refresh and duplicate snapshot write just
        // to render its response (important with cloud database round trips).
        return Ok(await MapOrder(order, ct, session, dishes, "CONFIRMED"));
    }

    [HttpGet("mobile-orders")]
    public async Task<ActionResult<IReadOnlyList<MobileOrderResponse>>> ListOrders(
        [FromQuery] int customerId, [FromQuery] string? scope, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking().Where(x => x.CustomerID == customerId);
        if (string.Equals(scope, "active", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsActive ?? true);
        if (string.Equals(scope, "history", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !(x.IsActive ?? true));
        var orders = await query.OrderByDescending(x => x.OrderTime).Take(100).ToListAsync(ct);
        var result = new List<MobileOrderResponse>(orders.Count);
        var sessionCodes = orders.Select(x => x.DiningSessionCode).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct().ToArray();
        var sessions = sessionCodes.Length == 0
            ? new Dictionary<string, DiningSession>(StringComparer.Ordinal)
            : await _db.DiningSessions.AsNoTracking().Where(x => sessionCodes.Contains(x.SessionCode))
                .ToDictionaryAsync(x => x.SessionCode, StringComparer.Ordinal, ct);
        var orderIds = orders.Select(x => x.OrderID).ToArray();
        var statusIds = orders.Select(x => x.StatusID).Distinct().ToArray();
        var statuses = await _db.OrderStatus.AsNoTracking().Where(x => statusIds.Contains(x.StatusID))
            .ToDictionaryAsync(x => x.StatusID, x => x.StatusCode, ct);
        var rawItems = await _db.OrderItems.AsNoTracking().Where(x => orderIds.Contains(x.OrderID))
            .OrderBy(x => x.ItemID).ToListAsync(ct);
        var itemsByOrder = rawItems.ToLookup(x => x.OrderID);
        // Refresh the owner-provided dish snapshot once for the whole bounded page.
        var dishes = rawItems.Count == 0 ? Array.Empty<CatalogApiClient.DishSnapshotResponse>()
            : await _catalog.GetDishesAsync(rawItems.Select(x => x.DishID).Distinct(), ct)
                ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        foreach (var order in orders)
        {
            var session = string.IsNullOrWhiteSpace(order.DiningSessionCode)
                ? null : sessions.GetValueOrDefault(order.DiningSessionCode);
            result.Add(await MapOrder(order, ct, session, dishes,
                statuses.GetValueOrDefault(order.StatusID) ?? "PENDING", itemsByOrder[order.OrderID].ToArray(), true));
        }
        return Ok(result);
    }

    [HttpGet("mobile-orders/{orderId:int}")]
    public async Task<ActionResult<MobileOrderResponse>> GetOrder(int orderId, [FromQuery] int customerId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderID == orderId, ct);
        if (order is null) return NotFound(new { code = "order_not_found", message = "Không tìm thấy đơn hàng." });
        if (order.CustomerID != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đơn hàng này." });
        return Ok(await MapOrder(order, ct));
    }

    [HttpGet("mobile-orders/{orderId:int}/tracking")]
    public async Task<ActionResult<MobileOrderTrackingResponse>> TrackOrder(int orderId, [FromQuery] int customerId, CancellationToken ct)
    {
        var orderResult = await GetOrder(orderId, customerId, ct);
        if (orderResult.Result is ObjectResult error && error.StatusCode is >= 400) return error;
        var order = orderResult.Value ?? (orderResult.Result as OkObjectResult)?.Value as MobileOrderResponse;
        if (order is null) return NotFound(new { code = "order_not_found", message = "Không tìm thấy đơn hàng." });
        return Ok(new MobileOrderTrackingResponse(order,
            order.Items.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity)), DateTime.UtcNow));
    }

    [HttpPost("{sessionId:int}/service-requests")]
    public async Task<ActionResult<ServiceRequestResponse>> CreateServiceRequest(int sessionId, CreateServiceRequestRequest request, CancellationToken ct)
    {
        var normalizedType = request.RequestType?.Trim().ToUpperInvariant();
        if (request.CustomerId <= 0 || normalizedType is not ("CALL_STAFF" or "WATER" or "UTENSILS" or "OTHER")
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 140
            || string.IsNullOrWhiteSpace(request.RequestHash) || request.RequestHash.Length != 64
            || request.Note?.Length > 500)
            return BadRequest(new { code = "validation_failed", message = "Yêu cầu phục vụ không hợp lệ." });
        var key = request.IdempotencyKey.Trim();
        var replay = await _db.ServiceRequests.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, request.RequestHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với yêu cầu khác." });
            return Ok(MapServiceRequest(replay));
        }
        var session = await _db.DiningSessions.FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        var sessionError = ValidateWritableSession(session, request.CustomerId);
        if (sessionError is not null) return sessionError;
        var now = DateTime.UtcNow;
        var entity = new ServiceRequest
        {
            DiningSessionId = sessionId, CustomerId = request.CustomerId, BranchId = session!.BranchId,
            TableId = session.TableId, RequestType = normalizedType, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = "OPEN", IdempotencyKey = key, RequestHash = request.RequestHash, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _db.ServiceRequests.Add(entity);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { code = "command_in_progress", message = "Yêu cầu đang được xử lý." }); }
        return Ok(MapServiceRequest(entity));
    }

    [HttpGet("{sessionId:int}/service-requests")]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestResponse>>> ListServiceRequests(
        int sessionId, [FromQuery] int customerId, CancellationToken ct)
    {
        var session = await _db.DiningSessions.AsNoTracking().FirstOrDefaultAsync(x => x.DiningSessionId == sessionId, ct);
        if (session is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (session.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        var rows = await _db.ServiceRequests.AsNoTracking().Where(x => x.DiningSessionId == sessionId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return Ok(rows.Select(MapServiceRequest).ToArray());
    }

    [HttpPost("service-requests/{requestId:int}/cancel")]
    public async Task<ActionResult<ServiceRequestResponse>> CancelServiceRequest(
        int requestId, [FromBody] CustomerSessionRequest request, CancellationToken ct)
    {
        var entity = await _db.ServiceRequests.FirstOrDefaultAsync(x => x.ServiceRequestId == requestId, ct);
        if (entity is null) return NotFound(new { code = "service_request_not_found", message = "Không tìm thấy yêu cầu phục vụ." });
        if (entity.CustomerId != request.CustomerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu yêu cầu này." });
        if (string.Equals(entity.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return Ok(MapServiceRequest(entity));
        if (!ServiceRequestStateMachine.CanTransition(entity.Status, ServiceRequestStateMachine.Cancelled))
            return Conflict(new { code = "service_request_state_conflict", message = "Yêu cầu đang được xử lý và không thể hủy." });
        entity.Status = "CANCELLED";
        entity.CancelledAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = entity.CancelledAtUtc.Value;
        await _db.SaveChangesAsync(ct);
        return Ok(MapServiceRequest(entity));
    }

    private ObjectResult? ValidateWritableSession(DiningSession? session, int customerId)
    {
        if (session is null) return NotFound(new { code = "session_not_found", message = "Không tìm thấy phiên phục vụ." });
        if (session.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu phiên phục vụ này." });
        if (session.EndedAtUtc is not null) return Conflict(new { code = "session_closed", message = "Phiên phục vụ đã kết thúc." });
        if (string.Equals(session.Status, "PAYMENT_REQUESTED", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { code = "session_payment_locked", message = "Không thể tạo yêu cầu mới khi đang thanh toán." });
        return null;
    }

    private async Task<MobileOrderResponse> MapOrder(OrderEntity order, CancellationToken ct, DiningSession? session = null,
        IReadOnlyList<CatalogApiClient.DishSnapshotResponse>? validatedDishes = null, string? knownStatus = null,
        IReadOnlyList<OrderItems>? knownItems = null, bool sessionResolved = false)
    {
        var status = knownStatus ?? await _db.OrderStatus.AsNoTracking().Where(x => x.StatusID == order.StatusID)
            .Select(x => x.StatusCode).FirstOrDefaultAsync(ct) ?? "PENDING";
        var rawItems = knownItems ?? await _db.OrderItems.AsNoTracking().Where(x => x.OrderID == order.OrderID)
            .OrderBy(x => x.ItemID).ToListAsync(ct);
        var dishes = validatedDishes ?? await _catalog.GetDishesAsync(rawItems.Select(x => x.DishID).Distinct(), ct)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var names = dishes.ToDictionary(x => x.DishId, x => x.Name);
        var items = rawItems.Select(x => new MobileOrderItemResponse(x.ItemID, x.DishID,
            names.GetValueOrDefault(x.DishID) ?? $"Món #{x.DishID}", x.Quantity, x.UnitPrice, x.LineTotal, x.Note, x.StatusCode)).ToArray();
        if (!sessionResolved) session ??= await _db.DiningSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionCode == order.DiningSessionCode, ct);
        var sessionSummary = session is null ? null : new DiningSessionSummaryResponse(session.DiningSessionId,
            session.SessionCode, session.BranchId, session.TableId, session.Status, session.StartedAtUtc, session.EndedAtUtc);
        return new MobileOrderResponse(order.OrderID, order.OrderCode, session?.DiningSessionId, order.DiningSessionCode,
            order.TableID, status, order.OrderTime, order.CompletedTime, items.Sum(x => x.LineTotal), items, sessionSummary);
    }

    private static ServiceRequestResponse MapServiceRequest(ServiceRequest x) => new(x.ServiceRequestId,
        x.DiningSessionId, x.RequestType, x.Note, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.CancelledAtUtc,
        x.BranchId, x.TableId, x.CustomerId);

    private static DiningSessionResponse Map(DiningSession x, bool alreadyCheckedIn) => new(
        x.DiningSessionId, x.SessionCode, x.CustomerId, x.BranchId, x.TableId, x.ReservationId,
        x.Status, x.ConvertedOrderId, x.StartedAtUtc, x.PaymentRequestedAtUtc, x.EndedAtUtc, alreadyCheckedIn);

    public sealed record CreateDiningSessionRequest(int CustomerId, int BranchId, int TableId, int? ReservationId,
        string IdempotencyKey, string RequestHash);
    public sealed record CustomerSessionRequest(int CustomerId);
    public sealed record PaymentSessionRequest(int CustomerId, int CheckoutQuoteId);
    public sealed record CompletePaymentRequest(int CustomerId, int CheckoutQuoteId, int PaymentId);
    public sealed record CheckoutItem(int ItemId, int OrderId, int DishId, string DishName, int Quantity, decimal UnitPrice, decimal LineTotal);
    public sealed record DiningSessionCheckoutContext(int DiningSessionId, string DiningSessionCode, int CustomerId,
        int BranchId, int TableId, string Status, IReadOnlyList<int> OrderIds, decimal Subtotal, IReadOnlyList<CheckoutItem> Items);
    public sealed record DiningSessionResponse(int DiningSessionId, string SessionCode, int CustomerId, int BranchId,
        int TableId, int? ReservationId, string Status, int? ConvertedOrderId, DateTime StartedAtUtc,
        DateTime? PaymentRequestedAtUtc, DateTime? EndedAtUtc, bool AlreadyCheckedIn);
    public sealed record MobileOrderItemRequest(int DishId, int Quantity, string? Note);
    public sealed record CreateMobileOrderRequest(int CustomerId, string IdempotencyKey, string RequestHash,
        IReadOnlyList<MobileOrderItemRequest>? Items, string? Note);
    public sealed record MobileOrderItemResponse(int ItemId, int DishId, string DishName, int Quantity,
        decimal UnitPrice, decimal LineTotal, string? Note, string Status);
    public sealed record DiningSessionSummaryResponse(int DiningSessionId, string SessionCode, int BranchId,
        int TableId, string Status, DateTime StartedAtUtc, DateTime? EndedAtUtc);
    public sealed record MobileOrderResponse(int OrderId, string? OrderCode, int? DiningSessionId,
        string? DiningSessionCode, int? TableId, string Status, DateTime OrderTime, DateTime? CompletedTime,
        decimal Subtotal, IReadOnlyList<MobileOrderItemResponse> Items, DiningSessionSummaryResponse? DiningSession = null);
    public sealed record MobileOrderTrackingResponse(MobileOrderResponse Order,
        IReadOnlyDictionary<string, int> StatusCounts, DateTime RefreshedAtUtc);
    public sealed record CreateServiceRequestRequest(int CustomerId, string? RequestType, string? Note,
        string IdempotencyKey, string RequestHash);
    public sealed record ServiceRequestResponse(int ServiceRequestId, int DiningSessionId, string RequestType,
        string? Note, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? CancelledAtUtc,
        int BranchId, int TableId, int CustomerId);
}
