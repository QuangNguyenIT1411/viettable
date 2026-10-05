using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SelfRestaurant.Billing.Api.Infrastructure.Auditing;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Eventing;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Controllers;

[ApiController]
public sealed class CashierController : ControllerBase
{
    private static readonly string[] ActiveStatusCodes = ["PENDING", "CONFIRMED", "PREPARING", "READY", "SERVING"];
    private static readonly TimeSpan CheckoutCommandWait = TimeSpan.FromSeconds(5);
    private const int TableLockTimeoutMs = 5000;

    private readonly BillingDbContext _db;
    private readonly CustomersApiClient _customersApi;
    private readonly OrdersApiClient _ordersApi;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly BusinessAuditLogger _auditLogger;
    private readonly IHostEnvironment _environment;

    public CashierController(BillingDbContext db, CustomersApiClient customersApi, OrdersApiClient ordersApi, IIntegrationEventPublisher eventPublisher, BusinessAuditLogger auditLogger, IHostEnvironment environment)
    {
        _db = db;
        _customersApi = customersApi;
        _ordersApi = ordersApi;
        _eventPublisher = eventPublisher;
        _auditLogger = auditLogger;
        _environment = environment;
    }

    [HttpGet("api/branches/{branchId:int}/cashier/orders")]
    public async Task<ActionResult<IReadOnlyList<CashierOrderResponse>>> GetCashierOrders(
        int branchId,
        CancellationToken cancellationToken)
    {
        var billedOrderIds = await _db.Bills
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => b.OrderID)
            .Distinct()
            .ToListAsync(cancellationToken);

        var billedOrderLookup = billedOrderIds.ToHashSet();

        var orders = (await _ordersApi.GetCashierOrdersAsync(branchId, cancellationToken))
            .Where(o => !billedOrderLookup.Contains(o.OrderId))
            .ToList();
        var customerLookup = (await _customersApi.GetCustomersAsync(
                orders.Where(o => o.CustomerId.HasValue).Select(o => o.CustomerId!.Value),
                cancellationToken))
            .ToDictionary(x => x.CustomerId);

        var payload = orders
            .Select(o =>
            {
                customerLookup.TryGetValue(o.CustomerId ?? 0, out var customer);

                var items = o.Items
                    .Select(i => new CashierOrderItemResponse(
                        i.ItemId,
                        i.OrderId,
                        i.DishId,
                        i.DishName,
                        i.Quantity,
                        i.UnitPrice,
                        i.LineTotal,
                        i.Image,
                        i.Note,
                        i.StatusCode))
                    .ToList();

                return new CashierOrderResponse(
                    o.OrderId,
                    o.OrderCode,
                    o.OrderTime,
                    o.TableId,
                    o.TableName,
                    o.CustomerId,
                    customer?.Name,
                    customer?.LoyaltyPoints ?? 0,
                    o.StatusCode,
                    o.StatusName,
                    o.Subtotal,
                    o.ItemCount,
                    items);
            })
            .ToList();

