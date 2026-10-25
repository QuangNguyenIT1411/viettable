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
    public MobileEngagementGatewayController(CustomersClient customers, CatalogClient catalog, BillingClient billing)
    { _customers = customers; _catalog = catalog; _billing = billing; }

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
    public Task<ActionResult<object>> Notifications([FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    { if (limit is < 1 or > 100) return Task.FromResult<ActionResult<object>>(Error("validation_failed", "limit phải từ 1 đến 100.", 400)); return Run(() => _customers.GetMobileNotificationsAsync(Actor()!.Value, cursor, limit, ct)); }

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
            var hash = Hash(new { request.InvoiceId, request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, Comment = request.Comment?.Trim(), request.ImageUrls });
            var value = await _customers.CreateMobileReviewAsync(actor.Value, new { customerId = actor.Value, request.InvoiceId,
                diningSessionId = sessionValue.GetInt32(), request.OverallRating, request.FoodRating, request.ServiceRating,
                request.SpaceRating, request.Comment, request.ImageUrls, idempotencyKey = $"c{actor}:review:{key.Value}", requestHash = hash }, ct);
            return Ok(value);
        }
        catch (ApiClientException ex) when (ex.StatusCode == 404) { return Error("review_not_eligible", "Chỉ có thể đánh giá sau khi giao dịch hoàn tất.", 409); }
        catch (ApiClientException ex) { return Downstream(ex); }
    }

    [HttpGet("me/reviews")]
    public Task<ActionResult<object>> Reviews(CancellationToken ct) => Run(() => _customers.GetMobileReviewsAsync(Actor()!.Value, ct));

    [HttpPut("reviews/{reviewId:int}")]
    public Task<ActionResult<object>> UpdateReview(int reviewId, UpdateReviewBody request, CancellationToken ct) =>
        Run(() => _customers.UpdateMobileReviewAsync(Actor()!.Value, reviewId, request, ct));

    [HttpDelete("reviews/{reviewId:int}")]
    public async Task<ActionResult> DeleteReview(int reviewId, CancellationToken ct)
    { var actor = Actor(); if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401); try { await _customers.DeleteMobileReviewAsync(actor.Value, reviewId, ct); return NoContent(); } catch (ApiClientException ex) { return Downstream(ex); } }

    [AllowAnonymous]
    [HttpGet("support/faqs")]
    public async Task<ActionResult<object>> Faqs(CancellationToken ct) { try { return Ok(await _customers.GetSupportFaqsAsync(ct)); } catch (ApiClientException ex) { return Downstream(ex); } }

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
