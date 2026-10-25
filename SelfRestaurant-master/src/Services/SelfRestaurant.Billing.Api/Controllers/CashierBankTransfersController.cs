using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// "Chuyển khoản cần xử lý": SePay transfers that were not auto-confirmed. The cashier checks the bank app,
/// settles by hand if right (the normal "Đã nhận chuyển khoản" button) and marks the warning as handled.
/// Transfers whose branch is unknown (no readable code) are shown to every branch.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "CashierService", Roles = "CASHIER,MANAGER,ADMIN")]
[Route("api/internal/cashier/bank-transfers")]
public sealed class CashierBankTransfersController(BillingDbContext db) : ControllerBase
{
    public static readonly string[] WarningOutcomes =
    [
        SePayMatchRules.AmountTooLow, SePayMatchRules.NoMatch, SePayMatchRules.BranchMismatch,
        SePayMatchRules.NotTransfer, SePayMatchRules.PaymentNotPending, SePayMatchRules.SessionNotPayable
    ];

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        var since = DateTime.UtcNow.AddDays(-7);
        var rows = await db.BankTransferEvents.AsNoTracking()
            .Where(x => x.ResolvedAtUtc == null && x.ReceivedAtUtc >= since && WarningOutcomes.Contains(x.Outcome)
                && (x.BranchId == branchId || x.BranchId == null))
            .OrderByDescending(x => x.ReceivedAtUtc).Take(50)
            .Select(x => new { eventId = x.BankTransferEventId, x.Amount, x.Content, x.Outcome, x.PaymentId, x.ReferenceCode,
                x.ReceivedAtUtc, x.TransactionDate,
                expectedAmount = db.Payments.Where(p => p.PaymentId == x.PaymentId).Select(p => (decimal?)p.Amount).FirstOrDefault(),
                tableId = (from p in db.Payments
                           join q in db.CheckoutQuotes on p.CheckoutQuoteId equals q.CheckoutQuoteId
                           where p.PaymentId == x.PaymentId
                           select (int?)q.TableId).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{eventId:long}/resolve")]
    public async Task<IActionResult> Resolve(long eventId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        if (!int.TryParse(User.FindFirst("employeeId")?.Value, out var employeeId) || employeeId <= 0) return Forbid();
        var row = await db.BankTransferEvents.SingleOrDefaultAsync(x => x.BankTransferEventId == eventId, ct);
        if (row is null || (row.BranchId is not null && row.BranchId != branchId) || !WarningOutcomes.Contains(row.Outcome))
            return NotFound(new { code = "bank_transfer_not_found", message = "Không tìm thấy chuyển khoản cần xử lý của chi nhánh." });
        if (row.ResolvedAtUtc is null)
        {
            row.ResolvedAtUtc = DateTime.UtcNow;
            row.ResolvedByEmployeeId = employeeId;
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { success = true, eventId, message = "Đã đánh dấu đã xử lý." });
    }
}
