using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SelfRestaurant.Orders.Api.Infrastructure.Auditing;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Infrastructure.Eventing;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using OrderEntity = SelfRestaurant.Orders.Api.Persistence.Entities.Orders;

namespace SelfRestaurant.Orders.Api.Controllers;

[ApiController]
public sealed class OrdersController : ControllerBase
{
    private static readonly string[] ActiveCashierStatuses = ["PENDING", "CONFIRMED", "PREPARING", "READY", "SERVING"];
    private static readonly string[] ActiveDiningStatuses = ["PENDING", "CONFIRMED", "PREPARING", "READY", "SERVING"];
    private static readonly TimeSpan SubmitCommandWait = TimeSpan.FromSeconds(5);
    private const int TableLockTimeoutMs = 5000;

    private readonly OrdersDbContext _db;
    private readonly ICatalogReadModel _catalogApi;
    private readonly ICustomerLoyaltyReadModel _customersApi;
    private readonly BillingCheckoutGuardClient _billingGuard;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly BusinessAuditLogger _auditLogger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrdersDbContext db, ICatalogReadModel catalogApi, ICustomerLoyaltyReadModel customersApi, BillingCheckoutGuardClient billingGuard, IIntegrationEventPublisher eventPublisher, BusinessAuditLogger auditLogger, IHostEnvironment environment, IConfiguration configuration, ILogger<OrdersController> logger)
    {
        _db = db;
        _catalogApi = catalogApi;
        _customersApi = customersApi;
        _billingGuard = billingGuard;
        _eventPublisher = eventPublisher;
        _auditLogger = auditLogger;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("api/tables/{tableId:int}/occupy")]
    public async Task<ActionResult> OccupyTable(int tableId, CancellationToken cancellationToken)
    {
        var tableSnapshot = await _catalogApi.GetTableAsync(tableId, cancellationToken);
        if (tableSnapshot is null)
        {
            return NotFound();
        }

        await _catalogApi.OccupyTableAsync(tableId, currentOrderId: null, cancellationToken);
        return NoContent();
    }

    [HttpPost("api/tables/{tableId:int}/reset")]
    public async Task<ActionResult> ResetTable(int tableId, CancellationToken cancellationToken)
    {
        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var tableSnapshot = await _catalogApi.GetTableAsync(tableId, cancellationToken);
        if (tableSnapshot is null)
        {
            return NotFound();
        }

        var activeOrders = await _db.Orders
            .Where(x => x.TableID == tableId && (x.IsActive ?? true))
            .OrderByDescending(x => x.OrderTime)
            .ToListAsync(cancellationToken);

        if (activeOrders.Count > 0)
        {
            var statusIds = activeOrders.Select(x => x.StatusID).Distinct().ToArray();
            var statusLookup = await _db.OrderStatus
                .Where(x => statusIds.Contains(x.StatusID))
                .ToDictionaryAsync(x => x.StatusID, x => x.StatusCode, cancellationToken);

            var nonPendingOrders = activeOrders
                .Where(order => !string.Equals(statusLookup.GetValueOrDefault(order.StatusID), "PENDING", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonPendingOrders.Count > 0)
            {
                return Conflict("Bàn đang có phiên phục vụ đang diễn ra, không thể reset.");
            }

            var pendingOrderIds = activeOrders.Select(x => x.OrderID).ToArray();
            var pendingItems = await _db.OrderItems
                .Where(x => pendingOrderIds.Contains(x.OrderID))
                .ToListAsync(cancellationToken);

            if (pendingItems.Count > 0)
            {
                _db.OrderItems.RemoveRange(pendingItems);
            }

            _db.Orders.RemoveRange(activeOrders);
            _auditLogger.Add(
                actionType: "TABLE_RESET",
                entityType: "TABLE",
                entityId: tableId.ToString(),
                tableId: tableId,
                beforeState: new
                {
                    activeOrderIds = activeOrders.Select(x => x.OrderID).ToArray(),
                    pendingItemCount = pendingItems.Count
                },
                afterState: new
                {
                    activeOrderIds = Array.Empty<int>(),
                    released = true
                },
                notes: "Developer-safe reset removed only pending rounds on an idle table.");
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Only fully release the table when no in-flight dining session remains.
        await _catalogApi.ReleaseTableAsync(tableId, cancellationToken);
        await tableLock.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/tables/reset-all")]
    public async Task<ActionResult<object>> ResetAllTables(CancellationToken cancellationToken)
    {
        var resetTables = await _catalogApi.ResetAllTablesAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            resetTables
        });
    }

    [HttpPost("api/internal/orders/dining-sessions/{diningSessionCode}/tables")]
    [HttpPost("internal/orders/dining-sessions/{diningSessionCode}/tables")]
    public async Task<ActionResult<object>> LinkDiningSessionTables(
        string diningSessionCode,
        [FromBody] LinkDiningSessionTablesRequest request,
        CancellationToken cancellationToken)
    {
        var sessionCode = NormalizeSessionCode(diningSessionCode);
        if (string.IsNullOrWhiteSpace(sessionCode))
        {
            return BadRequest(new { message = "Phiên phục vụ không hợp lệ." });
        }
        if (request is null)
        {
            return BadRequest(new { message = "Thiếu danh sách bàn cần ghép phiên." });
        }

        var tableIds = NormalizeTableIds(request.TableIds);
        var primaryTableId = request.PrimaryTableId is > 0
            ? request.PrimaryTableId.Value
            : tableIds.FirstOrDefault();
        tableIds = NormalizeTableIds([.. tableIds, primaryTableId]);
        if (primaryTableId <= 0)
        {
            return BadRequest(new { message = "Cần chọn ít nhất một bàn để ghép phiên." });
        }

        if (!await _db.Orders.AnyAsync(x => x.DiningSessionCode == sessionCode && (x.IsActive ?? true), cancellationToken))
        {
            return NotFound(new { message = "Không tìm thấy phiên phục vụ đang hoạt động." });
        }

        await LinkTablesToSessionAsync(sessionCode, primaryTableId, tableIds, cancellationToken);
        foreach (var tableId in tableIds)
        {
            await _catalogApi.OccupyTableAsync(tableId, currentOrderId: null, cancellationToken);
        }

        var linkedTables = await _db.DiningSessionTables
            .Where(x => x.DiningSessionCode == sessionCode)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.TableID)
            .Select(x => new { tableId = x.TableID, isPrimary = x.IsPrimary })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            diningSessionCode = sessionCode,
            primaryTableId,
            tableIds = linkedTables.Select(x => x.tableId).ToArray(),
            tables = linkedTables
        });
    }

    [HttpPost("api/dev/reset-test-state")]
    public async Task<ActionResult<object>> ResetDevTestState(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var orderItems = await _db.OrderItems.ToListAsync(cancellationToken);
        var orders = await _db.Orders.ToListAsync(cancellationToken);
        var outboxEvents = await _db.OutboxEvents.ToListAsync(cancellationToken);
        var inboxEvents = await _db.InboxEvents.ToListAsync(cancellationToken);
        var submitCommands = await _db.SubmitCommands.ToListAsync(cancellationToken);
        var diningSessionTables = await _db.DiningSessionTables.ToListAsync(cancellationToken);
        var diningSessions = await _db.DiningSessions.ToListAsync(cancellationToken);
        var preOrderItems = await _db.ReservationPreOrderItems.ToListAsync(cancellationToken);
        var preOrders = await _db.ReservationPreOrders.ToListAsync(cancellationToken);
        var serviceRequests = await _db.ServiceRequests.ToListAsync(cancellationToken);

        if (serviceRequests.Count > 0) _db.ServiceRequests.RemoveRange(serviceRequests);
        if (diningSessionTables.Count > 0) _db.DiningSessionTables.RemoveRange(diningSessionTables);
        if (diningSessions.Count > 0) _db.DiningSessions.RemoveRange(diningSessions);
        if (preOrderItems.Count > 0) _db.ReservationPreOrderItems.RemoveRange(preOrderItems);
        if (preOrders.Count > 0) _db.ReservationPreOrders.RemoveRange(preOrders);

        if (orderItems.Count > 0)
        {
            _db.OrderItems.RemoveRange(orderItems);
        }

        if (orders.Count > 0)
        {
            _db.Orders.RemoveRange(orders);
        }

        if (outboxEvents.Count > 0)
        {
            _db.OutboxEvents.RemoveRange(outboxEvents);
        }

        if (inboxEvents.Count > 0)
        {
            _db.InboxEvents.RemoveRange(inboxEvents);
        }

        if (submitCommands.Count > 0)
        {
            _db.SubmitCommands.RemoveRange(submitCommands);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            clearedOrders = orders.Count,
            clearedOrderItems = orderItems.Count,
            clearedOutboxEvents = outboxEvents.Count,
            clearedInboxEvents = inboxEvents.Count,
            clearedSubmitCommands = submitCommands.Count
        });
    }

    [HttpGet("api/tables/{tableId:int}/order")]
    public async Task<ActionResult<object>> GetActiveOrder(int tableId, CancellationToken cancellationToken)
    {
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(await BuildActiveOrderResponseAsync(order, cancellationToken));
    }

    [HttpGet("api/orders/{orderId:int}")]
    public async Task<ActionResult<object>> GetOrderById(int orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderID == orderId, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return Ok(await BuildActiveOrderResponseAsync(order, cancellationToken));
    }

    [HttpGet("api/internal/audit-logs")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? orderId,
        [FromQuery] int? tableId,
        [FromQuery] int? dishId,
        [FromQuery] int? billId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.BusinessAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var key = entityType.Trim();
            query = query.Where(x => x.EntityType == key);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            var key = entityId.Trim();
            query = query.Where(x => x.EntityId == key);
        }

        if (orderId is > 0)
        {
            query = query.Where(x => x.OrderId == orderId.Value);
        }

        if (tableId is > 0)
        {
            query = query.Where(x => x.TableId == tableId.Value);
        }

        if (dishId is > 0)
        {
            query = query.Where(x => x.DishId == dishId.Value);
        }

        if (billId is > 0)
        {
            query = query.Where(x => x.BillId == billId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new
            {
                auditId = x.BusinessAuditLogId,
                timestampUtc = x.CreatedAtUtc,
                actorType = x.ActorType,
                actorId = x.ActorId,
                actorCode = x.ActorCode,
                actorName = x.ActorName,
                actorRoleCode = x.ActorRoleCode,
                actionType = x.ActionType,
                entityType = x.EntityType,
                entityId = x.EntityId,
                tableId = x.TableId,
                orderId = x.OrderId,
                orderItemId = x.OrderItemId,
                dishId = x.DishId,
                billId = x.BillId,
                diningSessionCode = x.DiningSessionCode,
                correlationId = x.CorrelationId,
                idempotencyKey = x.IdempotencyKey,
                notes = x.Notes,
                beforeState = x.BeforeState,
                afterState = x.AfterState
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("api/internal/customers/{customerId:int}/order-history")]
    public async Task<ActionResult<object>> GetCustomerOrderHistory(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        var fromDate = DateTime.Today.AddDays(-days);

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o => o.CustomerID == customerId && o.OrderTime >= fromDate)
            .OrderByDescending(o => o.OrderTime);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                orderTime = o.OrderTime,
                completedTime = o.CompletedTime,
                tableId = o.TableID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return Ok(new { page, pageSize, totalItems, totalPages, items = Array.Empty<object>() });
        }

        var orderIds = orders.Select(o => o.orderId).ToArray();

        var aggregates = await _db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderID))
            .Where(i => i.StatusCode != "CANCELLED")
            .GroupBy(i => i.OrderID)
            .Select(g => new
            {
                orderId = g.Key,
                totalAmount = g.Sum(x => x.LineTotal),
                itemCount = g.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        var aggregateLookup = aggregates.ToDictionary(
            x => x.orderId,
            x => new { x.totalAmount, x.itemCount });

        // Get table names
        var tableIds = orders.Where(o => o.tableId.HasValue).Select(o => o.tableId!.Value).Distinct().ToList();
        var tables = await _catalogApi.GetTablesAsync(tableIds, cancellationToken) ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>();
        var tableLookup = tables.ToDictionary(t => t.TableId);

        // Get dish details for each order
        var orderItems = await _db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderID))
            .Select(i => new
            {
                orderId = i.OrderID,
                dishId = i.DishID,
                quantity = i.Quantity,
                statusCode = i.StatusCode
            })
            .ToListAsync(cancellationToken);

        var dishIds = orderItems.Select(i => i.dishId).Distinct().ToList();
        var dishes = await _catalogApi.GetDishesAsync(dishIds, cancellationToken) ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var dishLookup = dishes.ToDictionary(d => d.DishId);

        var items = orders.Select(o =>
        {
            var totals = aggregateLookup.GetValueOrDefault(o.orderId);
            var orderDishes = orderItems.Where(i => i.orderId == o.orderId && i.statusCode != "CANCELLED").ToList();
            var dishesSummary = string.Join(", ", orderDishes.Select(i =>
            {
                var dishName = dishLookup.TryGetValue(i.dishId, out var dish) ? dish.Name : $"Món #{i.dishId}";
                return $"{i.quantity}x {dishName}";
            }));

            var tableName = o.tableId.HasValue && tableLookup.TryGetValue(o.tableId.Value, out var table)
                ? $"Bàn {table.TableNumber}"
                : "-";

            return new
            {
                o.orderId,
                o.orderCode,
                o.orderTime,
                o.completedTime,
                tableName,
                o.statusCode,
                statusName = o.statusName,
                totalAmount = totals?.totalAmount ?? 0m,
                itemCount = totals?.itemCount ?? 0,
                dishesSummary
            };
        }).ToList();

        return Ok(new { page, pageSize, totalItems, totalPages, items });
    }

    [HttpGet("api/internal/customers/{customerId:int}/orders")]
    public async Task<ActionResult<object>> GetCustomerOrders(
        int customerId,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        // This is the old endpoint for customer dashboard - keep it for backward compatibility
        take = Math.Clamp(take, 1, 50);

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o => o.CustomerID == customerId)
            .OrderByDescending(o => o.OrderTime)
            .Take(take)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                orderTime = o.OrderTime,
                tableId = o.TableID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var orderIds = orders.Select(o => o.orderId).ToArray();

        var aggregates = await _db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderID))
            .Where(i => i.StatusCode != "CANCELLED")
            .GroupBy(i => i.OrderID)
            .Select(g => new
            {
                orderId = g.Key,
                totalAmount = g.Sum(x => x.LineTotal),
                itemCount = g.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        var aggregateLookup = aggregates.ToDictionary(
            x => x.orderId,
            x => new { x.totalAmount, x.itemCount });

        var tableIds = orders.Where(o => o.tableId.HasValue).Select(o => o.tableId!.Value).Distinct().ToList();
        var tables = await _catalogApi.GetTablesAsync(tableIds, cancellationToken) ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>();
        var tableLookup = tables.ToDictionary(t => t.TableId);

        var items = orders.Select(o =>
        {
            var totals = aggregateLookup.GetValueOrDefault(o.orderId);
            var tableName = o.tableId.HasValue && tableLookup.TryGetValue(o.tableId.Value, out var table)
                ? $"Bàn {table.TableNumber}"
                : null;

            return new
            {
                o.orderId,
                o.orderCode,
                o.orderTime,
                tableName,
                o.statusCode,
                statusName = o.statusName,
                totalAmount = totals?.totalAmount ?? 0m,
                itemCount = totals?.itemCount ?? 0
            };
        }).ToList();

        return Ok(items);
    }

    [HttpGet("api/internal/customers/{customerId:int}/active-order-context")]
    public async Task<ActionResult<object>> GetCustomerActiveOrderContext(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return BadRequest();
        }

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o =>
                o.CustomerID == customerId
                && o.TableID.HasValue
                && (o.IsActive ?? true)
                && ActiveCashierStatuses.Contains(o.Status.StatusCode))
            .OrderByDescending(o => o.OrderTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null || !order.TableID.HasValue)
        {
            return NotFound();
        }

        var table = await _catalogApi.GetTableAsync(order.TableID.Value, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        var branch = (await _catalogApi.GetBranchesAsync([table.BranchId], cancellationToken))
            ?.FirstOrDefault(x => x.BranchId == table.BranchId);

        return Ok(new
        {
            orderId = order.OrderID,
            orderCode = order.OrderCode,
            orderTime = order.OrderTime,
            statusCode = order.Status.StatusCode,
            orderStatus = order.Status.StatusName,
            diningSessionCode = order.DiningSessionCode,
            tableId = table.TableId,
            branchId = table.BranchId,
            branchName = branch?.Name,
            tableNumber = table.TableNumber,
        });
    }

    [HttpGet("api/internal/dishes/{dishId:int}/references")]
    public async Task<ActionResult<object>> GetDishReferences(int dishId, CancellationToken cancellationToken = default)
    {
        if (dishId <= 0)
        {
            return BadRequest(new { message = "Món ăn không hợp lệ." });
        }

        var orderItemCount = await _db.OrderItems
            .AsNoTracking()
            .CountAsync(x => x.DishID == dishId, cancellationToken);

        return Ok(new
        {
            dishId,
            hasHistory = orderItemCount > 0,
            orderItemCount
        });
    }

    [HttpGet("api/tables/{tableId:int}/orders/active")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetActiveOrders(int tableId, CancellationToken cancellationToken)
    {
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return Ok(Array.Empty<object>());
        }

        return Ok(new[] { await BuildActiveOrderResponseAsync(order, cancellationToken) });
    }

    [HttpPost("api/tables/{tableId:int}/order/items")]
    public async Task<ActionResult<object>> AddItem(int tableId, [FromBody] AddItemRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest("Số lượng phải lớn hơn 0.");
        }
        var dishSnapshot = await _catalogApi.GetDishAsync(request.DishId, cancellationToken);
        if (dishSnapshot is null)
        {
            return NotFound("Không tìm thấy món ăn.");
        }
        if (!IsDishOrderable(dishSnapshot))
        {
            return BuildDishUnavailableConflict([dishSnapshot.Name]);
        }

        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var activeSessionCode = await GetLatestActiveSessionCodeAsync(tableId, cancellationToken);
        var sessionCheck = await EnsureSessionWritableAsync(null, activeSessionCode, request.ExpectedDiningSessionCode, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: true, cancellationToken);
        if (order is null)
        {
            return NotFound("Không tìm thấy bàn.");
        }

        await UpsertOrderItemAsync(order.OrderID, dishSnapshot, request, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tableLock.CommitAsync(cancellationToken);
        return Ok(await BuildActiveOrderResponseAsync(order, cancellationToken));
    }

    [HttpPut("api/tables/{tableId:int}/order/items/{itemId:int}")]
    public async Task<ActionResult> UpdateQuantity(int tableId, int itemId, [FromBody] UpdateQuantityRequest request, CancellationToken cancellationToken)
    {
        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var item = await _db.OrderItems.FirstOrDefaultAsync(x => x.ItemID == itemId && x.OrderID == order.OrderID, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var statusCode = await GetOrderStatusCodeAsync(order.StatusID, cancellationToken);
        if (!string.Equals(statusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Không thể cập nhật món đã gửi bếp");
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        if (request.Quantity <= 0)
        {
            return BadRequest("Số lượng phải lớn hơn 0");
        }

        item.Quantity = request.Quantity;
        item.LineTotal = item.UnitPrice * item.Quantity;

        await _db.SaveChangesAsync(cancellationToken);
        await tableLock.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("api/tables/{tableId:int}/order/items/{itemId:int}")]
    public async Task<ActionResult> RemoveItem(int tableId, int itemId, CancellationToken cancellationToken)
    {
        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var item = await _db.OrderItems.FirstOrDefaultAsync(x => x.ItemID == itemId && x.OrderID == order.OrderID, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var statusCode = await GetOrderStatusCodeAsync(order.StatusID, cancellationToken);
        if (!string.Equals(statusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Không thể xóa món đã gửi bếp");
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var itemCountBeforeDelete = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderID == order.OrderID)
            .CountAsync(cancellationToken);

        _db.OrderItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        if (itemCountBeforeDelete <= 1)
        {
            order.IsActive = false;
            order.CompletedTime ??= DateTime.Now;

            if (order.TableID is int tableIdValue
                && !await HasOtherActiveOrdersInSameSessionAsync(order, cancellationToken))
            {
                await _catalogApi.ReleaseTableAsync(tableIdValue, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        await tableLock.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("api/tables/{tableId:int}/order/items/{itemId:int}/note")]
    public async Task<ActionResult> UpdateItemNote(
        int tableId,
        int itemId,
        [FromBody] UpdateItemNoteRequest request,
        CancellationToken cancellationToken)
    {
        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var statusCode = await _db.OrderStatus
            .Where(s => s.StatusID == order.StatusID)
            .Select(s => s.StatusCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.Equals(statusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Chỉ có thể sửa ghi chú khi món còn ở trạng thái chờ gửi bếp.");
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var item = await _db.OrderItems
            .FirstOrDefaultAsync(x => x.ItemID == itemId && x.OrderID == order.OrderID, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Note = NormalizeNote(request.Note);
        await _db.SaveChangesAsync(cancellationToken);
        await tableLock.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/tables/{tableId:int}/order/submit")]
    public async Task<ActionResult> SubmitOrder(int tableId, [FromBody] SubmitOrderRequest? request, CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request?.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest("Thiếu khóa xác nhận gửi món.");
        }

        var replay = await WaitForSubmitCommandCompletionAsync(idempotencyKey, tableId, cancellationToken);
        if (replay is not null)
        {
            return NoContent();
        }

        SubmitCommands? command = null;
        try
        {
            command = await CreateSubmitCommandAsync(idempotencyKey, tableId, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var duplicateReplay = await WaitForSubmitCommandCompletionAsync(idempotencyKey, tableId, cancellationToken);
            if (duplicateReplay is not null)
            {
                return NoContent();
            }

            return Conflict("Yêu cầu gửi món đang được xử lý. Vui lòng thử lại sau.");
        }

        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            await MarkSubmitCommandFailedAsync(command, "Không tìm thấy bàn hoặc đơn chờ gửi.", cancellationToken);
            return NotFound();
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, request?.ExpectedDiningSessionCode, cancellationToken);
        if (sessionCheck is not null)
        {
            await MarkSubmitCommandFailedAsync(command, "Phiên bàn đã thay đổi hoặc đang được thanh toán.", cancellationToken);
            return sessionCheck;
        }

        var statusCode = await GetOrderStatusCodeAsync(order.StatusID, cancellationToken);
        if (!string.Equals(statusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(order.SubmitIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                await MarkSubmitCommandCompletedAsync(command, order, cancellationToken);
                return NoContent();
            }

            await MarkSubmitCommandFailedAsync(command, "Đơn hàng đã được gửi.", cancellationToken);
            return BadRequest("Đơn hàng đã được gửi");
        }

        var itemCount = await _db.OrderItems.CountAsync(x => x.OrderID == order.OrderID, cancellationToken);
        if (itemCount == 0)
        {
            await MarkSubmitCommandFailedAsync(command, "Đơn hàng trống.", cancellationToken);
            return BadRequest("Đơn hàng trống");
        }

        var availabilityConflict = await ValidateOrderDishAvailabilityAsync(order.OrderID, cancellationToken);
        if (availabilityConflict is not null)
        {
            await MarkSubmitCommandFailedAsync(command, "Giỏ hàng có món không còn khả dụng.", cancellationToken);
            return availabilityConflict;
        }

        var pendingInventoryCheck = await ValidateIngredientAvailabilityAsync(
            order.OrderID,
            await _db.OrderItems
                .AsNoTracking()
                .Where(x => x.OrderID == order.OrderID)
                .Select(x => new CatalogApiClient.OrderIngredientConsumptionItem(x.DishID, x.Quantity, x.ItemID))
                .ToListAsync(cancellationToken),
            cancellationToken);
        if (pendingInventoryCheck is not null)
        {
            await MarkSubmitCommandFailedAsync(command, "Giỏ hàng vượt quá tồn kho nguyên liệu.", cancellationToken);
            return pendingInventoryCheck;
        }

        if (await GetOrderStatusIdAsync("CONFIRMED", cancellationToken) is not null)
        {
            if (string.IsNullOrWhiteSpace(order.DiningSessionCode))
            {
                order.DiningSessionCode = GenerateDiningSessionCode();
            }

            var pendingItems = await _db.OrderItems
                .Where(x => x.OrderID == order.OrderID && x.StatusCode == "PENDING")
                .ToListAsync(cancellationToken);

            foreach (var item in pendingItems)
            {
                item.StatusCode = "CONFIRMED";
            }

            // Frontend click locking helps, but the backend must own replay safety
            // so one intended submit cannot mint duplicate kitchen rounds.
            order.SubmitIdempotencyKey = idempotencyKey;
            await SyncOrderStateFromItemsAsync(order, cancellationToken);
            _auditLogger.Add(
                actionType: "ORDER_SUBMITTED",
                entityType: "ORDER",
                entityId: order.OrderID.ToString(),
                tableId: order.TableID,
                orderId: order.OrderID,
                diningSessionCode: order.DiningSessionCode,
                idempotencyKey: idempotencyKey,
                beforeState: new
                {
                    status = "PENDING",
                    itemStatus = "PENDING"
                },
                afterState: new
                {
                    status = order.StatusID,
                    itemStatus = "CONFIRMED",
                    itemCount = pendingItems.Count
                });
            await _db.SaveChangesAsync(cancellationToken);
            await MarkSubmitCommandCompletedAsync(command, order, cancellationToken);
            await PublishOrderEventAsync("order.submitted.v1", order, new
            {
                orderId = order.OrderID,
                orderCode = order.OrderCode,
                diningSessionCode = order.DiningSessionCode,
                tableId = order.TableID,
                customerId = order.CustomerID,
                statusCode = "CONFIRMED"
            }, cancellationToken);
        }

        await tableLock.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/tables/{tableId:int}/order/submit-batch")]
    public async Task<ActionResult<object>> SubmitOrderBatch(
        int tableId,
        [FromBody] SubmitOrderBatchRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest("Thiếu khóa xác nhận gửi món.");
        }

        var existingReplay = await WaitForSubmitCommandCompletionAsync(idempotencyKey, tableId, cancellationToken);
        if (existingReplay?.Result is OkObjectResult replayResult)
        {
            return replayResult;
        }

        SubmitCommands? command = null;
        try
        {
            command = await CreateSubmitCommandAsync(idempotencyKey, tableId, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var duplicateReplay = await WaitForSubmitCommandCompletionAsync(idempotencyKey, tableId, cancellationToken);
            if (duplicateReplay?.Result is OkObjectResult replay)
            {
                return replay;
            }

            return Conflict("Yêu cầu gửi món đang được xử lý. Vui lòng thử lại sau.");
        }

        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);

        var items = (request.Items ?? Array.Empty<AddItemRequest>())
            .Where(x => x.DishId > 0 && x.Quantity > 0)
            .ToList();

        if (items.Count == 0)
        {
            await MarkSubmitCommandFailedAsync(command, "Đơn hàng trống.", cancellationToken);
            return BadRequest("Đơn hàng trống");
        }

        var tableSnapshot = await _catalogApi.GetTableAsync(tableId, cancellationToken);
        if (tableSnapshot is null)
        {
            await MarkSubmitCommandFailedAsync(command, "Không tìm thấy bàn.", cancellationToken);
            return NotFound("Không tìm thấy bàn.");
        }

        var activeSessionCode = await GetLatestActiveSessionCodeAsync(tableId, cancellationToken);
        var sessionCheck = await EnsureSessionWritableAsync(null, activeSessionCode, request.ExpectedDiningSessionCode, cancellationToken);
        if (sessionCheck is not null)
        {
            await MarkSubmitCommandFailedAsync(command, "Phiên bàn đã thay đổi hoặc đang được thanh toán.", cancellationToken);
            return sessionCheck switch
            {
                ObjectResult objectResult => objectResult,
                _ => Conflict(new { message = "Phiên bàn không còn hợp lệ." })
            };
        }

        var existingOrder = await GetPendingRoundAsync(tableId, activeSessionCode, cancellationToken);

        if (existingOrder is not null)
        {
            var existingItems = await _db.OrderItems
                .Where(x => x.OrderID == existingOrder.OrderID)
                .ToListAsync(cancellationToken);

            if (existingItems.Count > 0)
            {
                _db.OrderItems.RemoveRange(existingItems);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var sessionCustomerId = activeSessionCode is null
            ? null
            : await GetSessionCustomerIdAsync(tableId, activeSessionCode, cancellationToken);

        var order = existingOrder ?? await CreatePendingOrderAsync(tableId, activeSessionCode, sessionCustomerId, cancellationToken);
        var dishLookup = ((await _catalogApi.GetDishesAsync(items.Select(x => x.DishId), cancellationToken))
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        foreach (var item in items)
        {
            if (!dishLookup.TryGetValue(item.DishId, out var dishSnapshot))
            {
                await MarkSubmitCommandFailedAsync(command, $"Dish {item.DishId} not found.", cancellationToken);
                return NotFound($"Không tìm thấy món ăn có mã {item.DishId}.");
            }

            if (!IsDishOrderable(dishSnapshot))
            {
                await MarkSubmitCommandFailedAsync(command, "Giỏ hàng có món không còn khả dụng.", cancellationToken);
                return BuildDishUnavailableConflict([dishSnapshot.Name]);
            }

            await UpsertOrderItemAsync(order.OrderID, dishSnapshot, item, cancellationToken);
        }

        var batchInventoryCheck = await ValidateIngredientAvailabilityAsync(
            order.OrderID,
            items.Select(x => new CatalogApiClient.OrderIngredientConsumptionItem(x.DishId, x.Quantity)).ToList(),
            cancellationToken);
        if (batchInventoryCheck is not null)
        {
            await MarkSubmitCommandFailedAsync(command, "Giỏ hàng vượt quá tồn kho nguyên liệu.", cancellationToken);
            return batchInventoryCheck;
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerPhoneNumber))
        {
            await AttachCustomerByPhoneAsync(order, request.CustomerPhoneNumber, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var itemCount = await _db.OrderItems.CountAsync(x => x.OrderID == order.OrderID, cancellationToken);
        if (itemCount == 0)
        {
            await MarkSubmitCommandFailedAsync(command, "Đơn hàng trống.", cancellationToken);
            return BadRequest("Đơn hàng trống");
        }

        if (await GetOrderStatusIdAsync("CONFIRMED", cancellationToken) is null)
        {
            await MarkSubmitCommandFailedAsync(command, "Status 'CONFIRMED' is missing.", cancellationToken);
            return BadRequest("Hệ thống chưa cấu hình trạng thái CONFIRMED.");
        }

        if (string.IsNullOrWhiteSpace(order.DiningSessionCode))
        {
            order.DiningSessionCode = GenerateDiningSessionCode();
        }
        await LinkTablesToSessionAsync(order.DiningSessionCode, tableId, [tableId], cancellationToken);

        var pendingItems = await _db.OrderItems
            .Where(x => x.OrderID == order.OrderID && x.StatusCode == "PENDING")
            .ToListAsync(cancellationToken);

        foreach (var item in pendingItems)
        {
            item.StatusCode = "CONFIRMED";
        }

        order.SubmitIdempotencyKey = idempotencyKey;
        await SyncOrderStateFromItemsAsync(order, cancellationToken);
        _auditLogger.Add(
            actionType: "ORDER_BATCH_SUBMITTED",
            entityType: "ORDER",
            entityId: order.OrderID.ToString(),
            tableId: order.TableID,
            orderId: order.OrderID,
            diningSessionCode: order.DiningSessionCode,
            idempotencyKey: idempotencyKey,
            beforeState: new
            {
                itemCount = 0,
                status = "PENDING"
            },
            afterState: new
            {
                itemCount = items.Count,
                status = "CONFIRMED"
            });
        await _db.SaveChangesAsync(cancellationToken);
        await MarkSubmitCommandCompletedAsync(command, order, cancellationToken);

        await PublishOrderEventAsync("order.submitted.v1", order, new
        {
            orderId = order.OrderID,
            orderCode = order.OrderCode,
            diningSessionCode = order.DiningSessionCode,
            tableId = order.TableID,
            customerId = order.CustomerID,
            statusCode = "CONFIRMED"
        }, cancellationToken);

        await tableLock.CommitAsync(cancellationToken);
        return Ok(await BuildActiveOrderResponseAsync(order, cancellationToken));
    }

    [HttpPost("api/tables/{tableId:int}/order/scan-loyalty-card")]
    public async Task<ActionResult<object>> ScanLoyaltyCard(
        int tableId,
        [FromBody] ScanLoyaltyCardRequest request,
        CancellationToken cancellationToken)
    {
        await using var tableLock = await BeginTableOperationAsync(tableId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Ok(new
            {
                success = false,
                message = "Vui lòng nhập số điện thoại"
            });
        }

        var phoneNumber = request.PhoneNumber.Trim();
        var customer = await _customersApi.GetLoyaltyByPhoneAsync(phoneNumber, cancellationToken);

        if (customer is null)
        {
            return Ok(new
            {
                success = false,
                message = "Không tìm thấy khách hàng với số điện thoại này"
            });
        }

        var order = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
        if (order is null)
        {
            return Ok(new
            {
                success = false,
                message = "Không tìm thấy đơn hàng"
            });
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        AttachCustomer(order, customer.CustomerId);
        await _db.SaveChangesAsync(cancellationToken);
        await tableLock.CommitAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Đã quét thẻ thành công",
            customer = new
            {
                name = customer.Name,
                phone = customer.Phone,
                currentPoints = customer.CurrentPoints,
                cardPoints = customer.CardPoints
            }
        });
    }

    [HttpPost("api/orders/{orderId:int}/confirm-received")]
    public async Task<ActionResult> ConfirmReceived(int orderId, [FromQuery] int customerId, [FromQuery] int? itemId, CancellationToken cancellationToken)
    {
        var tableIdHint = await _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => x.TableID)
            .FirstOrDefaultAsync(cancellationToken);
        await using var tableLock = tableIdHint is > 0
            ? await BeginTableOperationAsync(tableIdHint.Value, cancellationToken)
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.OrderID == orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        if (customerId <= 0 || order.CustomerID != customerId)
        {
            return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đơn hàng này." });
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var sessionOrders = string.IsNullOrWhiteSpace(order.DiningSessionCode)
            ? [order]
            : await _db.Orders
                .Where(x =>
                    x.TableID == order.TableID
                    && (x.IsActive ?? true)
                    && x.DiningSessionCode == order.DiningSessionCode)
                .ToListAsync(cancellationToken);

        var sessionOrderIds = sessionOrders.Select(x => x.OrderID).ToArray();
        var readyItems = itemId is > 0
            ? await _db.OrderItems.Where(x => x.OrderID == orderId && x.ItemID == itemId.Value).ToListAsync(cancellationToken)
            : await _db.OrderItems.Where(x => sessionOrderIds.Contains(x.OrderID) && x.StatusCode == "READY").ToListAsync(cancellationToken);

        if (itemId is > 0 && readyItems.Count == 0) return NotFound(new { code = "order_item_not_found", message = "Không tìm thấy món trong đơn hàng này." });
        if (itemId is > 0 && string.Equals(readyItems[0].StatusCode, "SERVING", StringComparison.OrdinalIgnoreCase)) return NoContent();
        if (readyItems.Any(x => !string.Equals(x.StatusCode, "READY", StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { code = "order_item_state_conflict", message = "Chỉ có thể xác nhận món đang ở trạng thái READY." });

        if (readyItems.Count == 0)
        {
            var alreadyServing = await _db.OrderItems.AnyAsync(
                x => sessionOrderIds.Contains(x.OrderID) && x.StatusCode == "SERVING",
                cancellationToken);
            if (alreadyServing)
            {
                return NoContent();
            }

            return BadRequest("Chỉ có thể xác nhận nhận món khi có món ở trạng thái READY.");
        }

        // Item-level confirm keeps mixed-progress rounds truthful while preserving
        // the same customer-facing confirm flow.
        foreach (var item in readyItems)
        {
            item.StatusCode = "SERVING";
        }

        foreach (var sessionOrder in sessionOrders)
        {
            await SyncOrderStateFromItemsAsync(sessionOrder, cancellationToken);
        }

        _auditLogger.Add(
            actionType: "ORDER_ITEMS_RECEIVED",
            entityType: "ORDER",
            entityId: order.OrderID.ToString(),
            tableId: order.TableID,
            orderId: order.OrderID,
            diningSessionCode: order.DiningSessionCode,
            beforeState: new { itemStatus = "READY" },
            afterState: new { itemStatus = "SERVING", itemCount = readyItems.Count });
        await _db.SaveChangesAsync(cancellationToken);
        await PublishOrderEventAsync("order.received-confirmed.v1", order, new
        {
            orderId = order.OrderID,
            orderCode = order.OrderCode,
            tableId = order.TableID,
            customerId = order.CustomerID,
            statusCode = "SERVING"
        }, cancellationToken);
        if (_db.Database.CurrentTransaction is not null)
        {
            await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("api/branches/{branchId:int}/top-dishes")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetTopDishes(int branchId, [FromQuery] int count = 5, CancellationToken cancellationToken = default)
    {
        var branchTableIds = await _catalogApi.GetBranchTableIdsAsync(branchId, cancellationToken) ?? Array.Empty<int>();
        if (branchTableIds.Count == 0)
        {
            return Ok(Array.Empty<int>());
        }

        var ids = await _db.OrderItems
            .AsNoTracking()
            .Join(_db.Orders.AsNoTracking(), i => i.OrderID, o => o.OrderID, (i, o) => new { i, o })
            .Where(x => x.o.TableID != null && branchTableIds.Contains(x.o.TableID.Value))
            .Where(x => x.i.StatusCode != "CANCELLED")
            .GroupBy(x => x.i.DishID)
            .Select(g => new { dishId = g.Key, qty = g.Sum(x => x.i.Quantity) })
            .OrderByDescending(x => x.qty)
            .Take(Math.Clamp(count, 1, 20))
            .Select(x => x.dishId)
            .ToListAsync(cancellationToken);

        return Ok(ids);
    }

    [HttpGet("api/internal/branches/{branchId:int}/cashier/orders")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetCashierOrdersInternal(
        int branchId,
        CancellationToken cancellationToken)
    {
        var branchTableIds = await _catalogApi.GetBranchTableIdsAsync(branchId, cancellationToken) ?? Array.Empty<int>();
        if (branchTableIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o =>
                (o.IsActive ?? false)
                && o.TableID != null
                && branchTableIds.Contains(o.TableID.Value)
                && ActiveCashierStatuses.Contains(o.Status.StatusCode))
            .AsSingleQuery()
            .OrderBy(o => o.OrderTime)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                diningSessionCode = o.DiningSessionCode,
                orderTime = o.OrderTime,
                tableId = o.TableID,
                customerId = o.CustomerID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName,
                items = _db.OrderItems.Where(i => i.OrderID == o.OrderID)
                    .OrderBy(i => i.ItemID)
                    .Select(i => new { itemId = i.ItemID, orderId = i.OrderID,
                        dishId = i.DishID, quantity = i.Quantity, unitPrice = i.UnitPrice,
                        lineTotal = i.LineTotal, note = i.Note, statusCode = i.StatusCode }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = orders.SelectMany(o => o.items).OrderBy(i => i.orderId).ThenBy(i => i.itemId).ToList();

        var tableLookup = (await _catalogApi.GetTablesAsync(
                orders.Where(x => x.tableId.HasValue).Select(x => x.tableId!.Value),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>())
            .ToDictionary(x => x.TableId);

        var dishLookup = (await _catalogApi.GetDishesAsync(items.Select(x => x.dishId).Distinct(), cancellationToken)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        var itemsByOrder = items
            .GroupBy(i => i.orderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var groupedPayload = orders
            .GroupBy(o => !string.IsNullOrWhiteSpace(o.diningSessionCode) ? $"session:{o.diningSessionCode}" : $"order:{o.orderId}")
            .Select(group =>
            {
                var representative = group.OrderByDescending(x => x.orderTime).First();
                var groupItems = group
                    .SelectMany(order =>
                    {
                        itemsByOrder.TryGetValue(order.orderId, out var orderItems);
                        orderItems ??= [];
                        return orderItems.Select(i => new
                        {
                            i.itemId,
                            i.orderId,
                            i.dishId,
                            i.quantity,
                            i.unitPrice,
                            i.lineTotal,
                            i.note,
                            i.statusCode,
                        });
                    })
                    .ToList();

                var materializedItems = groupItems.Select(i => new
                {
                    itemId = i.itemId,
                    orderId = i.orderId,
                    dishId = i.dishId,
                    dishName = dishLookup.TryGetValue(i.dishId, out var dish) ? dish.Name : $"Món #{i.dishId}",
                    quantity = i.quantity,
                    unitPrice = i.unitPrice,
                    lineTotal = i.lineTotal,
                    image = dishLookup.TryGetValue(i.dishId, out var imageDish) ? imageDish.Image : null,
                    note = i.note,
                    statusCode = NormalizeItemStatus(i.statusCode),
                }).ToList();

                var tableName = FormatTableName(
                    representative.tableId.HasValue && tableLookup.TryGetValue(representative.tableId.Value, out var table) ? table : null,
                    representative.tableId);

                var customerId = group
                    .Where(x => x.customerId.HasValue)
                    .OrderByDescending(x => x.orderTime)
                    .Select(x => x.customerId)
                    .FirstOrDefault();

                var statusCode = ResolveAggregateStatusCodeFromItemCodes(materializedItems.Select(x => x.statusCode), group.Select(x => x.statusCode));

                var statusName = group
                    .OrderByDescending(x => GetStatusPriority(x.statusCode))
                    .Select(x => x.statusName)
                    .FirstOrDefault() ?? "Đang phục vụ";

                return new
                {
                    orderId = representative.orderId,
                    orderCode = representative.orderCode,
                    diningSessionCode = representative.diningSessionCode,
                    orderTime = representative.orderTime,
                    tableId = representative.tableId ?? 0,
                    tableName,
                    customerId,
                    statusCode,
                    statusName,
                    subtotal = materializedItems.Where(x => !string.Equals(x.statusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase)).Sum(x => x.lineTotal),
                    itemCount = materializedItems.Where(x => !string.Equals(x.statusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase)).Sum(x => x.quantity),
                    items = materializedItems
                };
            })
            .ToList();

        return Ok(groupedPayload);
    }

    [HttpGet("api/internal/orders/{orderId:int}/checkout-context")]
    public async Task<ActionResult<object>> GetCheckoutContext(int orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o => o.OrderID == orderId)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                diningSessionCode = o.DiningSessionCode,
                tableId = o.TableID,
                customerId = o.CustomerID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName,
                isActive = o.IsActive ?? false,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var aggregateOrders = string.IsNullOrWhiteSpace(order.diningSessionCode)
            ? await _db.Orders
                .AsNoTracking()
                .Include(o => o.Status)
                .Where(o => o.OrderID == orderId)
                .ToListAsync(cancellationToken)
            : await _db.Orders
                .AsNoTracking()
                .Include(o => o.Status)
                .Where(o => (o.IsActive ?? false) && o.DiningSessionCode == order.diningSessionCode)
                .ToListAsync(cancellationToken);

        CatalogApiClient.TableSnapshotResponse? table = null;
        CatalogApiClient.BranchSnapshotResponse? branch = null;
        if (order.tableId is int tableId)
        {
            table = await _catalogApi.GetTableAsync(tableId, cancellationToken);
            if (table is not null)
            {
                branch = (await _catalogApi.GetBranchesAsync(new[] { table.BranchId }, cancellationToken))
                    ?.FirstOrDefault();
            }
        }

        var aggregateOrderIds = aggregateOrders.Select(x => x.OrderID).Distinct().ToArray();
        var aggregateItemStatuses = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => aggregateOrderIds.Contains(oi.OrderID))
            .Select(oi => new { oi.LineTotal, oi.StatusCode })
            .ToListAsync(cancellationToken);

        var subtotal = aggregateItemStatuses
            .Where(oi => !string.Equals(NormalizeItemStatus(oi.StatusCode), "CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Sum(oi => oi.LineTotal);

        var customerId = aggregateOrders
            .Where(x => x.CustomerID.HasValue)
            .OrderByDescending(x => x.OrderTime)
            .Select(x => x.CustomerID)
            .FirstOrDefault();

        var statusCode = ResolveAggregateStatusCodeFromItemCodes(
            aggregateItemStatuses.Select(x => x.StatusCode),
            aggregateOrders.Select(x => x.Status.StatusCode));
        var statusName = await _db.OrderStatus
            .AsNoTracking()
            .Where(x => x.StatusCode == statusCode)
            .Select(x => x.StatusName)
            .FirstOrDefaultAsync(cancellationToken) ?? order.statusName;

        // App (B4) dining session owning this order, if any: Billing settles those through the mobile invoice path.
        var mobileSession = string.IsNullOrWhiteSpace(order.diningSessionCode)
            ? null
            : await _db.DiningSessions.AsNoTracking()
                .Where(x => x.SessionCode == order.diningSessionCode)
                .Select(x => new { x.DiningSessionId, x.CustomerId })
                .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            diningSessionId = mobileSession?.DiningSessionId,
            diningSessionCustomerId = mobileSession?.CustomerId,
            order.orderId,
            order.orderCode,
            order.diningSessionCode,
            order.tableId,
            tableName = FormatTableName(table, order.tableId),
            branchId = table?.BranchId,
            branchName = branch?.Name,
            customerId,
            statusCode,
            statusName,
            isActive = aggregateOrders.Any(x => x.IsActive ?? false),
            subtotal
        });
    }

    [HttpGet("api/internal/orders:bill-context")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetOrderBillContexts([FromQuery] int[]? ids, CancellationToken cancellationToken)
    {
        var orderIds = (ids ?? Array.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (orderIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.OrderID))
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                tableId = o.TableID,
            })
            .ToListAsync(cancellationToken);

        var tableLookup = (await _catalogApi.GetTablesAsync(
                orders.Where(x => x.tableId.HasValue).Select(x => x.tableId!.Value),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>())
            .ToDictionary(x => x.TableId);

        var branchLookup = (await _catalogApi.GetBranchesAsync(
                tableLookup.Values.Select(x => x.BranchId),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.BranchSnapshotResponse>())
            .ToDictionary(x => x.BranchId);

        var payload = orders.Select(o =>
        {
            CatalogApiClient.TableSnapshotResponse? table = null;
            CatalogApiClient.BranchSnapshotResponse? branch = null;

            if (o.tableId.HasValue && tableLookup.TryGetValue(o.tableId.Value, out var foundTable))
            {
                table = foundTable;
                branchLookup.TryGetValue(foundTable.BranchId, out branch);
            }

            return new
            {
                orderId = o.orderId,
                orderCode = o.orderCode,
                tableId = o.tableId,
                tableName = FormatTableName(table, o.tableId),
                branchId = table?.BranchId,
                branchName = branch?.Name
            };
        }).ToList();

        return Ok(payload);
    }

    [HttpGet("api/admin/stats")]
    public async Task<ActionResult<object>> GetAdminStats([FromQuery] DateOnly? date, [FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        // This report follows the server-local calendar, as its default date does.
        // Explicit kind lets PostgreSQL's instant converter normalize the boundary.
        var from = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var to = from.AddDays(1);
        var branchTableIds = await ResolveBranchTableIdsAsync(branchId, cancellationToken);

        var todayOrdersQuery = _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderTime >= from && x.OrderTime < to);
        if (branchTableIds is not null)
        {
            todayOrdersQuery = todayOrdersQuery.Where(x => x.TableID.HasValue && branchTableIds.Contains(x.TableID.Value));
        }

        var todayOrders = await todayOrdersQuery.CountAsync(cancellationToken);
        var pendingOrders = await _db.Orders
            .Join(_db.OrderStatus, o => o.StatusID, s => s.StatusID, (o, s) => new { o, s })
            .Where(x => x.o.OrderTime >= from && x.o.OrderTime < to && x.s.StatusCode == "PENDING")
            .Where(x => branchTableIds == null || (x.o.TableID.HasValue && branchTableIds.Contains(x.o.TableID.Value)))
            .CountAsync(cancellationToken);

        var completedRevenueQuery = _db.OrderItems
            .Join(_db.Orders, i => i.OrderID, o => o.OrderID, (i, o) => new { i, o })
            .Join(_db.OrderStatus, x => x.o.StatusID, s => s.StatusID, (x, s) => new { x.i, x.o, s })
            .Where(x => x.o.OrderTime >= from && x.o.OrderTime < to && x.s.StatusCode == "COMPLETED")
            .Where(x => x.i.StatusCode != "CANCELLED");
        if (branchTableIds is not null)
        {
            completedRevenueQuery = completedRevenueQuery.Where(x => x.o.TableID.HasValue && branchTableIds.Contains(x.o.TableID.Value));
        }

        var completedRevenue = await completedRevenueQuery.SumAsync(x => (decimal?)x.i.LineTotal, cancellationToken) ?? 0m;

        return Ok(new
        {
            todayOrders,
            pendingOrders,
            todayRevenue = completedRevenue,
        });
    }

    [HttpGet("api/admin/reports/revenue")]
    public async Task<ActionResult<AdminRevenueReportResponse>> GetAdminRevenueReport(
        [FromQuery] int days = 30,
        [FromQuery] int? branchId = null,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        var from = DateTime.Today.AddDays(-(days - 1));
        var branchTableIds = await ResolveBranchTableIdsAsync(branchId, cancellationToken);

        var totalRevenueQuery = _db.OrderItems
            .AsNoTracking()
            .Join(_db.Orders.AsNoTracking(), i => i.OrderID, o => o.OrderID, (i, o) => new { i, o })
            .Where(x => x.o.OrderTime >= from)
            .Where(x => x.i.StatusCode != "CANCELLED");
        if (branchTableIds is not null)
        {
            totalRevenueQuery = totalRevenueQuery.Where(x => x.o.TableID.HasValue && branchTableIds.Contains(x.o.TableID.Value));
        }

        var totalRevenue = await totalRevenueQuery.SumAsync(x => (decimal?)x.i.LineTotal, cancellationToken) ?? 0m;

        var orderRowsQuery = _db.OrderItems
            .AsNoTracking()
            .Join(_db.Orders.AsNoTracking(), i => i.OrderID, o => o.OrderID, (i, o) => new { i, o })
            .Where(x => x.o.OrderTime >= from)
            .Where(x => x.i.StatusCode != "CANCELLED");
        if (branchTableIds is not null)
        {
            orderRowsQuery = orderRowsQuery.Where(x => x.o.TableID.HasValue && branchTableIds.Contains(x.o.TableID.Value));
        }

        var orderRows = await orderRowsQuery
            .Select(x => new
            {
                x.o.OrderID,
                x.o.OrderTime,
                x.o.TableID,
                Revenue = x.i.LineTotal
            })
            .ToListAsync(cancellationToken);

        var tableLookup = (await _catalogApi.GetTablesAsync(
                orderRows.Where(x => x.TableID.HasValue).Select(x => x.TableID!.Value),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>())
            .ToDictionary(x => x.TableId);

        var branchLookup = (await _catalogApi.GetBranchesAsync(
                tableLookup.Values.Select(x => x.BranchId),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.BranchSnapshotResponse>())
            .ToDictionary(x => x.BranchId);

        var rawRows = orderRows
            .Where(x => x.TableID.HasValue && tableLookup.ContainsKey(x.TableID.Value))
            .Select(x =>
            {
                var table = tableLookup[x.TableID!.Value];
                branchLookup.TryGetValue(table.BranchId, out var branch);
                return new
                {
                    x.OrderID,
                    x.OrderTime,
                    BranchId = table.BranchId,
                    BranchName = branch?.Name ?? $"Chi nhánh {table.BranchId}",
                    x.Revenue
                };
            })
            .ToList();

        var rows = rawRows
            .GroupBy(x => new { Date = DateOnly.FromDateTime(x.OrderTime), x.BranchId, x.BranchName })
            .Select(g => new AdminRevenueRowResponse(
                g.Key.Date,
                g.Key.BranchId,
                g.Key.BranchName,
                g.Select(x => x.OrderID).Distinct().Count(),
                g.Sum(x => x.Revenue)))
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.BranchName)
            .Take(500)
            .ToList();

        return Ok(new AdminRevenueReportResponse(totalRevenue, rows));
    }

    [HttpGet("api/admin/reports/top-dishes")]
    public async Task<ActionResult<AdminTopDishReportResponse>> GetAdminTopDishesReport(
        [FromQuery] int days = 30,
        [FromQuery] int take = 10,
        [FromQuery] int? branchId = null,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        take = Math.Clamp(take, 1, 50);
        var from = DateTime.Today.AddDays(-(days - 1));
        var branchTableIds = await ResolveBranchTableIdsAsync(branchId, cancellationToken);

        var rawItemsQuery = _db.OrderItems
            .AsNoTracking()
            .Join(_db.Orders.AsNoTracking(), i => i.OrderID, o => o.OrderID, (i, o) => new { i, o })
            .Where(x => x.o.OrderTime >= from)
            .Where(x => x.i.StatusCode != "CANCELLED");
        if (branchTableIds is not null)
        {
            rawItemsQuery = rawItemsQuery.Where(x => x.o.TableID.HasValue && branchTableIds.Contains(x.o.TableID.Value));
        }

        var rawItems = await rawItemsQuery
            .Select(x => new
            {
                x.i.DishID,
                x.i.Quantity,
                x.i.LineTotal
            })
            .ToListAsync(cancellationToken);

        var dishLookup = (await _catalogApi.GetDishesAsync(rawItems.Select(x => x.DishID).Distinct(), cancellationToken)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        var items = rawItems
            .Select(x =>
            {
                dishLookup.TryGetValue(x.DishID, out var dish);
                return new
                {
                    x.DishID,
                    DishName = dish?.Name ?? $"Món #{x.DishID}",
                    CategoryName = string.IsNullOrWhiteSpace(dish?.CategoryName) ? "Khác" : dish!.CategoryName!,
                    x.Quantity,
                    x.LineTotal
                };
            })
            .GroupBy(x => new { x.DishID, x.DishName, x.CategoryName })
            .Select(g => new AdminTopDishReportItemResponse(
                g.Key.DishID,
                g.Key.DishName,
                g.Key.CategoryName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.TotalQuantity)
            .ThenByDescending(x => x.TotalRevenue)
            .Take(take)
            .ToList();

        return Ok(new AdminTopDishReportResponse(items));
    }

    private async Task<int[]?> ResolveBranchTableIdsAsync(int? branchId, CancellationToken cancellationToken)
    {
        if (branchId is not > 0)
        {
            return null;
        }

        var tableIds = await _catalogApi.GetBranchTableIdsAsync(branchId.Value, cancellationToken);
        return tableIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<int>();
    }

    [HttpGet("api/branches/{branchId:int}/chef/orders")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetChefOrders(
        int branchId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Where(o => (o.IsActive ?? true))
            .AsQueryable();

        var branchTableIds = await _catalogApi.GetBranchTableIdsAsync(branchId, cancellationToken) ?? Array.Empty<int>();
        query = query.Where(o => o.TableID != null && branchTableIds.Contains(o.TableID.Value));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusCode = status.Trim().ToUpperInvariant();
            query = query.Where(o => o.Status.StatusCode == statusCode);
        }
        else
        {
            query = query.Where(o =>
                o.Status.StatusCode == "CONFIRMED"
                || o.Status.StatusCode == "PREPARING"
                || o.Status.StatusCode == "READY");
        }

        var orders = await query
            .AsSingleQuery()
            .OrderBy(o => o.OrderTime)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                tableId = o.TableID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName,
                orderTime = o.OrderTime,
                items = _db.OrderItems.Where(i => i.OrderID == o.OrderID)
                    .OrderBy(i => i.ItemID)
                    .Select(i => new { orderId = i.OrderID, itemId = i.ItemID,
                        dishId = i.DishID, quantity = i.Quantity, note = i.Note,
                        statusCode = i.StatusCode }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var tableLookup = (await _catalogApi.GetTablesAsync(
                orders.Where(x => x.tableId.HasValue).Select(x => x.tableId!.Value),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>())
            .ToDictionary(x => x.TableId);

        // Preserve item order and empty orders while removing the second
        // sequential Orders read. Catalog remains the live dish authority.
        var items = orders.SelectMany(o => o.items).ToList();

        var dishLookup = (await _catalogApi.GetDishesAsync(items.Select(x => x.dishId).Distinct(), cancellationToken)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        var payload = orders.Select(o => new
        {
            o.orderId,
            o.orderCode,
            o.tableId,
            tableName = FormatTableName(
                o.tableId.HasValue && tableLookup.TryGetValue(o.tableId.Value, out var table) ? table : null,
                o.tableId),
            o.statusCode,
            o.statusName,
            o.orderTime,
            items = items.Where(i => i.orderId == o.orderId).Select(i => new
            {
                i.orderId,
                i.itemId,
                i.dishId,
                dishName = dishLookup.TryGetValue(i.dishId, out var dish) ? dish.Name : $"Món #{i.dishId}",
                i.quantity,
                i.note,
                statusCode = NormalizeItemStatus(i.statusCode),
            }).ToList(),
        }).ToList();

        return Ok(payload);
    }

    [HttpGet("api/branches/{branchId:int}/chef/history")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetChefHistory(
        int branchId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var payload = await BuildChefHistoryAsync(branchId, null, 365, Math.Clamp(take, 1, 200), cancellationToken,
            includeBranchName: false);

        return Ok(payload.Select(o => new
        {
            o.OrderId,
            o.OrderCode,
            o.OrderTime,
            o.CompletedTime,
            o.TableName,
            o.StatusCode,
            o.StatusName,
            o.DishesSummary,
            o.Notes,
        }).ToList());
    }

    [HttpGet("api/internal/branches/{branchId:int}/chef/history")]
    public async Task<ActionResult<object>> GetInternalChefHistory(
        int branchId,
        [FromQuery] int? employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        
        var allItems = await BuildChefHistoryAsync(branchId, employeeId, days, 500, cancellationToken);
        
        var totalItems = allItems.Count;
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        
        var items = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return Ok(new { page, pageSize, totalItems, totalPages, items });
    }

    [HttpGet("api/internal/branches/{branchId:int}/chef/item-completions")]
    public async Task<ActionResult<object>> GetChefItemCompletions(
        int branchId,
        [FromQuery] int? employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        var fromDate = DateTime.Today.AddDays(-days);

        // DEBUG: Log parameters
        Console.WriteLine($"[DEBUG] GetChefItemCompletions called: branchId={branchId}, employeeId={employeeId}, page={page}, pageSize={pageSize}, days={days}");

        // Get all table IDs in this branch
        var tableIds = await _catalogApi.GetBranchTableIdsAsync(branchId, cancellationToken);
        
        Console.WriteLine($"[DEBUG] Found {tableIds?.Count ?? 0} tables in branch {branchId}");
        
        if (tableIds == null || tableIds.Count == 0)
        {
            return Ok(new { page, pageSize, totalItems = 0, totalPages = 0, logs = Array.Empty<object>() });
        }

        // DEBUG: Check all items with ChefId
        var allItemsWithChef = await _db.OrderItems.AsNoTracking()
            .Where(i => i.ChefId.HasValue)
            .CountAsync(cancellationToken);
        Console.WriteLine($"[DEBUG] Total items with ChefId in database: {allItemsWithChef}");

        // Get order items that were completed by chef
        // Join with Orders to get order details
        var query = from item in _db.OrderItems.AsNoTracking()
                    join order in _db.Orders.AsNoTracking() on item.OrderID equals order.OrderID
                    where item.ChefId.HasValue
                       && order.OrderTime >= fromDate
                       && order.TableID.HasValue
                       && tableIds.Contains(order.TableID.Value)
                    select new { item, order };

        // Filter by chef if specified
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(x => x.item.ChefId == employeeId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        Console.WriteLine($"[DEBUG] Query returned {totalItems} items for employeeId={employeeId}");

        var items = await query
            .OrderByDescending(x => x.order.OrderTime)
            .ThenByDescending(x => x.item.ItemID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                itemId = x.item.ItemID,
                orderId = x.order.OrderID,
                orderCode = x.order.OrderCode,
                orderTime = x.order.OrderTime,
                tableId = x.order.TableID,
                dishId = x.item.DishID,
                quantity = x.item.Quantity,
                chefId = x.item.ChefId
            })
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Ok(new { page, pageSize, totalItems, totalPages, logs = Array.Empty<object>() });
        }

        // Get table names
        var tableIdsToFetch = items.Where(i => i.tableId.HasValue).Select(i => i.tableId!.Value).Distinct().ToList();
        var tables = await _catalogApi.GetTablesAsync(tableIdsToFetch, cancellationToken) ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>();
        var tableLookup = tables.ToDictionary(t => t.TableId);

        // Get dish names
        var dishIds = items.Select(i => i.dishId).Distinct().ToList();
        var dishes = await _catalogApi.GetDishesAsync(dishIds, cancellationToken) ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var dishLookup = dishes.ToDictionary(d => d.DishId);

        var result = items.Select(i =>
        {
            var tableName = i.tableId.HasValue && tableLookup.TryGetValue(i.tableId.Value, out var table)
                ? $"Bàn {table.TableNumber}"
                : null;
            var dishName = dishLookup.TryGetValue(i.dishId, out var dish) ? dish.Name : $"Món #{i.dishId}";

            return new
            {
                auditId = i.itemId, // Use itemId as auditId for frontend compatibility
                timestampUtc = i.orderTime, // Use order time as approximation
                orderId = i.orderId,
                orderCode = i.orderCode,
                tableName,
                dishId = i.dishId,
                dishName,
                quantity = i.quantity,
                afterState = (string?)null // Not needed for this view
            };
        }).ToList();

        return Ok(new { page, pageSize, totalItems, totalPages, logs = result });
    }

    private async Task<IReadOnlyList<ChefHistoryItemResponse>> BuildChefHistoryAsync(
        int branchId,
        int? employeeId,
        int days,
        int take,
        CancellationToken cancellationToken,
        bool includeBranchName = true)
    {
        days = Math.Clamp(days, 1, 365);
        take = Math.Clamp(take, 1, 500);
        var fromDate = DateTime.Today.AddDays(-days);

        // Apply branch scope before the page limit; other branches must not consume
        // this chef's history window or be materialized for in-memory filtering.
        var branchTableIds = await _catalogApi.GetBranchTableIdsAsync(branchId, cancellationToken) ?? Array.Empty<int>();
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .AsSingleQuery()
            .Where(o => o.TableID != null && branchTableIds.Contains(o.TableID.Value) && o.OrderTime >= fromDate)
            .OrderByDescending(o => o.CompletedTime ?? o.OrderTime)
            .Take(take)
            .Select(o => new
            {
                orderId = o.OrderID,
                orderCode = o.OrderCode,
                orderTime = o.OrderTime,
                completedTime = o.CompletedTime,
                tableId = o.TableID,
                statusCode = o.Status.StatusCode,
                statusName = o.Status.StatusName,
                // Page orders first; project their items in the same owner query.
                // Keep chef-item filtering after the order page, as before.
                items = _db.OrderItems.Where(i => i.OrderID == o.OrderID
                    && (!employeeId.HasValue || employeeId.Value <= 0 || i.ChefId == employeeId.Value))
                    .Select(i => new { orderId = i.OrderID, dishId = i.DishID,
                        quantity = i.Quantity, note = i.Note, chefId = i.ChefId }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var filteredOrders = orders;

        var tableLookup = (await _catalogApi.GetTablesAsync(
                filteredOrders.Where(x => x.tableId.HasValue).Select(x => x.tableId!.Value),
                cancellationToken)
            ?? Array.Empty<CatalogApiClient.TableSnapshotResponse>())
            .ToDictionary(x => x.TableId);

        // Public Chef history does not project BranchName. Retain it for the
        // internal history contract, without paying an unused Catalog read.
        var branchName = includeBranchName
            ? (await _catalogApi.GetBranchesAsync(new[] { branchId }, cancellationToken))
                ?.FirstOrDefault()?.Name ?? $"Chi nhánh {branchId}"
            : string.Empty;

        var items = orders.SelectMany(o => o.items).ToList();
        
        // If filtering by chef, only include orders that have at least one item from this chef
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            var orderIdsWithChefItems = items.Select(i => i.orderId).Distinct().ToHashSet();
            filteredOrders = filteredOrders.Where(o => orderIdsWithChefItems.Contains(o.orderId)).ToList();
        }

        var dishLookup = (await _catalogApi.GetDishesAsync(items.Select(x => x.dishId).Distinct(), cancellationToken)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        return filteredOrders.Select(o =>
        {
            var orderItems = items.Where(i => i.orderId == o.orderId).ToList();
            var dishesSummary = string.Join(", ", orderItems.Select(i =>
            {
                var dishName = dishLookup.TryGetValue(i.dishId, out var dish) ? dish.Name : $"Món #{i.dishId}";
                return $"{i.quantity}x {dishName}";
            }));
            
            var notes = orderItems
                .Where(i => !string.IsNullOrWhiteSpace(i.note))
                .Select(i => i.note)
                .ToList();
            var combinedNotes = notes.Count > 0 ? string.Join(" | ", notes) : null;

            return new ChefHistoryItemResponse(
                o.orderId,
                o.orderCode,
                o.orderTime,
                o.completedTime,
                FormatTableName(
                    o.tableId.HasValue && tableLookup.TryGetValue(o.tableId.Value, out var table) ? table : null,
                    o.tableId),
                branchName,
                o.statusCode,
                o.statusName,
                dishesSummary,
                combinedNotes);
        }).ToList();
    }

    [HttpPost("api/orders/{orderId:int}/chef/start")]
    public Task<ActionResult> ChefStart(int orderId, [FromQuery] int? chefId, CancellationToken cancellationToken) =>
        UpdateOrderStatusAsync(orderId, "PREPARING", chefId, cancellationToken);

    [HttpPost("api/orders/{orderId:int}/chef/ready")]
    public Task<ActionResult> ChefReady(int orderId, [FromQuery] int? chefId, CancellationToken cancellationToken) =>
        UpdateOrderStatusAsync(orderId, "READY", chefId, cancellationToken);

    [HttpPost("api/orders/{orderId:int}/items/{itemId:int}/chef/start")]
    public Task<ActionResult> ChefStartItem(int orderId, int itemId, [FromQuery] int? chefId, CancellationToken cancellationToken) =>
        UpdateOrderItemStatusAsync(orderId, itemId, "PREPARING", null, chefId, cancellationToken);

    [HttpPost("api/orders/{orderId:int}/items/{itemId:int}/chef/ready")]
    public Task<ActionResult> ChefReadyItem(int orderId, int itemId, [FromQuery] int? chefId, CancellationToken cancellationToken) =>
        UpdateOrderItemStatusAsync(orderId, itemId, "READY", null, chefId, cancellationToken);

    [HttpPost("api/orders/{orderId:int}/chef/serve")]
    public ActionResult ChefServe(int orderId, CancellationToken cancellationToken) =>
        BadRequest("Bếp không thể xác nhận đã phục vụ. Khách hàng sẽ tự xác nhận khi đã nhận món.");

    [HttpPost("api/orders/{orderId:int}/status")]
    public Task<ActionResult> UpdateStatus(
        int orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StatusCode))
        {
            return Task.FromResult<ActionResult>(BadRequest("Missing statusCode."));
        }

        return UpdateOrderStatusAsync(orderId, request.StatusCode.Trim().ToUpperInvariant(), null, cancellationToken);
    }

    [HttpPost("api/orders/{orderId:int}/cancel")]
    public async Task<ActionResult> CancelOrder(
        int orderId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var tableIdHint = await _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => x.TableID)
            .FirstOrDefaultAsync(cancellationToken);
        await using var tableLock = tableIdHint is > 0
            ? await BeginTableOperationAsync(tableIdHint.Value, cancellationToken)
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var currentStatusCode = await GetOrderStatusCodeAsync(order.StatusID, cancellationToken);
        if (string.Equals(currentStatusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            if (_db.Database.CurrentTransaction is not null)
            {
                await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
            }
            return NoContent();
        }

        if (string.Equals(currentStatusCode, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Đơn hàng đã hoàn tất nên không thể hủy." });
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var items = await _db.OrderItems
            .Where(x => x.OrderID == orderId)
            .ToListAsync(cancellationToken);

        var changedItems = items
            .Where(x => IsAllowedItemTransition(x.StatusCode, "CANCELLED"))
            .ToList();

        if (changedItems.Count == 0)
        {
            return Conflict(new { message = "Đơn hàng hiện tại không còn món nào có thể hủy." });
        }

        foreach (var item in changedItems)
        {
            item.StatusCode = "CANCELLED";
        }

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            var reason = request.Reason.Trim();
            order.Note = string.IsNullOrWhiteSpace(order.Note) ? $"[CANCEL] {reason}" : $"{order.Note} | [CANCEL] {reason}";
        }

        var syncResult = await SyncOrderStateFromItemsAsync(order, cancellationToken);

        if (syncResult.StatusCode == "CANCELLED"
            && order.TableID is int tableId
            && !await HasOtherActiveOrdersInSameSessionAsync(order, cancellationToken)
            && !await HasOpenDiningSessionAsync(tableId, cancellationToken))
        {
            await _catalogApi.ReleaseTableAsync(tableId, cancellationToken);
        }

        _auditLogger.Add(
            actionType: "ORDER_CANCELLED",
            entityType: "ORDER",
            entityId: order.OrderID.ToString(),
            tableId: order.TableID,
            orderId: order.OrderID,
            diningSessionCode: order.DiningSessionCode,
            beforeState: new { status = currentStatusCode, cancelledItems = changedItems.Select(x => x.ItemID).ToArray() },
            afterState: new { status = syncResult.StatusCode, cancelledItems = changedItems.Count },
            notes: request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        if (_db.Database.CurrentTransaction is not null)
        {
            await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }
        await PublishOrderEventAsync("order.cancelled.v1", order, new
        {
            orderId = order.OrderID,
            orderCode = order.OrderCode,
            tableId = order.TableID,
            customerId = order.CustomerID,
            reason = request.Reason,
            statusCode = syncResult.StatusCode
        }, cancellationToken);
        return NoContent();
    }

    [HttpPost("api/orders/{orderId:int}/items/{itemId:int}/cancel")]
    public Task<ActionResult> CancelOrderItem(
        int orderId,
        int itemId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken) =>
        UpdateOrderItemStatusAsync(orderId, itemId, "CANCELLED", request.Reason, null, cancellationToken);


    [HttpGet("api/orders/{orderId:int}/items/{itemId:int}/ingredient-overrides")]
    public async Task<ActionResult<OrderItemIngredientOverridesResponse>> GetOrderItemIngredientOverrides(int orderId, int itemId, CancellationToken cancellationToken)
    {
        await EnsureOrderItemIngredientOverrideSchemaAsync(cancellationToken);

        var resolution = await ResolveOrderItemForOverrideAsync(orderId, itemId, cancellationToken);
        if (resolution.Item is null)
        {
            return NotFound();
        }
        var item = resolution.Item;

        var overrides = await _db.OrderItemIngredients
            .AsNoTracking()
            .Where(x => x.OrderItemID == item.ItemID && !x.IsRemoved)
            .Select(x => new OrderItemIngredientOverrideItem(x.IngredientID, null, null, x.Quantity))
            .ToListAsync(cancellationToken);

        return Ok(new OrderItemIngredientOverridesResponse(orderId, item.ItemID, item.DishID, overrides));
    }

    [HttpPut("api/orders/{orderId:int}/items/{itemId:int}/ingredient-overrides")]
    public async Task<ActionResult> UpdateOrderItemIngredientOverrides(int orderId, int itemId, [FromBody] UpdateOrderItemIngredientOverridesRequest request, CancellationToken cancellationToken)
    {
        await EnsureOrderItemIngredientOverrideSchemaAsync(cancellationToken);

        var order = await _db.Orders
            .Include(o => o.Status)
            .FirstOrDefaultAsync(o => o.OrderID == orderId && (o.IsActive ?? true), cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var allowed = new[] { "CONFIRMED", "PREPARING", "READY", "SERVING" };
        if (!allowed.Contains(order.Status.StatusCode))
        {
            return BadRequest("Chỉ được chỉnh thành phần khi đơn đang trong luồng xử lý bếp.");
        }

        var resolution = await ResolveOrderItemForOverrideAsync(orderId, itemId, cancellationToken, track: true);
        var ingredientCount = request.Items?.Count ?? 0;
        _logger.LogInformation(
            "Chef ingredient override save requested. OrderId={OrderId}; IncomingItemId={IncomingItemId}; ResolvedOrderItemId={ResolvedOrderItemId}; ResolutionMethod={ResolutionMethod}; MatchCount={MatchCount}; IngredientCount={IngredientCount}.",
            orderId,
            itemId,
            resolution.Item?.ItemID,
            resolution.Method,
            resolution.MatchCount,
            ingredientCount);
        if (resolution.Item is null)
        {
            return BadRequest("Không tìm thấy món trong đơn hàng hiện tại để cập nhật thành phần.");
        }
        var item = resolution.Item;
        var resolvedItemId = item.ItemID;

        var resolvedStillExists = await _db.OrderItems
            .AsNoTracking()
            .AnyAsync(x => x.OrderID == orderId && x.ItemID == resolvedItemId, cancellationToken);
        if (!resolvedStillExists)
        {
            _logger.LogWarning(
                "Chef ingredient override blocked before save because resolved item no longer exists. OrderId={OrderId}; IncomingItemId={IncomingItemId}; ResolvedOrderItemId={ResolvedOrderItemId}; ResolutionMethod={ResolutionMethod}.",
                orderId,
                itemId,
                resolvedItemId,
                resolution.Method);
            return BadRequest("Không tìm thấy món trong đơn hàng hiện tại để cập nhật thành phần.");
        }

        var cleaned = (request.Items ?? Array.Empty<UpdateOrderItemIngredientOverrideItem>())
            .Where(x => x.IngredientId > 0)
            .GroupBy(x => x.IngredientId)
            .Select(g => g.Last())
            .ToList();
        if (cleaned.Any(x => x.Quantity < 0))
        {
            return BadRequest("Số lượng nguyên liệu không được âm.");
        }

        var current = await _db.OrderItemIngredients.Where(x => x.OrderItemID == resolvedItemId).ToListAsync(cancellationToken);
        _db.OrderItemIngredients.RemoveRange(current);
        var normalizedOverrideNote = NormalizeSnapshot(request.Note, 500);
        foreach (var row in cleaned)
        {
            _db.OrderItemIngredients.Add(new OrderItemIngredients
            {
                OrderItemID = resolvedItemId,
                IngredientID = row.IngredientId,
                IngredientName = NormalizeSnapshot(row.IngredientName, 200),
                Unit = NormalizeSnapshot(row.Unit, 50),
                Quantity = row.Quantity,
                Note = normalizedOverrideNote,
                IsRemoved = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var note = request.Note.Trim();
            item.Note = string.IsNullOrWhiteSpace(item.Note) ? $"Điều chỉnh thành phần: {note}" : $"{item.Note} | Điều chỉnh thành phần: {note}";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã lưu thành phần riêng cho món trong đơn này." });
    }

    [HttpPut("api/orders/{orderId:int}/items/{itemId:int}/chef-note")]
    public async Task<ActionResult> ChefUpdateItemNote(
        int orderId,
        int itemId,
        [FromBody] UpdateItemNoteRequest request,
        CancellationToken cancellationToken)
    {
        var tableIdHint = await _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => x.TableID)
            .FirstOrDefaultAsync(cancellationToken);
        await using var tableLock = tableIdHint is > 0
            ? await BeginTableOperationAsync(tableIdHint.Value, cancellationToken)
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = await _db.Orders
            .Include(o => o.Status)
            .FirstOrDefaultAsync(o => o.OrderID == orderId && (o.IsActive ?? true), cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var allowStatuses = new[] { "CONFIRMED", "PREPARING", "READY", "SERVING" };
        if (!allowStatuses.Contains(order.Status.StatusCode))
        {
            return BadRequest("Đơn hàng chưa ở trạng thái xử lý của bếp.");
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var item = await _db.OrderItems
            .FirstOrDefaultAsync(x => x.ItemID == itemId && x.OrderID == orderId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var incoming = NormalizeNote(request.Note);
        if (request.Append == true && !string.IsNullOrWhiteSpace(incoming))
        {
            var chefNote = $"[BEP] {incoming}";
            item.Note = string.IsNullOrWhiteSpace(item.Note)
                ? chefNote
                : $"{item.Note} | {chefNote}";
        }
        else
        {
            item.Note = incoming;
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (_db.Database.CurrentTransaction is not null)
        {
            await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }
        return NoContent();
    }

    private async Task<OrderEntity?> GetOrCreateOrderAsync(int tableId, bool createIfMissing, CancellationToken cancellationToken)
    {
        var tableSnapshot = await _catalogApi.GetTableAsync(tableId, cancellationToken);
        if (tableSnapshot is null)
        {
            return null;
        }

        // A real dining table may place multiple kitchen rounds before checkout.
        // We therefore resolve the active dining session first, then find or open
        // the editable pending round inside that session instead of reusing the
        // newest active order blindly.
        var activeSessionCode = await GetLatestActiveSessionCodeAsync(tableId, cancellationToken);
        var pendingRound = await GetPendingRoundAsync(tableId, activeSessionCode, cancellationToken);
        if (pendingRound is not null)
        {
            return pendingRound;
        }

        if (activeSessionCode is not null)
        {
            var latestActiveInSession = await _db.Orders
                .Where(x =>
                    x.TableID == tableId
                    && (x.IsActive ?? true)
                    && x.DiningSessionCode == activeSessionCode)
                .OrderByDescending(x => x.OrderTime)
                .FirstOrDefaultAsync(cancellationToken);

            if (!createIfMissing)
            {
                return latestActiveInSession;
            }

            var sessionCustomerId = await GetSessionCustomerIdAsync(tableId, activeSessionCode, cancellationToken);
            return await CreatePendingOrderAsync(tableId, activeSessionCode, sessionCustomerId, cancellationToken);
        }

        if (!createIfMissing)
        {
            return null;
        }

        return await CreatePendingOrderAsync(tableId, diningSessionCode: null, customerId: null, cancellationToken: cancellationToken);
    }

    private async Task<OrderEntity> CreatePendingOrderAsync(int tableId, string? diningSessionCode, int? customerId, CancellationToken cancellationToken)
    {
        var pendingId = await GetOrderStatusIdAsync("PENDING", cancellationToken) ?? 1;
        var order = new OrderEntity
        {
            DiningSessionCode = NormalizeSessionCode(diningSessionCode),
            TableID = tableId,
            CustomerID = customerId,
            StatusID = pendingId,
            IsActive = true,
            OrderTime = DateTime.Now,
            OrderCode = await GenerateOrderCodeAsync(cancellationToken),
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await _catalogApi.OccupyTableAsync(tableId, order.OrderID, cancellationToken);
        if (!string.IsNullOrWhiteSpace(order.DiningSessionCode))
        {
            await LinkTablesToSessionAsync(order.DiningSessionCode, tableId, [tableId], cancellationToken);
        }
        return order;
    }

    private async Task LinkTablesToSessionAsync(string? diningSessionCode, int primaryTableId, IReadOnlyList<int> tableIds, CancellationToken cancellationToken)
    {
        var sessionCode = NormalizeSessionCode(diningSessionCode);
        if (string.IsNullOrWhiteSpace(sessionCode))
        {
            return;
        }

        var normalizedTableIds = NormalizeTableIds([.. tableIds, primaryTableId]);
        if (primaryTableId <= 0 || normalizedTableIds.Count == 0)
        {
            return;
        }

        var existingRows = await _db.DiningSessionTables
            .Where(x => x.DiningSessionCode == sessionCode)
            .ToListAsync(cancellationToken);

        var effectivePrimaryTableId = existingRows.FirstOrDefault(x => x.IsPrimary)?.TableID ?? primaryTableId;
        if (normalizedTableIds.Count > 1 || !existingRows.Any(x => x.IsPrimary))
        {
            effectivePrimaryTableId = primaryTableId;
        }

        foreach (var tableId in normalizedTableIds)
        {
            var row = existingRows.FirstOrDefault(x => x.TableID == tableId);
            if (row is null)
            {
                row = new DiningSessionTables
                {
                    DiningSessionCode = sessionCode,
                    TableID = tableId,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.DiningSessionTables.Add(row);
                existingRows.Add(row);
            }

            row.IsPrimary = tableId == effectivePrimaryTableId;
        }

        foreach (var row in existingRows)
        {
            row.IsPrimary = row.TableID == effectivePrimaryTableId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<int> NormalizeTableIds(IEnumerable<int>? tableIds)
        => tableIds?
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray()
            ?? Array.Empty<int>();

    private async Task<string> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var now = DateTime.Now;
            var suffix = Random.Shared.Next(100, 999);
            var code = $"ORD-{now:yyyyMMddHHmmssfff}-{suffix}";
            var exists = await _db.Orders.AnyAsync(o => o.OrderCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"ORD-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".Substring(0, 36);
    }

    private async Task<int?> GetOrderStatusIdAsync(string statusCode, CancellationToken cancellationToken)
    {
        return await _db.OrderStatus
            .Where(x => x.StatusCode == statusCode)
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> GetOrderStatusCodeAsync(int statusId, CancellationToken cancellationToken)
    {
        return await _db.OrderStatus
            .Where(x => x.StatusID == statusId)
            .Select(x => x.StatusCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<object> BuildActiveOrderResponseAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        // Customer and cashier screens need a table-level session view so we do
        // not lose earlier rounds or attach new dishes to a closed dining visit.
        var aggregateOrders = await GetOrdersForAggregateAsync(order, cancellationToken);
        var aggregateOrderIds = aggregateOrders.Select(x => x.OrderID).ToArray();
        var statusRows = await _db.OrderStatus
            .AsNoTracking()
            .Select(x => new StatusRow(x.StatusID, x.StatusCode, x.StatusName))
            .ToListAsync(cancellationToken);
        var statusLookup = statusRows.ToDictionary(x => x.StatusID);

        var rawItems = await _db.OrderItems
            .AsNoTracking()
            .Where(x => aggregateOrderIds.Contains(x.OrderID))
            .OrderBy(x => x.OrderID)
            .ThenBy(x => x.ItemID)
            .Select(x => new
            {
                x.OrderID,
                x.ItemID,
                x.DishID,
                x.Quantity,
                x.UnitPrice,
                x.LineTotal,
                x.Note,
                x.StatusCode,
            })
            .ToListAsync(cancellationToken);

        var dishIds = rawItems
            .Select(x => x.DishID)
            .Distinct()
            .ToList();

        var dishLookup = (await _catalogApi.GetDishesAsync(dishIds, cancellationToken)
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId, x => (x.Name, x.Unit, x.Image));

        var items = rawItems.Select(x =>
        {
            dishLookup.TryGetValue(x.DishID, out var dish);
            return new
            {
                itemId = x.ItemID,
                orderId = x.OrderID,
                dishId = x.DishID,
                dishName = dish.Name ?? $"Mon #{x.DishID}",
                quantity = x.Quantity,
                unitPrice = x.UnitPrice,
                lineTotal = x.LineTotal,
                note = x.Note,
                unit = dish.Unit,
                image = dish.Image,
                status = NormalizeItemStatus(x.StatusCode),
            };
        }).ToList();

        var representativeOrder = aggregateOrders
            .OrderByDescending(x => x.OrderTime)
            .First();
        var pendingRound = aggregateOrders
            .Where(x => statusLookup.TryGetValue(x.StatusID, out var status) && string.Equals(status.StatusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OrderTime)
            .FirstOrDefault();
        var aggregateStatusCode = ResolveAggregateStatusCodeFromItemCodes(items.Select(x => x.status), aggregateOrders.Select(x => statusLookup.TryGetValue(x.StatusID, out var status) ? status.StatusCode : null));
        var aggregateStatus = statusRows.FirstOrDefault(x => string.Equals(x.StatusCode, aggregateStatusCode, StringComparison.OrdinalIgnoreCase));
        var hasCommittedSession = aggregateOrders.Any(x => !string.IsNullOrWhiteSpace(x.DiningSessionCode));

        return new
        {
            orderId = representativeOrder.OrderID,
            orderCode = representativeOrder.OrderCode,
            diningSessionCode = representativeOrder.DiningSessionCode,
            hasActiveDiningSession = hasCommittedSession,
            activeOrderIds = aggregateOrderIds,
            hasPendingRound = pendingRound is not null,
            pendingOrderId = pendingRound?.OrderID,
            tableId = representativeOrder.TableID,
            customerId = representativeOrder.CustomerID,
            statusCode = aggregateStatusCode,
            orderStatus = aggregateStatus?.StatusName ?? "Pending",
            subtotal = items.Where(x => !string.Equals(x.status, "CANCELLED", StringComparison.OrdinalIgnoreCase)).Sum(x => x.lineTotal),
            totalItems = items.Where(x => !string.Equals(x.status, "CANCELLED", StringComparison.OrdinalIgnoreCase)).Sum(x => x.quantity),
            items,
        };
    }

    private async Task<ActionResult> UpdateOrderStatusAsync(int orderId, string statusCode, int? chefId, CancellationToken cancellationToken)
    {
        var tableIdHint = await _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => x.TableID)
            .FirstOrDefaultAsync(cancellationToken);
        await using var tableLock = tableIdHint is > 0
            ? await BeginTableOperationAsync(tableIdHint.Value, cancellationToken)
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var orderItems = await _db.OrderItems
            .Where(x => x.OrderID == order.OrderID)
            .ToListAsync(cancellationToken);
        var beforeOrderStatusCode = await GetOrderStatusCodeAsync(order.StatusID, cancellationToken);

        var changedItems = orderItems
            .Where(x => IsAllowedItemTransition(x.StatusCode, statusCode))
            .ToList();

        if (changedItems.Count == 0)
        {
            var alreadyInTarget = orderItems.Count > 0 && orderItems.All(x =>
                string.Equals(NormalizeItemStatus(x.StatusCode), statusCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeItemStatus(x.StatusCode), "CANCELLED", StringComparison.OrdinalIgnoreCase));
            if (alreadyInTarget)
            {
                if (_db.Database.CurrentTransaction is not null)
                {
                    await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
                }
                return NoContent();
            }

            return Conflict(new { message = $"Đơn hàng hiện không còn món nào có thể chuyển sang {statusCode}." });
        }

        var entersKitchenFlow = string.Equals(statusCode, "PREPARING", StringComparison.OrdinalIgnoreCase);
        var isReady = string.Equals(statusCode, "READY", StringComparison.OrdinalIgnoreCase);

        if (entersKitchenFlow)
        {
            var consumptionItems = changedItems
                .Select(x => new CatalogApiClient.OrderIngredientConsumptionItem(x.DishID, x.Quantity, x.ItemID))
                .ToList();

            if (consumptionItems.Count == 0)
            {
                return BadRequest("Đơn hàng không có món để chuyển sang bếp.");
            }

            var consumption = await _catalogApi.ConsumeIngredientsForOrderAsync(order.OrderID, consumptionItems, cancellationToken);
            if (!consumption.Success)
            {
                return Conflict(new
                {
                    message = consumption.Message ?? "Không thể trừ nguyên liệu trong kho.",
                    issues = consumption.Issues.Select(x => new
                    {
                        ingredientId = x.IngredientId,
                        ingredientName = x.IngredientName,
                        requiredQuantity = x.RequiredQuantity,
                        availableQuantity = x.AvailableQuantity,
                        unit = x.Unit
                    }).ToList()
                });
            }
        }

        foreach (var item in changedItems)
        {
            item.StatusCode = statusCode;
            
            // Assign ChefId when chef starts preparing or marks as ready
            if ((entersKitchenFlow || isReady) && chefId.HasValue && chefId.Value > 0)
            {
                if (!item.ChefId.HasValue)
                {
                    item.ChefId = chefId.Value;
                }
            }
        }

        var syncResult = await SyncOrderStateFromItemsAsync(order, cancellationToken);
        _auditLogger.Add(
            actionType: $"ORDER_ITEMS_{statusCode}",
            entityType: "ORDER",
            entityId: order.OrderID.ToString(),
            tableId: order.TableID,
            orderId: order.OrderID,
            diningSessionCode: order.DiningSessionCode,
            beforeState: new { status = beforeOrderStatusCode, changedItemIds = changedItems.Select(x => x.ItemID).ToArray() },
            afterState: new { status = syncResult.StatusCode, changedItemIds = changedItems.Select(x => x.ItemID).ToArray() });
        await _db.SaveChangesAsync(cancellationToken);
        var atomicReadyEvents = isReady && _configuration.GetValue<bool?>("Runtime:LegacyPersistentWorkers") == false;
        if (atomicReadyEvents)
        {
            foreach (var changedItem in changedItems)
                await PublishOrderEventAsync("order.status-ready.v1", order, new
                {
                    orderId = order.OrderID,
                    orderCode = order.OrderCode,
                    tableId = order.TableID,
                    customerId = order.CustomerID,
                    orderItemId = changedItem.ItemID,
                    dishId = changedItem.DishID,
                    dishName = await ResolveDishNameAsync(changedItem.DishID, cancellationToken),
                    quantity = changedItem.Quantity,
                    statusCode = syncResult.StatusCode,
                    itemStatusCode = "READY"
                }, cancellationToken);
        }
        if (_db.Database.CurrentTransaction is not null)
        {
            await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }

        if (isReady && !atomicReadyEvents)
        {
            foreach (var item in changedItems)
            {
                await PublishOrderEventAsync("order.status-ready.v1", order, new
                {
                    orderId = order.OrderID,
                    orderCode = order.OrderCode,
                    tableId = order.TableID,
                    customerId = order.CustomerID,
                    orderItemId = item.ItemID,
                    dishId = item.DishID,
                    dishName = await ResolveDishNameAsync(item.DishID, cancellationToken),
                    quantity = item.Quantity,
                    statusCode = syncResult.StatusCode,
                    itemStatusCode = "READY"
                }, cancellationToken);
            }
        }

        return NoContent();
    }

    private async Task<ActionResult> UpdateOrderItemStatusAsync(
        int orderId,
        int itemId,
        string statusCode,
        string? cancelReason,
        int? chefId,
        CancellationToken cancellationToken)
    {
        var tableIdHint = await _db.Orders
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => x.TableID)
            .FirstOrDefaultAsync(cancellationToken);
        await using var tableLock = tableIdHint is > 0
            ? await BeginTableOperationAsync(tableIdHint.Value, cancellationToken)
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.OrderID == orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var sessionCheck = await EnsureSessionWritableAsync(order.OrderID, order.DiningSessionCode, null, cancellationToken);
        if (sessionCheck is not null)
        {
            return sessionCheck;
        }

        var item = await _db.OrderItems.FirstOrDefaultAsync(x => x.OrderID == orderId && x.ItemID == itemId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!IsAllowedItemTransition(item.StatusCode, statusCode))
        {
            return Conflict(new { message = $"Món hiện không thể chuyển sang {statusCode}." });
        }

        if (string.Equals(statusCode, "PREPARING", StringComparison.OrdinalIgnoreCase))
        {
            var consumption = await _catalogApi.ConsumeIngredientsForOrderAsync(
                order.OrderID,
                [new CatalogApiClient.OrderIngredientConsumptionItem(item.DishID, item.Quantity, item.ItemID)],
                cancellationToken);
            if (!consumption.Success)
            {
                return Conflict(new { message = consumption.Message ?? "Không đủ nguyên liệu để tiếp tục chế biến.", details = consumption.Issues });
            }
            
            // Assign chef when starting to prepare
            if (chefId.HasValue && chefId.Value > 0)
            {
                item.ChefId = chefId.Value;
            }
        }

        // Also assign chef when marking as READY (in case chef skipped PREPARING step)
        if (string.Equals(statusCode, "READY", StringComparison.OrdinalIgnoreCase))
        {
            if (chefId.HasValue && chefId.Value > 0 && !item.ChefId.HasValue)
            {
                item.ChefId = chefId.Value;
            }
        }

        var beforeStatus = NormalizeItemStatus(item.StatusCode);
        item.StatusCode = statusCode;
        if (string.Equals(statusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cancelReason))
        {
            var reason = cancelReason.Trim();
            item.Note = string.IsNullOrWhiteSpace(item.Note) ? $"[HỦY] {reason}" : $"{item.Note} | [HỦY] {reason}";
        }

        var syncResult = await SyncOrderStateFromItemsAsync(order, cancellationToken);
        _auditLogger.Add(
            actionType: $"ORDER_ITEM_{statusCode}",
            entityType: "ORDER_ITEM",
            entityId: item.ItemID.ToString(),
            tableId: order.TableID,
            orderId: order.OrderID,
            orderItemId: item.ItemID,
            dishId: item.DishID,
            diningSessionCode: order.DiningSessionCode,
            beforeState: new { status = beforeStatus },
            afterState: new { status = statusCode, orderStatus = syncResult.StatusCode, chefId = item.ChefId },
            notes: cancelReason);
        await _db.SaveChangesAsync(cancellationToken);
        var ready = string.Equals(statusCode, "READY", StringComparison.OrdinalIgnoreCase);
        var atomicReadyEvent = ready && _configuration.GetValue<bool?>("Runtime:LegacyPersistentWorkers") == false;
        if (atomicReadyEvent)
            await PublishOrderEventAsync("order.status-ready.v1", order, new
            {
                orderId = order.OrderID,
                orderCode = order.OrderCode,
                tableId = order.TableID,
                customerId = order.CustomerID,
                orderItemId = item.ItemID,
                dishId = item.DishID,
                dishName = await ResolveDishNameAsync(item.DishID, cancellationToken),
                quantity = item.Quantity,
                statusCode = syncResult.StatusCode,
                itemStatusCode = "READY"
            }, cancellationToken);
        if (_db.Database.CurrentTransaction is not null)
        {
            await _db.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }

        if (ready && !atomicReadyEvent)
        {
            await PublishOrderEventAsync("order.status-ready.v1", order, new
            {
                orderId = order.OrderID,
                orderCode = order.OrderCode,
                tableId = order.TableID,
                customerId = order.CustomerID,
                orderItemId = item.ItemID,
                dishId = item.DishID,
                dishName = await ResolveDishNameAsync(item.DishID, cancellationToken),
                quantity = item.Quantity,
                statusCode = syncResult.StatusCode,
                itemStatusCode = "READY"
            }, cancellationToken);
        }

        if (string.Equals(statusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase)
            && syncResult.StatusCode == "CANCELLED"
            && order.TableID is int tableId
            && !await HasOtherActiveOrdersInSameSessionAsync(order, cancellationToken)
            && !await HasOpenDiningSessionAsync(tableId, cancellationToken))
        {
            await _catalogApi.ReleaseTableAsync(tableId, cancellationToken);
        }

        return NoContent();
    }

    private static bool IsKitchenStatus(string? statusCode) =>
        string.Equals(statusCode, "PREPARING", StringComparison.OrdinalIgnoreCase)
        || string.Equals(statusCode, "READY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(statusCode, "SERVING", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedOrderTransition(string? currentStatusCode, string nextStatusCode)
    {
        var current = (currentStatusCode ?? string.Empty).Trim().ToUpperInvariant();
        var next = (nextStatusCode ?? string.Empty).Trim().ToUpperInvariant();

        return (current, next) switch
        {
            ("PENDING", "PREPARING") => true,
            ("CONFIRMED", "PREPARING") => true,
            ("PREPARING", "READY") => true,
            ("READY", "SERVING") => true,
            ("PENDING", "CANCELLED") => true,
            ("CONFIRMED", "CANCELLED") => true,
            ("PREPARING", "CANCELLED") => true,
            ("READY", "CANCELLED") => true,
            _ => false
        };
    }

    private static string NormalizeItemStatus(string? statusCode)
        => string.IsNullOrWhiteSpace(statusCode) ? "PENDING" : statusCode.Trim().ToUpperInvariant();

    private static string? FormatTableName(CatalogApiClient.TableSnapshotResponse? table, int? fallbackTableId)
    {
        if (table is not null)
        {
            return $"Bàn {table.TableNumber}";
        }

        return fallbackTableId.HasValue ? $"Bàn {fallbackTableId.Value}" : null;
    }

    private static bool IsAllowedItemTransition(string? currentStatusCode, string nextStatusCode)
    {
        var current = NormalizeItemStatus(currentStatusCode);
        var next = NormalizeItemStatus(nextStatusCode);

        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            ("PENDING", "CONFIRMED") => true,
            ("PENDING", "PREPARING") => true,
            ("CONFIRMED", "PREPARING") => true,
            ("PREPARING", "READY") => true,
            ("READY", "SERVING") => true,
            ("PENDING", "CANCELLED") => true,
            ("CONFIRMED", "CANCELLED") => true,
            ("PREPARING", "CANCELLED") => true,
            ("READY", "CANCELLED") => true,
            _ => false
        };
    }

    private static string ResolveAggregateStatusCodeFromItemCodes(IEnumerable<string?> itemStatuses, IEnumerable<string?> fallbackOrderStatuses)
    {
        var statuses = itemStatuses
            .Select(NormalizeItemStatus)
            .ToArray();

        if (statuses.Length == 0)
        {
            return ResolveAggregateStatusCodeFromCodes(fallbackOrderStatuses);
        }

        var activeStatuses = statuses.Where(x => x != "CANCELLED").ToArray();
        if (activeStatuses.Length == 0)
        {
            return "CANCELLED";
        }

        if (activeStatuses.Contains("PENDING")) return "PENDING";
        if (activeStatuses.Contains("CONFIRMED")) return "CONFIRMED";
        if (activeStatuses.Contains("PREPARING")) return "PREPARING";
        if (activeStatuses.Contains("READY")) return "READY";
        if (activeStatuses.Contains("SERVING")) return "SERVING";
        return "PENDING";
    }

    private async Task<(string StatusCode, bool HasActiveItems)> SyncOrderStateFromItemsAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        // Whole-order status is now derived from item truth so mixed-progress rounds
        // stay readable without letting one item overwrite the rest.
        var trackedItems = _db.ChangeTracker
            .Entries<OrderItems>()
            .Where(entry =>
                entry.Entity.OrderID == order.OrderID
                && entry.State is not EntityState.Deleted
                && entry.State is not EntityState.Detached)
            .Select(entry => entry.Entity)
            .ToList();

        List<string?> itemStatuses;
        if (trackedItems.Count > 0)
        {
            var trackedItemIds = trackedItems
                .Where(item => item.ItemID > 0)
                .Select(item => item.ItemID)
                .ToArray();

            var persistedStatuses = await _db.OrderItems
                .AsNoTracking()
                .Where(x => x.OrderID == order.OrderID && !trackedItemIds.Contains(x.ItemID))
                .Select(x => (string?)x.StatusCode)
                .ToListAsync(cancellationToken);

            // Submit/start/ready flows often update item entities and then derive the
            // order summary before SaveChangesAsync. Re-reading only from SQL would miss
            // those tracked mutations and leave the whole round stuck on stale status.
            itemStatuses = trackedItems
                .Select(item => (string?)item.StatusCode)
                .Concat(persistedStatuses)
                .ToList();
        }
        else
        {
            itemStatuses = await _db.OrderItems
                .Where(x => x.OrderID == order.OrderID)
                .Select(x => (string?)x.StatusCode)
                .ToListAsync(cancellationToken);
        }

        var derivedStatusCode = ResolveAggregateStatusCodeFromItemCodes(itemStatuses, [await GetOrderStatusCodeAsync(order.StatusID, cancellationToken)]);
        var hasActiveItems = itemStatuses.Any(status => !string.Equals(NormalizeItemStatus(status), "CANCELLED", StringComparison.OrdinalIgnoreCase));
        var statusId = await GetOrderStatusIdAsync(derivedStatusCode, cancellationToken);
        if (statusId is null)
        {
            throw new InvalidOperationException($"Status '{derivedStatusCode}' is missing.");
        }

        order.StatusID = statusId.Value;
        order.IsActive = hasActiveItems;
        if (!hasActiveItems)
        {
            order.CompletedTime ??= DateTime.Now;
        }
        else if (!string.Equals(derivedStatusCode, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            order.CompletedTime = null;
        }

        return (derivedStatusCode, hasActiveItems);
    }

    private async Task<string> ResolveDishNameAsync(int dishId, CancellationToken cancellationToken)
    {
        var dish = await _catalogApi.GetDishAsync(dishId, cancellationToken);
        return string.IsNullOrWhiteSpace(dish?.Name) ? $"Món #{dishId}" : dish.Name;
    }

    private async Task UpsertOrderItemAsync(
        int orderId,
        CatalogApiClient.DishSnapshotResponse dishSnapshot,
        AddItemRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedNote = NormalizeNote(request.Note);
        var item = await _db.OrderItems
            .FirstOrDefaultAsync(x => x.OrderID == orderId && x.DishID == request.DishId && x.Note == normalizedNote, cancellationToken);

        if (item is null)
        {
            item = new OrderItems
            {
                OrderID = orderId,
                DishID = request.DishId,
                Quantity = request.Quantity,
                UnitPrice = dishSnapshot.Price,
                LineTotal = dishSnapshot.Price * request.Quantity,
                Note = normalizedNote,
                StatusCode = "PENDING",
            };
            _db.OrderItems.Add(item);
            return;
        }

        item.Quantity += request.Quantity;
        item.LineTotal = item.UnitPrice * item.Quantity;
    }

    private async Task AttachCustomerByPhoneAsync(OrderEntity order, string phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return;
        }

        var customer = await _customersApi.GetLoyaltyByPhoneAsync(phoneNumber.Trim(), cancellationToken);
        if (customer is null)
        {
            return;
        }

        AttachCustomer(order, customer.CustomerId);
    }

    private static void AttachCustomer(OrderEntity order, int customerId)
    {
        if (customerId > 0)
        {
            order.CustomerID = customerId;
        }
    }

    private async Task PublishOrderEventAsync(string eventName, OrderEntity order, object payload, CancellationToken cancellationToken)
    {
        await _eventPublisher.PublishAsync(new IntegrationEventEnvelope(
            EventName: eventName,
            OccurredAtUtc: DateTime.UtcNow,
            Source: "Orders.Api",
            CorrelationId: HttpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier,
            Payload: payload), cancellationToken);
    }

    private static string? NormalizeNote(string? note)
    {
        var cleaned = note?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, 500)];
    }

    private static string? NormalizeSnapshot(string? value, int maxLength)
    {
        var cleaned = value?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, maxLength)];
    }

    private async Task EnsureOrderItemIngredientOverrideSchemaAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.IsNpgsql()) return; // Schema is owned by the deployment migration.
        await _db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.OrderItemIngredients', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'IngredientName') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD IngredientName NVARCHAR(200) NULL;

                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'Unit') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD Unit NVARCHAR(50) NULL;

                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'Note') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD Note NVARCHAR(500) NULL;

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItemIngredients_Ingredients' AND parent_object_id = OBJECT_ID(N'dbo.OrderItemIngredients'))
                    ALTER TABLE dbo.OrderItemIngredients DROP CONSTRAINT FK_OrderItemIngredients_Ingredients;
            END
            """, cancellationToken);
    }

    private async Task<OrderItemResolution> ResolveOrderItemForOverrideAsync(int orderId, int itemId, CancellationToken cancellationToken, bool track = false)
    {
        var query = track ? _db.OrderItems.AsQueryable() : _db.OrderItems.AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.ItemID == itemId && x.OrderID == orderId, cancellationToken);
        if (item is not null)
        {
            return new OrderItemResolution(item, "direct ItemID", 1);
        }

        var dishMatches = await query
            .Where(x => x.OrderID == orderId && x.DishID == itemId)
            .OrderByDescending(x => x.ItemID)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (dishMatches.Count == 1)
        {
            return new OrderItemResolution(dishMatches[0], "fallback DishID", 1);
        }

        return new OrderItemResolution(null, "failed", dishMatches.Count);
    }

    private sealed record OrderItemResolution(OrderItems? Item, string Method, int MatchCount);

    private static bool IsDishOrderable(CatalogApiClient.DishSnapshotResponse? dishSnapshot)
        => dishSnapshot is not null
           && dishSnapshot.IsActive
           && dishSnapshot.Available;

    private async Task<ObjectResult?> ValidateOrderDishAvailabilityAsync(int orderId, CancellationToken cancellationToken)
    {
        // UI can go stale while the customer is browsing, so submit must re-check
        // Catalog availability before anything is allowed to reach the kitchen.
        var orderItems = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderID == orderId)
            .Select(x => new { x.DishID })
            .ToListAsync(cancellationToken);

        if (orderItems.Count == 0)
        {
            return null;
        }

        var dishLookup = ((await _catalogApi.GetDishesAsync(orderItems.Select(x => x.DishID), cancellationToken))
            ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>())
            .ToDictionary(x => x.DishId);

        var unavailableDishNames = orderItems
            .Where(item => !dishLookup.TryGetValue(item.DishID, out var snapshot) || !IsDishOrderable(snapshot))
            .Select(item => NormalizeDishNameForMessage(
                dishLookup.TryGetValue(item.DishID, out var snapshot) ? snapshot.Name : null,
                item.DishID))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        return unavailableDishNames.Length == 0
            ? null
            : BuildDishUnavailableConflict(unavailableDishNames);
    }

    private static ObjectResult BuildDishUnavailableConflict(IReadOnlyCollection<string> dishNames)
    {
        var names = dishNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        var message = names.Length switch
        {
            0 => "Có món hiện đang tạm hết hoặc ngừng bán. Vui lòng cập nhật giỏ hàng trước khi gửi bếp.",
            1 => $"Món \"{names[0]}\" hiện đang tạm hết hoặc ngừng bán. Vui lòng cập nhật giỏ hàng trước khi gửi bếp.",
            _ => $"Một số món hiện đang tạm hết hoặc ngừng bán: {string.Join(", ", names)}. Vui lòng cập nhật giỏ hàng trước khi gửi bếp."
        };

        return new ObjectResult(new
        {
            message,
            unavailableDishNames = names
        })
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    private async Task<ObjectResult?> ValidateIngredientAvailabilityAsync(
        int orderId,
        IReadOnlyList<CatalogApiClient.OrderIngredientConsumptionItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var inventoryCheck = await _catalogApi.ValidateIngredientsForOrderAsync(orderId, items, cancellationToken);
        if (inventoryCheck.Success)
        {
            return null;
        }

        return new ObjectResult(new
        {
            message = inventoryCheck.Message ?? "Không đủ nguyên liệu để tiếp tục gửi món xuống bếp.",
            details = inventoryCheck.Issues.Select(x => new
            {
                ingredientId = x.IngredientId,
                ingredientName = x.IngredientName,
                requiredQuantity = x.RequiredQuantity,
                availableQuantity = x.AvailableQuantity,
                unit = x.Unit
            }).ToArray()
        })
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    private static string NormalizeDishNameForMessage(string? dishName, int dishId)
        => string.IsNullOrWhiteSpace(dishName) ? $"Món #{dishId}" : dishName.Trim();

    private static string? NormalizeSessionCode(string? diningSessionCode)
    {
        var cleaned = diningSessionCode?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, 64)];
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var cleaned = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, 100)];
    }

    private static string GenerateDiningSessionCode()
        => $"SESS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(64, $"SESS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Length)];

    private async Task<string?> GetLatestActiveSessionCodeAsync(int tableId, CancellationToken cancellationToken)
    {
        var linkedSessionCode = await _db.DiningSessionTables
            .Join(_db.Orders, dst => dst.DiningSessionCode, o => o.DiningSessionCode, (dst, o) => new { dst, o })
            .Join(_db.OrderStatus, x => x.o.StatusID, s => s.StatusID, (x, s) => new { x.dst, x.o, s })
            .Where(x =>
                x.dst.TableID == tableId
                && (x.o.IsActive ?? true)
                && ActiveDiningStatuses.Contains(x.s.StatusCode))
            .OrderByDescending(x => x.o.OrderTime)
            .Select(x => x.dst.DiningSessionCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(linkedSessionCode))
        {
            return linkedSessionCode;
        }

        return await _db.Orders
            .Join(_db.OrderStatus, o => o.StatusID, s => s.StatusID, (o, s) => new { o, s })
            .Where(x =>
                x.o.TableID == tableId
                && (x.o.IsActive ?? true)
                && x.o.DiningSessionCode != null
                && ActiveDiningStatuses.Contains(x.s.StatusCode))
            .OrderByDescending(x => x.o.OrderTime)
            .Select(x => x.o.DiningSessionCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<OrderEntity?> GetPendingRoundAsync(int tableId, string? activeSessionCode, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(activeSessionCode))
        {
            var pendingInSession = await _db.Orders
                .Join(_db.OrderStatus, o => o.StatusID, s => s.StatusID, (o, s) => new { o, s })
                .Where(x =>
                    (x.o.IsActive ?? true)
                    && x.o.DiningSessionCode == activeSessionCode
                    && x.s.StatusCode == "PENDING")
                .OrderByDescending(x => x.o.OrderTime)
                .Select(x => x.o)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingInSession is not null)
            {
                return pendingInSession;
            }

            return null;
        }

        return await _db.Orders
            .Join(_db.OrderStatus, o => o.StatusID, s => s.StatusID, (o, s) => new { o, s })
            .Where(x =>
                x.o.TableID == tableId
                && (x.o.IsActive ?? true)
                && x.o.DiningSessionCode == null
                && x.s.StatusCode == "PENDING")
            .OrderByDescending(x => x.o.OrderTime)
            .Select(x => x.o)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<SubmitCommands> CreateSubmitCommandAsync(string idempotencyKey, int tableId, CancellationToken cancellationToken)
    {
        var existingFailed = await _db.SubmitCommands
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey && x.Status == "FAILED", cancellationToken);

        if (existingFailed is not null)
        {
            _db.SubmitCommands.Remove(existingFailed);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var command = new SubmitCommands
        {
            IdempotencyKey = idempotencyKey,
            TableId = tableId,
            Status = "PENDING",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SubmitCommands.Add(command);
        await _db.SaveChangesAsync(cancellationToken);
        return command;
    }

    private async Task MarkSubmitCommandCompletedAsync(SubmitCommands? command, OrderEntity order, CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return;
        }

        command.OrderId = order.OrderID;
        command.TableId = order.TableID ?? command.TableId;
        command.DiningSessionCode = NormalizeSessionCode(order.DiningSessionCode);
        command.Status = "COMPLETED";
        command.CompletedAtUtc = DateTime.UtcNow;
        command.Error = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkSubmitCommandFailedAsync(SubmitCommands? command, string error, CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return;
        }

        command.Status = "FAILED";
        command.CompletedAtUtc = DateTime.UtcNow;
        command.Error = error;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ActionResult<object>?> WaitForSubmitCommandCompletionAsync(string idempotencyKey, int tableId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(SubmitCommandWait);

        while (DateTime.UtcNow <= deadline)
        {
            var command = await _db.SubmitCommands
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

            if (command is null)
            {
                return null;
            }

            if (string.Equals(command.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                if (command.OrderId is int replayOrderId)
                {
                    var replayOrder = await _db.Orders
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.OrderID == replayOrderId, cancellationToken);

                    if (replayOrder is not null)
                    {
                        return Ok(await BuildActiveOrderResponseAsync(replayOrder, cancellationToken));
                    }
                }

                var fallbackOrder = await GetOrCreateOrderAsync(tableId, createIfMissing: false, cancellationToken);
                if (fallbackOrder is not null)
                {
                    return Ok(await BuildActiveOrderResponseAsync(fallbackOrder, cancellationToken));
                }

                return Ok(new { success = true });
            }

            if (string.Equals(command.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await Task.Delay(150, cancellationToken);
        }

        return null;
    }

    private async Task<IDbContextTransaction> BeginTableOperationAsync(int tableId, CancellationToken cancellationToken)
    {
        // Multiple screens can act on the same table at once, so critical writes
        // take a short-lived database application lock before re-validating state.
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireApplicationLockAsync($"restaurant-table:{tableId}", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task AcquireApplicationLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_db.Database.IsNpgsql())
        {
            await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(_db, resource, cancellationToken);
            return;
        }
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
                              DECLARE @result int;
                              EXEC @result = sp_getapplock
                                  @Resource = @resource,
                                  @LockMode = 'Exclusive',
                                  @LockOwner = 'Transaction',
                                  @LockTimeout = @lockTimeout;
                              SELECT @result;
                              """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.Value = resource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@lockTimeout";
        timeoutParameter.Value = TableLockTimeoutMs;
        command.Parameters.Add(timeoutParameter);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
        {
            throw new InvalidOperationException("Không thể khóa bàn để xử lý thao tác. Vui lòng thử lại.");
        }
    }

    private async Task<ActionResult?> EnsureSessionWritableAsync(
        int? orderId,
        string? diningSessionCode,
        string? expectedDiningSessionCode,
        CancellationToken cancellationToken)
    {
        // A stale menu must not silently reopen or append to a session that another
        // actor has already changed or sent to checkout.
        var normalizedExpected = NormalizeSessionCode(expectedDiningSessionCode);
        var normalizedCurrent = NormalizeSessionCode(diningSessionCode);

        if (!string.IsNullOrWhiteSpace(normalizedExpected)
            && !string.Equals(normalizedExpected, normalizedCurrent, StringComparison.Ordinal))
        {
            return Conflict(new
            {
                message = "Phiên bàn đã thay đổi. Vui lòng tải lại để tiếp tục thao tác mới."
            });
        }

        if (string.IsNullOrWhiteSpace(normalizedCurrent))
        {
            return null;
        }

        var checkoutState = await _billingGuard.GetCheckoutStateAsync(orderId, normalizedCurrent, cancellationToken);
        if (checkoutState?.HasCheckoutInProgress == true || checkoutState?.HasCompletedCheckout == true)
        {
            return Conflict(new
            {
                message = checkoutState.Message ?? "Phiên bàn này đang được quầy thu ngân xử lý. Vui lòng tải lại."
            });
        }

        return null;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.GetBaseException() is Npgsql.PostgresException postgres)
            return postgres.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
        var message = ex.GetBaseException().Message ?? string.Empty;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("2601", StringComparison.OrdinalIgnoreCase)
               || message.Contains("2627", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int?> GetSessionCustomerIdAsync(int tableId, string diningSessionCode, CancellationToken cancellationToken)
    {
        return await _db.Orders
            .Where(x =>
                x.TableID == tableId
                && (x.IsActive ?? true)
                && x.DiningSessionCode == diningSessionCode
                && x.CustomerID.HasValue
                && x.CustomerID > 0)
            .OrderByDescending(x => x.OrderTime)
            .Select(x => x.CustomerID)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // An app guest (explicit dining session) may still be seated after the kitchen cancels everything.
    private Task<bool> HasOpenDiningSessionAsync(int tableId, CancellationToken cancellationToken) =>
        _db.DiningSessions.AnyAsync(x => x.TableId == tableId && x.EndedAtUtc == null, cancellationToken);

    private async Task<bool> HasOtherActiveOrdersInSameSessionAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        if (order.TableID is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(order.DiningSessionCode))
        {
            return await _db.Orders.AnyAsync(
                x => x.OrderID != order.OrderID && x.TableID == order.TableID && (x.IsActive ?? true),
                cancellationToken);
        }

        return await _db.Orders.AnyAsync(
            x =>
                x.OrderID != order.OrderID
                && x.TableID == order.TableID
                && (x.IsActive ?? true)
                && x.DiningSessionCode == order.DiningSessionCode,
            cancellationToken);
    }

    private async Task<List<OrderEntity>> GetOrdersForAggregateAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        if (order.TableID is null)
        {
            return [order];
        }

        if (!string.IsNullOrWhiteSpace(order.DiningSessionCode))
        {
            return await _db.Orders
                .AsNoTracking()
                .Where(x =>
                    (x.IsActive ?? true)
                    && x.DiningSessionCode == order.DiningSessionCode)
                .OrderBy(x => x.OrderTime)
                .ToListAsync(cancellationToken);
        }

        return [order];
    }

    private static string ResolveAggregateStatusCode(IEnumerable<OrderEntity> orders, IReadOnlyDictionary<int, StatusRow> statusLookup)
    {
        var statusCodes = orders
            .Select(order => statusLookup.TryGetValue(order.StatusID, out var status) ? status.StatusCode : null)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.ToUpperInvariant())
            .ToArray();

        return ResolveAggregateStatusCodeFromCodes(statusCodes);
    }

    private static string ResolveAggregateStatusCodeFromCodes(IEnumerable<string?> codes)
    {
        var statusCodes = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.ToUpperInvariant())
            .ToArray();

        if (statusCodes.Contains("PENDING")) return "PENDING";
        if (statusCodes.Contains("CONFIRMED")) return "CONFIRMED";
        if (statusCodes.Contains("PREPARING")) return "PREPARING";
        if (statusCodes.Contains("READY")) return "READY";
        if (statusCodes.Contains("SERVING")) return "SERVING";
        if (statusCodes.Contains("COMPLETED")) return "COMPLETED";
        if (statusCodes.Contains("CANCELLED")) return "CANCELLED";
        return "PENDING";
    }

    private static int GetStatusPriority(string? statusCode)
    {
        return (statusCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "PENDING" => 6,
            "CONFIRMED" => 5,
            "PREPARING" => 4,
            "READY" => 3,
            "SERVING" => 2,
            "COMPLETED" => 1,
            "CANCELLED" => 0,
            _ => -1
        };
    }

    public sealed record AddItemRequest(int DishId, int Quantity, string? Note, string? ExpectedDiningSessionCode = null);
    public sealed record SubmitOrderRequest(string? IdempotencyKey, string? ExpectedDiningSessionCode = null);
    public sealed record UpdateQuantityRequest(int Quantity);
    public sealed record UpdateItemNoteRequest(string? Note, bool? Append);
    public sealed record OrderItemIngredientOverrideItem(int IngredientId, string? IngredientName, string? Unit, decimal Quantity);
    public sealed record OrderItemIngredientOverridesResponse(int OrderId, int ItemId, int DishId, IReadOnlyList<OrderItemIngredientOverrideItem> Items);
    public sealed record UpdateOrderItemIngredientOverrideItem(int IngredientId, string? IngredientName, string? Unit, decimal Quantity);
    public sealed record UpdateOrderItemIngredientOverridesRequest(IReadOnlyList<UpdateOrderItemIngredientOverrideItem>? Items, string? Note);
    public sealed record ScanLoyaltyCardRequest(string? PhoneNumber);
    public sealed record UpdateOrderStatusRequest(string? StatusCode);
    public sealed record SubmitOrderBatchRequest(IReadOnlyList<AddItemRequest>? Items, string? CustomerPhoneNumber, string? IdempotencyKey, string? ExpectedDiningSessionCode = null);
    public sealed record LinkDiningSessionTablesRequest(int? PrimaryTableId, IReadOnlyList<int>? TableIds);
    public sealed record CancelOrderRequest(string? Reason);
    private sealed record StatusRow(int StatusID, string StatusCode, string StatusName);
    public sealed record AdminRevenueRowResponse(
        DateOnly Date,
        int BranchId,
        string BranchName,
        int TotalOrders,
        decimal TotalRevenue);
    public sealed record ChefHistoryItemResponse(
        int OrderId,
        string? OrderCode,
        DateTime OrderTime,
        DateTime? CompletedTime,
        string? TableName,
        string? BranchName,
        string StatusCode,
        string StatusName,
        string DishesSummary,
        string? Notes);
    public sealed record AdminRevenueReportResponse(decimal TotalRevenue, IReadOnlyList<AdminRevenueRowResponse> RevenueByBranchDate);
    public sealed record AdminTopDishReportItemResponse(
        int DishId,
        string DishName,
        string CategoryName,
        int TotalQuantity,
        decimal TotalRevenue);
    public sealed record AdminTopDishReportResponse(IReadOnlyList<AdminTopDishReportItemResponse> Items);
}
