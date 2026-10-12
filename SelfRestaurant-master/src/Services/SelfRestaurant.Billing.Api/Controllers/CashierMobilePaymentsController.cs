using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "CashierService", Roles = "CASHIER,MANAGER,ADMIN")]
[Route("api/internal/cashier/onsite-payments")]
public sealed class CashierMobilePaymentsController(BillingDbContext db, PaymentFinalizationService finalization) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        var payments = await (from p in db.Payments.AsNoTracking()
            join q in db.CheckoutQuotes on p.CheckoutQuoteId equals q.CheckoutQuoteId
            where q.BranchId == branchId && p.Channel == "ONSITE"
            orderby p.Status == "PENDING" descending, p.PaymentId descending
            select new { p.PaymentId, p.PaymentCode, p.Amount, p.Currency, p.Method, p.Location, p.Status,
                p.DiningSessionId, q.DiningSessionCode, q.TableId, p.CompletedAtUtc, p.ConfirmedByEmployeeId,
                splitPart = db.SplitBillParts.Where(x => x.PaymentId == p.PaymentId).Select(x => x.Label).FirstOrDefault(),
                isCurrentAttempt = !db.SplitBills.Any(x => x.CheckoutQuoteId == q.CheckoutQuoteId)
                    || db.SplitBillParts.Any(x => x.PaymentId == p.PaymentId) }).Take(100).ToListAsync(ct);
        return Ok(payments);
    }

    [HttpPost("{paymentId:int}/confirm")]
    public async Task<IActionResult> Confirm(int paymentId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0
            || !int.TryParse(User.FindFirst("employeeId")?.Value, out var employeeId) || employeeId <= 0) return Forbid();
        try
        {
            var payment = await finalization.ConfirmAsync(paymentId, employeeId, branchId, null, ct);
            return Ok(new { payment.PaymentId, payment.Status, payment.Amount, payment.CompletedAtUtc, payment.ConfirmedByEmployeeId,
                payment.CustomerId, payment.DiningSessionId });
        }
        catch (PaymentDomainException ex)
        { return StatusCode(ex.Status, new { success = false, code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }
}
