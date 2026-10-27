using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// Reservation deposits for the Gateway (docs/DEPOSIT_PLAN.md). The Gateway checks who is asking (the booking's
/// customer, or a cashier/admin of the branch) and passes the ids; money is confirmed only by the SePay webhook.
/// </summary>
[ApiController]
[Route("api/internal/deposits")]
public sealed class InternalDepositsController(ReservationDepositService deposits) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateDepositRequest request, CancellationToken ct)
    {
        try { return Ok(ReservationDepositService.Map(await deposits.CreateAsync(request, ct), DateTime.UtcNow)); }
        catch (PaymentDomainException ex) { return Error(ex); }
    }

    /// <summary>The deposit of a booking; with includeQr the VietQR to pay it while it still waits.</summary>
    [HttpGet("by-reservation/{reservationId:int}")]
    public async Task<ActionResult<object>> Get(int reservationId, [FromQuery] bool includeQr,
        [FromServices] BankTransferQrService transfer, [FromServices] IOptions<SePayOptions> sepay, CancellationToken ct)
    {
        var deposit = await deposits.GetAsync(reservationId, ct);
        if (deposit is null) return NotFound(new { code = "deposit_not_found", message = "Đặt bàn này không có khoản đặt cọc." });
        object? qr = null;
        if (includeQr && deposit.Status == SelfRestaurant.Deposits.DepositRules.Pending && transfer.IsConfigured)
            qr = transfer.Create(deposit.Amount, SelfRestaurant.Deposits.DepositRules.TransferContent(deposit.ReservationId))
                with { AutoConfirm = sepay.Value.Enabled };
        return Ok(new { deposit = ReservationDepositService.Map(deposit, DateTime.UtcNow), transfer = qr });
    }

    [HttpPost("by-reservation/{reservationId:int}/close")]
    public async Task<ActionResult<object>> Close(int reservationId, CloseDepositRequest request, CancellationToken ct)
    {
        var deposit = await deposits.CloseAsync(reservationId, request, ct);
        return deposit is null
            ? NotFound(new { code = "deposit_not_found", message = "Đặt bàn này không có khoản đặt cọc." })
            : Ok(ReservationDepositService.Map(deposit, DateTime.UtcNow));
    }

    /// <summary>Staff list: one branch (or every branch for an admin), or the deposits of some bookings.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] int? branchId, [FromQuery] string? reservationIds, CancellationToken ct)
    {
        var ids = (reservationIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(x => x > 0).Distinct().Take(200).ToArray();
        var now = DateTime.UtcNow;
        return Ok((await deposits.ListAsync(branchId, ids, ct)).Select(x => ReservationDepositService.Map(x, now)).ToArray());
    }

    [HttpPost("{depositId:int}/refunded")]
    public async Task<ActionResult<object>> MarkRefunded(int depositId, RefundedRequest request, CancellationToken ct)
    {
        try { return Ok(ReservationDepositService.Map(await deposits.MarkRefundedAsync(depositId, request.EmployeeId, request.BranchId, ct), DateTime.UtcNow)); }
        catch (PaymentDomainException ex) { return Error(ex); }
    }

    private ObjectResult Error(PaymentDomainException ex) => StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });

    public sealed record RefundedRequest(int EmployeeId, int? BranchId);
}
