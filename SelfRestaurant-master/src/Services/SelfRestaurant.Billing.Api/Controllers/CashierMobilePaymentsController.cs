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
            select new { p.PaymentId, p.PaymentCode, p.Amount, p.Currency, p.Method, p.Location, p.Status, p.ProviderName,
                p.DiningSessionId, q.DiningSessionCode, q.TableId, p.CompletedAtUtc, p.ConfirmedByEmployeeId,
                splitPart = db.SplitBillParts.Where(x => x.PaymentId == p.PaymentId).Select(x => x.Label).FirstOrDefault(),
                isCurrentAttempt = !db.SplitBills.Any(x => x.CheckoutQuoteId == q.CheckoutQuoteId)
                    || db.SplitBillParts.Any(x => x.PaymentId == p.PaymentId) }).Take(100).ToListAsync(ct);
        return Ok(payments);
    }

    // Same VietQR as in the app, shown on the counter screen for a pending app payment by bank transfer.
    [HttpGet("{paymentId:int}/transfer-qr")]
    public async Task<IActionResult> TransferQr(int paymentId, [FromServices] BankTransferQrService transfer,
        [FromServices] Microsoft.Extensions.Options.IOptions<SePayOptions> sepay, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        var row = await (from p in db.Payments.AsNoTracking()
                         join q in db.CheckoutQuotes.AsNoTracking() on p.CheckoutQuoteId equals q.CheckoutQuoteId
                         where p.PaymentId == paymentId
                         select new { Payment = p, q.BranchId }).FirstOrDefaultAsync(ct);
        if (row is null || row.BranchId != branchId) return NotFound(new { code = "payment_not_found", message = "Không tìm thấy giao dịch của chi nhánh." });
        if (!MobileCheckoutController.IsTransferPayment(row.Payment.Channel, row.Payment.Method))
            return Conflict(new { code = "not_bank_transfer", message = "Giao dịch này không thanh toán bằng chuyển khoản." });
        var result = TransferQrResult(transfer, row.Payment.Amount, BankTransferQrService.PaymentContent(branchId, paymentId));
        // App payments are confirmed automatically by SePay when it is on; counter orders (DON) never are.
        return result is OkObjectResult { Value: TransferQrResponse qr } ? Ok(qr with { AutoConfirm = sepay.Value.Enabled }) : result;
    }

    // VietQR for a cashier checkout of one order. The amount is the total after discounts shown on the counter screen.
    [HttpGet("~/api/internal/cashier/transfer-qr")]
    public IActionResult OrderTransferQr([FromQuery] int orderId, [FromQuery] decimal amount, [FromServices] BankTransferQrService transfer)
    {
        if (!int.TryParse(User.FindFirst("branchId")?.Value, out var branchId) || branchId <= 0) return Forbid();
        if (orderId <= 0 || amount <= 0 || amount > 1_000_000_000m)
            return BadRequest(new { code = "validation_failed", message = "Đơn hàng hoặc số tiền không hợp lệ." });
        return TransferQrResult(transfer, amount, BankTransferQrService.OrderContent(branchId, orderId));
    }

    private IActionResult TransferQrResult(BankTransferQrService transfer, decimal amount, string content) =>
        transfer.IsConfigured
            ? Ok(transfer.Create(amount, content))
            : Conflict(new { code = "bank_transfer_unavailable", message = "Chưa cấu hình tài khoản nhận chuyển khoản." });

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
