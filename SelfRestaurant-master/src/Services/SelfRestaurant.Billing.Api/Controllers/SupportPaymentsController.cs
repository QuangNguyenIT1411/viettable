using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// "Chat với nhân viên hỗ trợ" (owner 06/11): the customer's own bank-transfer payments of the last 14 days, with their
/// real status, so the app can check "đã chuyển khoản nhưng chưa được xác nhận" before opening a chat, and staff see the
/// payment a conversation is about.
/// </summary>
[ApiController]
public sealed class SupportPaymentsController(BillingDbContext db) : ControllerBase
{
    [HttpGet("api/internal/support/customers/{customerId:int}/transfers")]
    public async Task<ActionResult<object>> Transfers(int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return BadRequest(new { code = "validation_failed", message = "Khách hàng không hợp lệ." });
        var since = DateTime.UtcNow.AddDays(-14);
        var rows = await (from p in db.Payments.AsNoTracking()
                          join q in db.CheckoutQuotes.AsNoTracking() on p.CheckoutQuoteId equals q.CheckoutQuoteId
                          where p.CustomerId == customerId && p.CreatedAtUtc >= since
                          orderby p.PaymentId descending
                          select new { Payment = p, q.BranchId }).Take(50).ToListAsync(ct);
        var transfers = rows.Where(x => MobileCheckoutController.IsTransferPayment(x.Payment.Channel, x.Payment.Method)).ToList();
        var ids = transfers.Select(x => x.Payment.PaymentId).ToArray();
        var invoices = await db.Invoices.AsNoTracking().Where(i => ids.Contains(i.PaymentId))
            .Select(i => new { i.PaymentId, i.InvoiceId }).ToListAsync(ct);
        return Ok(new
        {
            items = transfers.Select(x => new
            {
                paymentId = x.Payment.PaymentId, branchId = x.BranchId, amount = Money(x.Payment.Amount), status = x.Payment.Status,
                confirmed = x.Payment.Status == "SUCCEEDED", createdAtUtc = x.Payment.CreatedAtUtc, completedAtUtc = x.Payment.CompletedAtUtc,
                transferContent = BankTransferQrService.PaymentContent(x.BranchId, x.Payment.PaymentId),
                invoiceId = invoices.FirstOrDefault(i => i.PaymentId == x.Payment.PaymentId)?.InvoiceId
            })
        });
    }

    private static long Money(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
}
