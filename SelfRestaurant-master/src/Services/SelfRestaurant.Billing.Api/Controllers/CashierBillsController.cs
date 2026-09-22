using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

[ApiController]
public sealed class CashierBillsController : ControllerBase
{
    private readonly BillingDbContext _db;
    private readonly OrdersApiClient _ordersApi;
    private readonly CustomersApiClient _customersApi;

    public CashierBillsController(BillingDbContext db, OrdersApiClient ordersApi, CustomersApiClient customersApi)
    {
        _db = db;
        _ordersApi = ordersApi;
        _customersApi = customersApi;
    }

    [HttpGet("api/employees/{employeeId:int}/cashier/bills")]
    public async Task<ActionResult<IReadOnlyList<BillSummaryResponse>>> GetBills(
        int employeeId,
        [FromQuery] int branchId,
        [FromQuery] DateOnly? date,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã nhân viên thu ngân." });
        }

        if (branchId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã chi nhánh." });
        }

        take = Math.Clamp(take, 1, 200);

        DateTime? start = null;
        DateTime? end = null;
        if (date is not null)
        {
            start = date.Value.ToDateTime(TimeOnly.MinValue);
            end = start.Value.AddDays(1);
        }

        var bills = await _db.Bills
            .AsNoTracking()
            .Where(b =>
                b.IsActive
                && b.EmployeeID == employeeId
                && (start == null || (b.BillTime >= start && b.BillTime < end)))
            .OrderByDescending(b => b.BillTime)
            .Take(take)
            .ToListAsync(cancellationToken);

        var orderLookup = await GetFallbackOrderContextLookupAsync(bills, cancellationToken);
        var customerLookup = (await _customersApi.GetCustomersAsync(
                bills.Where(b => b.CustomerID.HasValue).Select(b => b.CustomerID!.Value),
                cancellationToken))
            .ToDictionary(x => x.CustomerId);

        var payload = bills
            .Where(b => ResolveBranchId(b, orderLookup) == branchId)
            .Select(b =>
            {
                orderLookup.TryGetValue(b.OrderID, out var orderContext);
                customerLookup.TryGetValue(b.CustomerID ?? 0, out var customer);

                return new BillSummaryResponse(
                    b.BillID,
                    b.BillCode,
                    b.BillTime,
                    b.OrderID,
                    b.OrderCodeSnapshot ?? orderContext?.OrderCode,
                    b.TableNameSnapshot ?? orderContext?.TableName ?? "-",
                    customer?.Name,
                    b.Subtotal,
                    b.Discount,
                    b.PointsDiscount,
                    b.PointsUsed,
                    b.TotalAmount,
                    b.PaymentMethod,
                    b.PaymentAmount,
                    b.ChangeAmount);
            })
            .ToList();

