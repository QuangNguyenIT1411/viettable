using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>Running promotions for the app's banner and "Ưu đãi" screen (public, like the menu).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/mobile/v1/promotions")]
public sealed class MobilePromotionsGatewayController(BillingClient billing, PromotionAnnouncer announcer) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] int? branchId, CancellationToken ct)
    {
        // A promotion that just started is announced on the first read after its start (no background job on Vercel).
        await announcer.AnnounceDueAsync(ct);
        try { return Ok(new { items = await billing.GetActivePromotionsAsync(branchId, ct) }); }
        catch (ApiClientException ex) { return Error(ex.Code ?? "promotions_unavailable", ex.Message, ex.StatusCode); }
    }

    [HttpGet("{promotionId:int}")]
    public async Task<ActionResult<object>> Get(int promotionId, CancellationToken ct)
    {
        try
        {
            var row = await billing.GetPromotionAsync(promotionId, ct);
            return row is null ? Error("promotion_not_found", "Không tìm thấy khuyến mãi.", 404) : Ok(row.Value);
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "promotions_unavailable", ex.Message, ex.StatusCode); }
    }

    private ObjectResult Error(string code, string message, int status) =>
        StatusCode(status, MobileApiError.Create(code, message, HttpContext.TraceIdentifier, null));
}
