using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1")]
public sealed class MobileEngagementGatewayController : ControllerBase
{
    private readonly CustomersClient _customers;
    private readonly CatalogClient _catalog;
    private readonly BillingClient _billing;
    private readonly IdentityClient? Identity;
    public MobileEngagementGatewayController(CustomersClient customers, CatalogClient catalog, BillingClient billing, IdentityClient? identity = null)
    { _customers = customers; _catalog = catalog; _billing = billing; Identity = identity; }

    [HttpGet("me/favorites")]
    public Task<ActionResult<object>> Favorites(CancellationToken ct) => Run(() => _customers.GetMobileFavoritesAsync(Actor()!.Value, ct));

    [HttpPost("me/favorites/dishes/{dishId:int}")]
    public async Task<ActionResult<object>> AddFavorite(int dishId, CancellationToken ct)
    {
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var dish = await _catalog.GetAdminDishByIdAsync(dishId, ct);
            if (dish is null || dish.IsActive == false) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);
            return Ok(await _customers.AddMobileFavoriteAsync(actor.Value, dishId, dish.Name, ct));
        }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpDelete("me/favorites/dishes/{dishId:int}")]
    public async Task<ActionResult> RemoveFavorite(int dishId, CancellationToken ct)
    { var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { await _customers.RemoveMobileFavoriteAsync(actor.Value, dishId, ct); return NoContent(); } catch (ApiClientException ex) { return Downstream(ex); } }

    [HttpGet("notifications")]
    public async Task<ActionResult<object>> Notifications([FromServices] PromotionAnnouncer announcer, [FromQuery] string? cursor,
        [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) return Error("validation_failed", "limit phải từ 1 đến 100.", 400);
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        // First page only: running promotions the customer was not told about yet land in "Ưu đãi" before the read.
        if (string.IsNullOrEmpty(cursor)) await announcer.EnsureForCustomerAsync(actor.Value, ct);
        return await Run(() => _customers.GetMobileNotificationsAsync(actor.Value, cursor, limit, ct));
    }

    [HttpPost("notifications/read-all")]
    public Task<ActionResult<object>> ReadAllNotifications(CancellationToken ct) => Run(() => _customers.ReadAllMobileNotificationsAsync(Actor()!.Value, ct));

    [HttpPost("notifications/{notificationId:long}/read")]
    public Task<ActionResult<object>> ReadNotification(long notificationId, CancellationToken ct) => Run(() => _customers.ReadMobileNotificationAsync(Actor()!.Value, notificationId, ct));

    [HttpPost("reviews")]
    public async Task<ActionResult<object>> CreateReview(CreateReviewBody request, CancellationToken ct)
    {
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = Key(); if (key.Error is not null) return key.Error;
        try
        {
            var invoice = await _billing.GetInvoiceAsync(request.InvoiceId, actor.Value, ct);
            if (invoice.ValueKind != JsonValueKind.Object || !invoice.TryGetProperty("diningSessionId", out var sessionValue))
                return Error("review_not_eligible", "Chỉ có thể đánh giá sau khi giao dịch hoàn tất.", 409);
            var hash = Hash(new { request.InvoiceId, request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, Comment = request.Comment?.Trim(), request.ImageUrls, request.Tags, request.IsAnonymous, request.DishRatings });
            // Branch, dishes and the author's name come from the server (invoice + profile), never from the app (owner 05/11).
            string? authorName = null;
            try { authorName = Identity is null ? null : (await Identity.GetMobileProfileAsync(ct))?.Name; } catch (ApiClientException) { }
            var value = await _customers.CreateMobileReviewAsync(actor.Value, new { customerId = actor.Value, request.InvoiceId,
                diningSessionId = sessionValue.GetInt32(), request.OverallRating, request.FoodRating, request.ServiceRating,
                request.SpaceRating, request.Comment, request.ImageUrls, idempotencyKey = $"c{actor}:review:{key.Value}", requestHash = hash,
                request.Tags, request.IsAnonymous, request.DishRatings, branchId = IntProperty(invoice, "branchId"), authorName,
                invoiceDishes = InvoiceDishes(invoice) }, ct);
            return Ok(value);
        }
        catch (ApiClientException ex) when (ex.StatusCode == 404) { return Error("review_not_eligible", "Chỉ có thể đánh giá sau khi giao dịch hoàn tất.", 409); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpGet("me/reviews")]
    public Task<ActionResult<object>> Reviews(CancellationToken ct) => Run(() => _customers.GetMobileReviewsAsync(Actor()!.Value, ct));

    [HttpPut("reviews/{reviewId:int}")]
    public async Task<ActionResult<object>> UpdateReview(int reviewId, UpdateReviewBody request, CancellationToken ct)
    {
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            // Dishes of the invoice (from Billing) so newly rated dishes get their real names.
            object[] dishes = Array.Empty<object>();
            if (request.InvoiceId is > 0)
                dishes = InvoiceDishes(await _billing.GetInvoiceAsync(request.InvoiceId.Value, actor.Value, ct));
            return Ok(await _customers.UpdateMobileReviewAsync(actor.Value, reviewId, new { request.OverallRating, request.FoodRating,
                request.ServiceRating, request.SpaceRating, request.Comment, request.ImageUrls, request.Tags, request.IsAnonymous,
                request.DishRatings, invoiceDishes = dishes }, ct));
        }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    /// <summary>"Chi tiết món": average stars, count and the latest comments of a dish, from real reviews (owner 05/11).</summary>
    [AllowAnonymous]
    [HttpGet("dishes/{dishId:int}/reviews/summary")]
    public async Task<ActionResult<object>> DishReviewSummary(int dishId, CancellationToken ct)
    {
        // Public like the menu: the shared helper above requires a signed-in guest, so it is not used here.
        try { return Ok(await _customers.GetDishReviewSummaryAsync(dishId, ct)); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    private static int? IntProperty(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static object[] InvoiceDishes(JsonElement invoice) =>
        invoice.ValueKind == JsonValueKind.Object && invoice.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Select(x => (object)new
            {
                dishId = IntProperty(x, "dishId") ?? 0,
                dishName = x.TryGetProperty("dishName", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null
            }).ToArray()
            : Array.Empty<object>();

    [HttpDelete("reviews/{reviewId:int}")]
    public async Task<ActionResult> DeleteReview(int reviewId, CancellationToken ct)
    { var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { await _customers.DeleteMobileReviewAsync(actor.Value, reviewId, ct); return NoContent(); } catch (ApiClientException ex) { return Downstream(ex); } }

    [AllowAnonymous]
    [HttpGet("support/faqs")]
    public async Task<ActionResult<object>> Faqs([FromServices] Microsoft.Extensions.Options.IOptions<SelfRestaurant.Deposits.DepositOptions> deposit, CancellationToken ct)
    {
        // The deposit question follows the backend flag (owner 27/10).
        try { return Ok(ReservationDepositCoordinator.WithDepositFaq(await _customers.GetSupportFaqsAsync(ct), deposit.Value)); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpPost("support/requests")]
    public async Task<ActionResult<object>> Support(CreateSupportBody request, CancellationToken ct)
    {
        var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); var key = Key(); if (key.Error is not null) return key.Error;
        var hash = Hash(new { Category = request.Category?.Trim().ToUpperInvariant(), Subject = request.Subject?.Trim(), Message = request.Message?.Trim(), request.OrderId, request.ReservationId, request.PaymentId });
        try { return Ok(await _customers.CreateSupportRequestAsync(actor.Value, new { request.Category, request.Subject, request.Message,
            request.OrderId, request.ReservationId, request.PaymentId, idempotencyKey = $"c{actor}:support:{key.Value}", requestHash = hash }, ct)); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    private async Task<ActionResult<object>> Run(Func<Task<JsonElement>> action)
    { if (Actor() is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { return Ok(await action()); } catch (ApiClientException ex) { return Downstream(ex); } }
    private int? Actor() => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;
    private (string? Value, ObjectResult? Error) Key() { var value = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim(); return string.IsNullOrWhiteSpace(value) || value.Length > 64 ? (null, Error("idempotency_key_required", "Idempotency-Key là bắt buộc và tối đa 64 ký tự.", 400)) : (value, null); }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private ObjectResult Downstream(ApiClientException ex) => Error(ex.Code ?? "engagement_failed", ex.Message, ex.StatusCode);
    private ObjectResult Error(string code, string message, int status) => StatusCode(status, MobileApiError.Create(code, message, Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier));
}