        return Ok(payload);
    }

    [HttpGet("api/internal/employees/{employeeId:int}/cashier/history")]
    public async Task<ActionResult<object>> GetInternalCashierHistory(
        int employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã nhân viên thu ngân." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        var fromDate = DateTime.Today.AddDays(-days);

        var query = _db.Bills
            .AsNoTracking()
            .Where(b => b.IsActive && b.EmployeeID == employeeId && b.BillTime >= fromDate);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var bills = await query
            .OrderByDescending(b => b.BillTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderLookup = await GetFallbackOrderContextLookupAsync(bills, cancellationToken);
        var customerLookup = (await _customersApi.GetCustomersAsync(
                bills.Where(b => b.CustomerID.HasValue).Select(b => b.CustomerID!.Value),
                cancellationToken))
            .ToDictionary(x => x.CustomerId);

        var items = bills.Select(b =>
        {
            orderLookup.TryGetValue(b.OrderID, out var orderContext);
            customerLookup.TryGetValue(b.CustomerID ?? 0, out var customer);

            return new BillSummaryResponse(
                b.BillID,
                b.BillCode,
                b.BillTime,
                b.OrderID,
                b.OrderCodeSnapshot ?? orderContext?.OrderCode,
                b.TableNameSnapshot ?? orderContext?.TableName ?? "-",
                customer?.Name,
                b.Subtotal,
                b.Discount,
                b.PointsDiscount,
                b.PointsUsed,
                b.TotalAmount,
                b.PaymentMethod,
                b.PaymentAmount,
                b.ChangeAmount);
        }).ToList();

        return Ok(new { page, pageSize, totalItems, totalPages, items });
    }

    [HttpGet("api/employees/{employeeId:int}/cashier/report")]
    public async Task<ActionResult<CashierReportResponse>> GetReport(
        int employeeId,
        [FromQuery] int branchId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var target = date ?? DateOnly.FromDateTime(DateTime.Today);

        var bills = await GetBills(employeeId, branchId, target, take: 200, cancellationToken);
        if (bills.Result is ObjectResult { StatusCode: >= 400 } bad)
        {
            return bad;
        }

        var list = bills.Value
            ?? (bills.Result as OkObjectResult)?.Value as IReadOnlyList<BillSummaryResponse>
            ?? Array.Empty<BillSummaryResponse>();
        var revenue = list.Sum(b => b.TotalAmount);

        return Ok(new CashierReportResponse(
            Date: target,
            EmployeeId: employeeId,
            BranchId: branchId,
            BillCount: list.Count,
            TotalRevenue: revenue,
            Bills: list));
    }

    [HttpGet("api/branches/{branchId:int}/cashier/report")]
    public async Task<ActionResult<BranchCashierReportResponse>> GetBranchReport(
        int branchId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        if (branchId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã chi nhánh." });
        }

        var target = date ?? DateOnly.FromDateTime(DateTime.Today);
        var start = target.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        var bills = await _db.Bills
            .AsNoTracking()
            .Where(b =>
                b.IsActive
                && b.BillTime >= start
                && b.BillTime < end)
            .ToListAsync(cancellationToken);

        var orderLookup = await GetFallbackOrderContextLookupAsync(bills, cancellationToken);

        var filteredBills = bills
            .Where(b => ResolveBranchId(b, orderLookup) == branchId)
            .ToList();

        return Ok(new BranchCashierReportResponse(
            Date: target,
            BranchId: branchId,
            BillCount: filteredBills.Count,
            TotalRevenue: filteredBills.Sum(b => b.TotalAmount)));
    }

    private async Task<IReadOnlyDictionary<int, OrdersApiClient.OrderBillContextResponse>> GetFallbackOrderContextLookupAsync(
        IReadOnlyList<Persistence.Entities.Bills> bills,
        CancellationToken cancellationToken)
    {
        var missingOrderIds = bills
            .Where(b => b.BranchIdSnapshot is null || string.IsNullOrWhiteSpace(b.TableNameSnapshot) || string.IsNullOrWhiteSpace(b.OrderCodeSnapshot))
            .Select(b => b.OrderID)
            .Distinct()
            .ToArray();

        if (missingOrderIds.Length == 0)
        {
            return new Dictionary<int, OrdersApiClient.OrderBillContextResponse>();
        }

        return (await _ordersApi.GetOrderBillContextsAsync(missingOrderIds, cancellationToken))
            .ToDictionary(x => x.OrderId);
    }

    private static int? ResolveBranchId(
        Persistence.Entities.Bills bill,
        IReadOnlyDictionary<int, OrdersApiClient.OrderBillContextResponse> orderLookup)
    {
        if (bill.BranchIdSnapshot.HasValue)
        {
            return bill.BranchIdSnapshot.Value;
        }

        return orderLookup.TryGetValue(bill.OrderID, out var orderContext) ? orderContext.BranchId : null;
    }

    public sealed record BillSummaryResponse(
        int BillId,
        string BillCode,
        DateTime BillTime,
        int OrderId,
        string? OrderCode,
        string TableName,
        string? CustomerName,
        decimal Subtotal,
        decimal Discount,
        decimal PointsDiscount,
        int? PointsUsed,
        decimal TotalAmount,
        string PaymentMethod,
        decimal? PaymentAmount,
        decimal? ChangeAmount);

    public sealed record CashierReportResponse(
        DateOnly Date,
        int EmployeeId,
        int BranchId,
        int BillCount,
        decimal TotalRevenue,
        IReadOnlyList<BillSummaryResponse> Bills);

    public sealed record BranchCashierReportResponse(
        DateOnly Date,
        int BranchId,
        int BillCount,
        decimal TotalRevenue);
}
