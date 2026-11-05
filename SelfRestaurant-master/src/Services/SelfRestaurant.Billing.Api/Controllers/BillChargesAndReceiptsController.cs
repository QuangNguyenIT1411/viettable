using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// Service fee / VAT per branch for Admin (owner 05/11). Reached only through the Gateway's admin routes, like promotions.
/// </summary>
[ApiController]
[Route("api/internal/branch-charges")]
public sealed class InternalBranchChargesController(BillingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct)
    {
        var rows = await db.BranchChargeSettings.AsNoTracking().OrderBy(x => x.BranchId).ToListAsync(ct);
        return Ok(new { items = rows.Select(Map), maxServiceFeePercent = CheckoutPricing.MaxServiceFeePercent, maxVatPercent = CheckoutPricing.MaxVatPercent });
    }

    [HttpGet("{branchId:int}")]
    public async Task<ActionResult<object>> Get(int branchId, CancellationToken ct)
    {
        var (fee, vat) = await BranchCharges.ForBranchAsync(db, branchId, ct);
        return Ok(new { branchId, serviceFeePercent = fee, vatPercent = vat });
    }

    public sealed record SaveRequest(decimal ServiceFeePercent, decimal VatPercent, int? EmployeeId, string? EmployeeName);

    [HttpPut("{branchId:int}")]
    public async Task<ActionResult<object>> Save(int branchId, SaveRequest request, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest(new { code = "validation_failed", message = "Chi nhánh không hợp lệ." });
        if (CheckoutPricing.ChargeSettingsError(request.ServiceFeePercent, request.VatPercent) is { } error)
            return BadRequest(new { code = "validation_failed", message = error });
        var row = await db.BranchChargeSettings.FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (row is null) { row = new BranchChargeSetting { BranchId = branchId }; db.BranchChargeSettings.Add(row); }
        row.ServiceFeePercent = request.ServiceFeePercent;
        row.VatPercent = request.VatPercent;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByEmployeeId = request.EmployeeId;
        row.UpdatedByName = BranchCharges.Trim(request.EmployeeName, 200);
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    private static object Map(BranchChargeSetting x) => new
    {
        x.BranchId, x.ServiceFeePercent, x.VatPercent, x.UpdatedAtUtc, x.UpdatedByEmployeeId, x.UpdatedByName
    };
}

/// <summary>
/// "PHIẾU THANH TOÁN" for the cashier screen (owner 05/11): one paid bill of the cashier's branch, read from what was
/// stored when it was paid. Positive id = counter bill (Bills), negative id = app invoice (the cashier history's row key).
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "CashierService", Roles = "CASHIER,MANAGER,ADMIN")]
[Route("api/internal/cashier/receipts")]
public sealed class CashierReceiptsController(BillingDbContext db) : ControllerBase
{
    [HttpGet("{billId:int}")]
    public async Task<ActionResult<object>> Get(int billId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        if (billId < 0)
        {
            var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceId == -billId, ct);
            if (invoice is null) return NotFound(new { code = "receipt_not_found", message = "Không tìm thấy hóa đơn." });
            var quote = await db.CheckoutQuotes.AsNoTracking().FirstOrDefaultAsync(x => x.CheckoutQuoteId == invoice.CheckoutQuoteId, ct);
            if ((invoice.BranchId ?? quote?.BranchId) != branchId) return NotFound(new { code = "receipt_not_found", message = "Không tìm thấy hóa đơn." });
            var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == invoice.PaymentId, ct);
            var voucher = quote?.VoucherId is int voucherId ? await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherId == voucherId, ct) : null;
            return Ok(new { source = "APP", receipt = InvoiceViews.Map(invoice, quote, payment, voucher) });
        }
        var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(x => x.BillID == billId && x.IsActive, ct);
        if (bill is null || (bill.BranchIdSnapshot is int b && b != branchId))
            return NotFound(new { code = "receipt_not_found", message = "Không tìm thấy hóa đơn." });
        return Ok(new { source = "COUNTER", receipt = MapBill(bill) });
    }

    /// <summary>A counter bill in the same shape as an app invoice, so one receipt template prints both.</summary>
    public static object MapBill(Bills bill)
    {
        var lines = InvoiceViews.Lines(bill.ItemsJson);
        var hasSnapshot = bill.ItemsJson is not null;
        return new
        {
            invoiceId = bill.BillID,
            invoiceCode = bill.BillCode,
            snapshotVersion = hasSnapshot ? InvoiceSnapshot.CurrentVersion : 0,
            branchId = bill.BranchIdSnapshot,
            branchName = bill.BranchNameSnapshot,
            branchAddress = (string?)null,
            tableName = bill.TableNameSnapshot,
            sessionStartedAtUtc = (DateTime?)null,
            paidAtUtc = DateTime.SpecifyKind(bill.BillTime, DateTimeKind.Local).ToUniversalTime(),
            issuedAtUtc = DateTime.SpecifyKind(bill.BillTime, DateTimeKind.Local).ToUniversalTime(),
            cashierName = bill.EmployeeNameSnapshot,
            items = lines.Select(x => new { x.DishId, x.DishName, x.Quantity, x.UnitPrice, x.LineTotal }),
            itemCount = lines.Sum(x => x.Quantity),
            subtotal = Money(bill.Subtotal),
            promotionName = (string?)null,
            promotionDiscount = Money(bill.PromotionDiscount),
            voucherCode = (string?)null,
            voucherDiscount = 0L,
            manualDiscount = Money(bill.Discount),
            pointsUsed = bill.PointsUsed ?? 0,
            pointsDiscount = Money(bill.PointsDiscount),
            serviceFeePercent = hasSnapshot ? bill.ServiceFeePercent : (decimal?)null,
            serviceFeeAmount = Money(bill.ServiceFeeAmount),
            vatPercent = hasSnapshot ? bill.VatPercent : (decimal?)null,
            vatAmount = Money(bill.VatAmount),
            depositApplied = 0L,
            grossTotal = Money(bill.TotalAmount),
            totalAmount = Money(bill.TotalAmount),
            amountPaid = Money(bill.PaymentAmount ?? bill.TotalAmount),
            changeAmount = Money(bill.ChangeAmount ?? 0),
            paymentMethod = bill.PaymentMethod,
            bankTransactionRef = (string?)null,
            pointsEarned = (int?)null
        };
    }

    private static long Money(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
}
