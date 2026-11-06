using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// What a basket would cost at a branch before any discount (owner 06/11: "Gợi ý món" shows an estimate computed
/// exactly like the bill). Same formula and the branch's current service fee / VAT.
/// </summary>
[ApiController]
public sealed class CheckoutEstimateController(BillingDbContext db) : ControllerBase
{
    public sealed record EstimateRequest(int BranchId, decimal Subtotal);

    [HttpPost("api/internal/checkout/estimate")]
    public async Task<ActionResult<object>> Estimate([FromBody] EstimateRequest? request, CancellationToken ct)
    {
        if (request is null || request.BranchId <= 0 || request.Subtotal < 0)
            return BadRequest(new { code = "validation_failed", message = "Thiếu chi nhánh hoặc số tiền." });
        var charges = await BranchCharges.ForBranchAsync(db, request.BranchId, ct);
        var result = CheckoutPricing.Compute(new PricingInput(request.Subtotal, null, null, 0, 0, 0, 0, 0,
            ServiceFeePercent: charges.ServiceFeePercent, VatPercent: charges.VatPercent));
        return Ok(new
        {
            subtotal = result.Subtotal, serviceFeePercent = result.ServiceFeePercent, serviceFee = result.ServiceFee,
            vatPercent = result.VatPercent, vat = result.Vat, total = result.GrossTotal
        });
    }
}
