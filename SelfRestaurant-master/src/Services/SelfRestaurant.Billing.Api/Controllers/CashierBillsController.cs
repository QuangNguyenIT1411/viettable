using System.Text.Json;
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
            start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
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

        var appInvoices = await GetAppInvoiceRowsAsync(employeeId, branchId, start, end, take, cancellationToken);
        return Ok(payload.Concat(appInvoices).OrderByDescending(x => x.BillTime).Take(take).ToList());
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

        // Legacy Bills and app Invoices are merged newest-first; take enough of each to fill this page.
        var window = page * pageSize;
        var appInvoices = await GetAppInvoiceRowsAsync(employeeId, null, fromDate, null, window, cancellationToken);
        var totalItems = await query.CountAsync(cancellationToken)
            + await AppInvoicesInRange(employeeId, null, fromDate, null).CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var bills = await query
            .OrderByDescending(b => b.BillTime)
            .Take(window)
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
        }).Concat(appInvoices)
            .OrderByDescending(x => x.BillTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

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
        var start = target.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
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

        // Every app invoice of the branch counts (cashier-confirmed or online); app sessions never create Bills.
        var appTotals = await AppInvoicesInRange(null, branchId, start, end)
            .Select(x => x.Invoice.TotalAmount)
            .ToListAsync(cancellationToken);

        return Ok(new BranchCashierReportResponse(
            Date: target,
            BranchId: branchId,
            BillCount: filteredBills.Count + appTotals.Count,
            TotalRevenue: filteredBills.Sum(b => b.TotalAmount) + appTotals.Sum()));
    }

    // App (mobile checkout) invoices. Bills use server-local BillTime; Invoices use UTC.
    // A cashier owns an invoice when one of its SUCCEEDED payments was confirmed by them.
    private IQueryable<AppInvoice> AppInvoicesInRange(int? employeeId, int? branchId, DateTime? fromLocal, DateTime? toLocal)
    {
        var invoices = _db.Invoices.AsNoTracking();
        if (fromLocal is not null)
        {
            var fromUtc = ToUtc(fromLocal.Value);
            invoices = invoices.Where(i => i.IssuedAtUtc >= fromUtc);
        }
        if (toLocal is not null)
        {
            var toUtc = ToUtc(toLocal.Value);
            invoices = invoices.Where(i => i.IssuedAtUtc < toUtc);
        }
        if (employeeId is not null)
            invoices = invoices.Where(i => _db.Payments.Any(p => p.CheckoutQuoteId == i.CheckoutQuoteId
                && p.Status == "SUCCEEDED" && p.ConfirmedByEmployeeId == employeeId));
        var quotes = _db.CheckoutQuotes.AsNoTracking();
        if (branchId is not null) quotes = quotes.Where(q => q.BranchId == branchId);
        return from invoice in invoices
               join quote in quotes on invoice.CheckoutQuoteId equals quote.CheckoutQuoteId
               select new AppInvoice { Invoice = invoice, Quote = quote };
    }

    private async Task<List<BillSummaryResponse>> GetAppInvoiceRowsAsync(
        int? employeeId, int? branchId, DateTime? fromLocal, DateTime? toLocal, int take, CancellationToken cancellationToken)
    {
        var rows = await AppInvoicesInRange(employeeId, branchId, fromLocal, toLocal)
            .OrderByDescending(x => x.Invoice.IssuedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return new List<BillSummaryResponse>();

        var paymentIds = rows.Select(x => x.Invoice.PaymentId).Distinct().ToArray();
        var methods = await _db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.PaymentId))
            .ToDictionaryAsync(p => p.PaymentId, p => p.Method, cancellationToken);
        var orderIds = rows.ToDictionary(x => x.Invoice.InvoiceId, x => FirstOrderId(x.Invoice.ItemsJson));
        var orderLookup = (await _ordersApi.GetOrderBillContextsAsync(
                orderIds.Values.Where(id => id > 0).Distinct().ToArray(), cancellationToken))
            .ToDictionary(x => x.OrderId);
        var customerLookup = (await _customersApi.GetCustomersAsync(rows.Select(x => x.Invoice.CustomerId), cancellationToken))
            .ToDictionary(x => x.CustomerId);

        return rows.Select(x =>
        {
            var orderId = orderIds[x.Invoice.InvoiceId];
            orderLookup.TryGetValue(orderId, out var order);
            customerLookup.TryGetValue(x.Invoice.CustomerId, out var customer);
            return new BillSummaryResponse(
                -x.Invoice.InvoiceId, // negative: never collides with a legacy BillID used as a row key
                $"Hóa đơn #{x.Invoice.InvoiceId}",
                x.Invoice.IssuedAtUtc.ToLocalTime(),
                orderId,
                order?.OrderCode,
                order?.TableName ?? "-",
                customer?.Name,
                x.Quote.Subtotal,
                x.Quote.VoucherDiscount,
                x.Quote.PointsDiscount,
                x.Quote.PointsApplied,
                x.Invoice.TotalAmount,
                methods.GetValueOrDefault(x.Invoice.PaymentId) ?? "APP",
                null,
                null);
        }).ToList();
    }

    private static DateTime ToUtc(DateTime local) =>
        local.Kind == DateTimeKind.Utc ? local : DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    private static int FirstOrderId(string itemsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;
            foreach (var item in doc.RootElement.EnumerateArray())
                foreach (var property in item.EnumerateObject())
                    if (property.NameEquals("OrderId") || property.NameEquals("orderId"))
                        return property.Value.TryGetInt32(out var id) ? id : 0;
        }
        catch (JsonException) { }
        return 0;
    }

    private sealed class AppInvoice
    {
        public Persistence.Entities.Invoice Invoice { get; init; } = null!;
        public Persistence.Entities.CheckoutQuote Quote { get; init; } = null!;
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