        return Ok(payload);
    }

    [HttpPost("api/orders/{orderId:int}/checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        int orderId,
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "Thiếu khóa xác nhận thanh toán." });
        }

        var replay = await WaitForCheckoutCommandCompletionAsync(idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        CheckoutCommands? command = null;
        try
        {
            command = await CreateCheckoutCommandAsync(idempotencyKey, orderId, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var duplicateReplay = await WaitForCheckoutCommandCompletionAsync(idempotencyKey, cancellationToken);
            if (duplicateReplay is not null)
            {
                return duplicateReplay;
            }

            return Conflict(new { message = "Yêu cầu thanh toán đang được xử lý. Vui lòng thử lại sau." });
        }

        var initialOrder = await _ordersApi.GetCheckoutContextAsync(orderId, cancellationToken);
        if (initialOrder is null)
        {
            await MarkCheckoutCommandFailedAsync(command, "Không tìm thấy đơn hàng.", cancellationToken);
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        }

        if (!string.IsNullOrWhiteSpace(initialOrder.DiningSessionCode)
            && await _db.CheckoutQuotes.AnyAsync(x => x.DiningSessionCode == initialOrder.DiningSessionCode, cancellationToken))
        {
            await MarkCheckoutCommandFailedAsync(command, "Phiên ứng dụng phải xác nhận qua giao dịch mobile.", cancellationToken);
            return Conflict(new { code = "mobile_payment_required", message = "Phiên này sử dụng thanh toán ứng dụng. Hãy xác nhận giao dịch ở mục Thanh toán ứng dụng tại chỗ." });
        }

        await using var tableLock = await BeginTableOperationAsync(initialOrder.TableId, cancellationToken);

        var alreadyBilled = await _db.Bills
            .AsNoTracking()
            .AnyAsync(b => b.OrderID == orderId && b.IsActive, cancellationToken);
        if (alreadyBilled)
        {
            await MarkCheckoutCommandFailedAsync(command, "Đơn hàng này đã được thanh toán.", cancellationToken);
            return Conflict(new { message = "Đơn hàng này đã được thanh toán." });
        }

        var employeeId = request.EmployeeId;
        if (employeeId <= 0)
        {
            await MarkCheckoutCommandFailedAsync(command, "Thiếu thông tin nhân viên thu ngân.", cancellationToken);
            return BadRequest(new { message = "Thiếu thông tin nhân viên thu ngân." });
        }

        var order = await _ordersApi.GetCheckoutContextAsync(orderId, cancellationToken);
        if (order is null)
        {
            await MarkCheckoutCommandFailedAsync(command, "Không tìm thấy đơn hàng.", cancellationToken);
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        }

        if (!order.IsActive || !ActiveStatusCodes.Contains(order.StatusCode))
        {
            await MarkCheckoutCommandFailedAsync(command, "Đơn hàng không còn hoạt động.", cancellationToken);
            return BadRequest(new { message = "Đơn hàng không còn hoạt động." });
        }

        var subtotal = order.Subtotal;
        var discount = request.Discount < 0 ? 0 : request.Discount;
        if (discount > subtotal)
        {
            discount = subtotal;
        }

        var baseTotal = subtotal - discount;
        CustomersApiClient.CustomerSnapshotResponse? customerSnapshot = null;
        if (order.CustomerId is int customerId && customerId > 0)
        {
            customerSnapshot = await _customersApi.GetCustomerAsync(customerId, cancellationToken);
            if (customerSnapshot is null)
            {
                await MarkCheckoutCommandFailedAsync(command, "Không thể đồng bộ dữ liệu khách hàng từ Customers API.", cancellationToken);
                return Problem("Không thể đồng bộ dữ liệu khách hàng từ Customers API.", statusCode: StatusCodes.Status502BadGateway);
            }
        }

        var pointsBefore = customerSnapshot?.LoyaltyPoints ?? 0;
        var requestedPoints = request.PointsUsed < 0 ? 0 : request.PointsUsed;
        var usedPoints = 0;
        if (customerSnapshot is not null && requestedPoints > 0)
        {
            var maxByCustomerBalance = customerSnapshot.LoyaltyPoints;
            var maxByPolicy = (int)Math.Floor(baseTotal * 0.10m);
            var maxPointsAllowed = Math.Min(maxByCustomerBalance, maxByPolicy);
            maxPointsAllowed = (maxPointsAllowed / 1000) * 1000;

            usedPoints = Math.Min(requestedPoints, maxPointsAllowed);
            usedPoints = (usedPoints / 1000) * 1000;
        }

        var pointsDiscount = (decimal)usedPoints;
        var totalAmount = baseTotal - pointsDiscount;
        if (totalAmount < 0)
        {
            totalAmount = 0;
        }

        var paymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod.Trim().ToUpperInvariant();
        if (paymentMethod == "CARD")
        {
            paymentMethod = "QR";
        }

        if (paymentMethod is not ("CASH" or "QR" or "TRANSFER"))
        {
            await MarkCheckoutCommandFailedAsync(command, "Phương thức thanh toán không hợp lệ.", cancellationToken);
            return BadRequest(new { message = "Phương thức thanh toán không hợp lệ." });
        }

        var paymentAmount = request.PaymentAmount;
        if (paymentMethod == "CASH")
        {
            if (paymentAmount < totalAmount)
            {
                await MarkCheckoutCommandFailedAsync(command, "Số tiền khách đưa không đủ để thanh toán hóa đơn.", cancellationToken);
                return BadRequest(new { message = "Số tiền khách đưa không đủ để thanh toán hóa đơn." });
            }
        }
        else
        {
            paymentAmount = totalAmount;
        }

        var changeAmount = paymentMethod == "CASH" ? paymentAmount - totalAmount : 0;
        _auditLogger.Add(
            actionType: "CHECKOUT_STARTED",
            entityType: "DINING_SESSION",
            entityId: string.IsNullOrWhiteSpace(order.DiningSessionCode) ? order.OrderId.ToString() : order.DiningSessionCode,
            tableId: order.TableId,
            orderId: order.OrderId,
            diningSessionCode: order.DiningSessionCode,
            idempotencyKey: idempotencyKey,
            beforeState: new { status = order.StatusCode, subtotal },
            afterState: new { status = "CHECKOUT_IN_PROGRESS", totalAmount, paymentMethod, employeeId });

        var billCode = await GenerateBillCodeAsync(cancellationToken);

        var bill = new Bills
        {
            OrderID = order.OrderId,
            DiningSessionCode = order.DiningSessionCode,
            CheckoutIdempotencyKey = idempotencyKey,
            BillCode = billCode,
            OrderCodeSnapshot = order.OrderCode,
            TableIdSnapshot = order.TableId,
            TableNameSnapshot = order.TableName,
            BranchIdSnapshot = order.BranchId,
            BranchNameSnapshot = order.BranchName,
            BillTime = DateTime.Now,
            Subtotal = subtotal,
            Discount = discount,
            PointsDiscount = pointsDiscount,
            PointsUsed = usedPoints > 0 ? (int?)usedPoints : null,
            TotalAmount = totalAmount,
            PaymentMethod = paymentMethod,
            PaymentAmount = paymentAmount,
            ChangeAmount = changeAmount,
            EmployeeID = employeeId,
            CustomerID = order.CustomerId,
            IsActive = true,
        };

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(cancellationToken);
        _auditLogger.Add(
            actionType: "BILL_CREATED",
            entityType: "BILL",
            entityId: bill.BillID.ToString(),
            tableId: order.TableId,
            orderId: order.OrderId,
            billId: bill.BillID,
            diningSessionCode: order.DiningSessionCode,
            idempotencyKey: idempotencyKey,
            beforeState: new { status = "PENDING" },
            afterState: new
            {
                billCode,
                totalAmount,
                paymentMethod,
                pointsUsed = usedPoints,
                pointsBefore,
                customerId = order.CustomerId
            });
        // Checkout can be double-clicked or retried after a timeout, so we persist
        // the completed billing result before any follow-up side effects.
        await MarkCheckoutCommandCompletedAsync(
            command,
            bill,
            order.DiningSessionCode,
            pointsUsed: usedPoints,
            pointsEarned: 0,
            customerPoints: customerSnapshot?.LoyaltyPoints ?? 0,
            customerName: customerSnapshot?.Name,
            pointsBefore: pointsBefore,
            cancellationToken: cancellationToken);
        var earnedPoints = 0;
        var customerPointsAfter = customerSnapshot?.LoyaltyPoints ?? 0;
        var customerName = customerSnapshot?.Name;
        if (order.CustomerId is int settlementCustomerId && settlementCustomerId > 0)
        {
            var settlement = await _customersApi.SettleLoyaltyAsync(
                settlementCustomerId,
                new CustomersApiClient.LoyaltySettlementRequest(usedPoints, totalAmount),
                cancellationToken);

            if (settlement is null)
            {
                return Problem("Không thể đồng bộ điểm tích lũy với Customers API.", statusCode: StatusCodes.Status502BadGateway);
            }

            usedPoints = settlement.PointsUsed;
            earnedPoints = settlement.PointsEarned;
            customerPointsAfter = settlement.CustomerPoints;
            customerName = settlement.CustomerName;
            await MarkCheckoutCommandCompletedAsync(
                command,
                bill,
                order.DiningSessionCode,
                pointsUsed: usedPoints,
                pointsEarned: earnedPoints,
                customerPoints: customerPointsAfter,
                customerName: customerName,
                pointsBefore: pointsBefore,
                cancellationToken: cancellationToken);
        }

        _auditLogger.Add(
            actionType: "CHECKOUT_COMPLETED",
            entityType: "BILL",
            entityId: bill.BillID.ToString(),
            tableId: order.TableId,
            orderId: order.OrderId,
            billId: bill.BillID,
            diningSessionCode: order.DiningSessionCode,
            idempotencyKey: idempotencyKey,
            beforeState: new { status = "PENDING" },
            afterState: new
            {
                status = "COMPLETED",
                billCode,
                totalAmount,
                paymentMethod,
                pointsUsed = usedPoints,
                pointsEarned = earnedPoints,
                customerPointsAfter,
                employeeId
            });
        await _db.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(new IntegrationEventEnvelope(
            EventName: "payment.completed.v1",
            OccurredAtUtc: DateTime.UtcNow,
            Source: "Billing.Api",
            CorrelationId: HttpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier,
            Payload: new
            {
                orderId = order.OrderId,
                billCode,
                tableId = order.TableId,
                customerId = order.CustomerId,
                employeeId,
                subtotal,
                discount,
                pointsUsed = usedPoints,
                pointsEarned = earnedPoints,
                totalAmount,
                paymentMethod
            }), cancellationToken);

        await tableLock.CommitAsync(cancellationToken);

        return Ok(new CheckoutResponse(
            BillCode: billCode,
            TotalAmount: totalAmount,
            ChangeAmount: changeAmount,
            PointsUsed: usedPoints,
            PointsEarned: earnedPoints,
            CustomerPoints: customerPointsAfter,
            CustomerName: customerName,
            PointsBefore: pointsBefore));
    }

    [HttpGet("api/internal/audit-logs")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? orderId,
        [FromQuery] int? tableId,
        [FromQuery] int? billId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var query = _db.BusinessAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(x => x.EntityId == entityId.Trim());
        }

        if (orderId is > 0)
        {
            query = query.Where(x => x.OrderId == orderId.Value);
        }

        if (tableId is > 0)
        {
            query = query.Where(x => x.TableId == tableId.Value);
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

    [HttpGet("api/internal/checkout-state")]
    public async Task<ActionResult<object>> GetCheckoutState(
        [FromQuery] int? orderId,
        [FromQuery] string? diningSessionCode,
        CancellationToken cancellationToken)
    {
        if ((!orderId.HasValue || orderId.Value <= 0) && string.IsNullOrWhiteSpace(diningSessionCode))
        {
            return BadRequest(new { message = "Thiếu thông tin kiểm tra thanh toán." });
        }

        var normalizedSessionCode = string.IsNullOrWhiteSpace(diningSessionCode) ? null : diningSessionCode.Trim();
        // This guard runs inside shared write paths from Orders, so the query must stay
        // SQL-translatable on older local databases and not rely on client-side string comparison.
        var hasPendingCheckout = await _db.CheckoutCommands
            .AsNoTracking()
            .AnyAsync(x =>
                (x.Status == "PENDING" || x.Status == "COMPLETED")
                && ((orderId.HasValue && x.OrderId == orderId.Value)
                    || (!string.IsNullOrWhiteSpace(normalizedSessionCode) && x.DiningSessionCode == normalizedSessionCode)),
                cancellationToken);

        var hasCompletedCheckout = await _db.Bills
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive
                && ((orderId.HasValue && x.OrderID == orderId.Value)
                    || (!string.IsNullOrWhiteSpace(normalizedSessionCode) && x.DiningSessionCode == normalizedSessionCode)),
                cancellationToken);

        return Ok(new
        {
            hasCheckoutInProgress = hasPendingCheckout,
            hasCompletedCheckout,
            message = hasCompletedCheckout
                ? "Phiên bàn này đã được thanh toán."
                : hasPendingCheckout
                    ? "Phiên bàn này đang được quầy thu ngân xử lý thanh toán."
                    : null
        });
    }

    [HttpPost("api/dev/reset-test-state")]
    public async Task<ActionResult<object>> ResetDevTestState(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var bills = await _db.Bills.ToListAsync(cancellationToken);
        var checkoutCommands = await _db.CheckoutCommands.ToListAsync(cancellationToken);
        var snapshots = await _db.OrderContextSnapshots.ToListAsync(cancellationToken);
        var outboxEvents = await _db.OutboxEvents.ToListAsync(cancellationToken);
        var splitParts = await _db.SplitBillParts.ToListAsync(cancellationToken);
        var splitBills = await _db.SplitBills.ToListAsync(cancellationToken);
        var invoices = await _db.Invoices.ToListAsync(cancellationToken);
        var payments = await _db.Payments.ToListAsync(cancellationToken);
        var quotes = await _db.CheckoutQuotes.ToListAsync(cancellationToken);

        if (splitParts.Count > 0) _db.SplitBillParts.RemoveRange(splitParts);
        if (splitBills.Count > 0) _db.SplitBills.RemoveRange(splitBills);
        if (invoices.Count > 0) _db.Invoices.RemoveRange(invoices);
        if (payments.Count > 0) _db.Payments.RemoveRange(payments);
        if (quotes.Count > 0) _db.CheckoutQuotes.RemoveRange(quotes);

        if (bills.Count > 0)
        {
            _db.Bills.RemoveRange(bills);
        }

        if (checkoutCommands.Count > 0)
        {
            _db.CheckoutCommands.RemoveRange(checkoutCommands);
        }

        if (snapshots.Count > 0)
        {
            _db.OrderContextSnapshots.RemoveRange(snapshots);
        }

        if (outboxEvents.Count > 0)
        {
            _db.OutboxEvents.RemoveRange(outboxEvents);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            clearedBills = bills.Count,
            clearedCheckoutCommands = checkoutCommands.Count,
            clearedOrderSnapshots = snapshots.Count,
            clearedOutboxEvents = outboxEvents.Count
        });
    }

    private async Task<string> GenerateBillCodeAsync(CancellationToken cancellationToken)
    {
        var dateCode = DateTime.Now.ToString("yyyyMMdd");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var random = Random.Shared.Next(1000, 9999);
            var code = $"BILL-{dateCode}-{random}";

            var exists = await _db.Bills.AnyAsync(b => b.BillCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"BILL-{dateCode}-{Guid.NewGuid():N}";
    }

    private async Task<CheckoutCommands> CreateCheckoutCommandAsync(string idempotencyKey, int orderId, CancellationToken cancellationToken)
    {
        var existingFailed = await _db.CheckoutCommands
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey && x.Status == "FAILED", cancellationToken);

        if (existingFailed is not null)
        {
            _db.CheckoutCommands.Remove(existingFailed);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var command = new CheckoutCommands
        {
            IdempotencyKey = idempotencyKey,
            OrderId = orderId,
            Status = "PENDING",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.CheckoutCommands.Add(command);
        await _db.SaveChangesAsync(cancellationToken);
        return command;
    }

    private async Task MarkCheckoutCommandCompletedAsync(
        CheckoutCommands? command,
        Bills bill,
        string? diningSessionCode,
        int pointsUsed,
        int pointsEarned,
        int customerPoints,
        string? customerName,
        int pointsBefore,
        CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return;
        }

        command.BillId = bill.BillID;
        command.OrderId = bill.OrderID;
        command.DiningSessionCode = diningSessionCode;
        command.BillCode = bill.BillCode;
        command.TotalAmount = bill.TotalAmount;
        command.ChangeAmount = bill.ChangeAmount;
        command.PointsUsed = pointsUsed;
        command.PointsEarned = pointsEarned;
        command.CustomerPoints = customerPoints;
        command.CustomerName = customerName;
        command.PointsBefore = pointsBefore;
        command.Status = "COMPLETED";
        command.CompletedAtUtc = DateTime.UtcNow;
        command.Error = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkCheckoutCommandFailedAsync(CheckoutCommands? command, string error, CancellationToken cancellationToken)
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

    private async Task<ActionResult<CheckoutResponse>?> WaitForCheckoutCommandCompletionAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(CheckoutCommandWait);

        while (DateTime.UtcNow <= deadline)
        {
            var command = await _db.CheckoutCommands
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

            if (command is null)
            {
                return null;
            }

            if (string.Equals(command.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                if (command.BillId is int billId)
                {
                    var bill = await _db.Bills
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.BillID == billId, cancellationToken);

                    if (bill is not null)
                    {
                        return Ok(new CheckoutResponse(
                            BillCode: command.BillCode ?? bill.BillCode,
                            TotalAmount: command.TotalAmount ?? bill.TotalAmount,
                            ChangeAmount: command.ChangeAmount ?? bill.ChangeAmount ?? 0,
                            PointsUsed: command.PointsUsed ?? bill.PointsUsed ?? 0,
                            PointsEarned: command.PointsEarned ?? 0,
                            CustomerPoints: command.CustomerPoints ?? 0,
                            CustomerName: command.CustomerName,
                            PointsBefore: command.PointsBefore ?? 0));
                    }
                }

                return Ok(new CheckoutResponse(string.Empty, 0, 0, 0, 0, 0, null, 0));
            }

            if (string.Equals(command.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await Task.Delay(150, cancellationToken);
        }

        return null;
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var cleaned = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, 100)];
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

    private async Task<IDbContextTransaction> BeginTableOperationAsync(int? tableId, CancellationToken cancellationToken)
    {
        // Checkout shares table state with Orders, so we serialize the critical
        // section on the same table resource instead of trusting stale reads.
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (tableId is > 0)
            {
                await AcquireApplicationLockAsync($"restaurant-table:{tableId.Value}", cancellationToken);
            }

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
            throw new InvalidOperationException("Không thể khóa bàn để xử lý thanh toán. Vui lòng thử lại.");
        }
    }

    public sealed record CashierOrderItemResponse(
        int ItemId,
        int OrderId,
        int DishId,
        string DishName,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string? Image,
        string? Note,
        string StatusCode);

    public sealed record CashierOrderResponse(
        int OrderId,
        string? OrderCode,
        DateTime OrderTime,
        int TableId,
        string TableName,
        int? CustomerId,
        string? CustomerName,
        int CustomerPoints,
        string StatusCode,
        string StatusName,
        decimal Subtotal,
        int ItemCount,
        IReadOnlyList<CashierOrderItemResponse> Items);

    public sealed record CheckoutRequest(
        int EmployeeId,
        decimal Discount = 0,
        int PointsUsed = 0,
        string PaymentMethod = "CASH",
        decimal PaymentAmount = 0,
        string? IdempotencyKey = null);

    public sealed record CheckoutResponse(
        string BillCode,
        decimal TotalAmount,
        decimal ChangeAmount,
        int PointsUsed,
        int PointsEarned,
        int CustomerPoints,
        string? CustomerName,
        int PointsBefore);
}
