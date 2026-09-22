using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Controllers;

[ApiController]
[Route("api/internal/mobile")]
public sealed class MobileEngagementController : ControllerBase
{
    private readonly CustomersDbContext _db;
    public MobileEngagementController(CustomersDbContext db) => _db = db;

    [HttpGet("customers/{customerId:int}/favorites")]
    public async Task<ActionResult<object>> Favorites(int customerId, CancellationToken ct)
    {
        var rows = await _db.CustomerFavorites.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return Ok(new { items = rows.Select(MapFavorite), nextCursor = (string?)null });
    }

    [HttpPost("customers/{customerId:int}/favorites/dishes/{dishId:int}")]
    public async Task<ActionResult<object>> AddFavorite(int customerId, int dishId, FavoriteRequest request, CancellationToken ct)
    {
        if (customerId <= 0 || dishId <= 0 || string.IsNullOrWhiteSpace(request.DishName)) return Validation();
        var existing = await _db.CustomerFavorites.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.DishId == dishId, ct);
        if (existing is not null) return Ok(MapFavorite(existing));
        var entity = new CustomerFavorite { CustomerId = customerId, DishId = dishId, DishNameSnapshot = request.DishName.Trim(), CreatedAtUtc = DateTime.UtcNow };
        _db.CustomerFavorites.Add(entity);
        try { await _db.SaveChangesAsync(ct); } catch (DbUpdateException) { entity = await _db.CustomerFavorites.FirstAsync(x => x.CustomerId == customerId && x.DishId == dishId, ct); }
        return Ok(MapFavorite(entity));
    }

    [HttpDelete("customers/{customerId:int}/favorites/dishes/{dishId:int}")]
    public async Task<ActionResult> RemoveFavorite(int customerId, int dishId, CancellationToken ct)
    {
        var entity = await _db.CustomerFavorites.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.DishId == dishId, ct);
        if (entity is not null) { _db.CustomerFavorites.Remove(entity); await _db.SaveChangesAsync(ct); }
        return NoContent();
    }

    [HttpGet("customers/{customerId:int}/notifications")]
    public async Task<ActionResult<object>> Notifications(int customerId, [FromQuery] long? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = _db.ReadyDishNotifications.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (cursor is > 0) query = query.Where(x => x.ReadyDishNotificationId < cursor);
        var rows = await query.OrderByDescending(x => x.ReadyDishNotificationId).Take(limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
        return Ok(new { items = rows.Select(x => new { notificationId = x.ReadyDishNotificationId, type = x.EventName,
            title = "Cập nhật món ăn", x.Message, x.OrderId, x.OrderItemId, x.DishId, x.DishName,
            isRead = x.Status != "OPEN", x.CreatedAtUtc, readAtUtc = x.ResolvedAtUtc }),
            nextCursor = hasMore ? rows[^1].ReadyDishNotificationId.ToString() : null });
    }

    [HttpPost("customers/{customerId:int}/notifications/{notificationId:long}/read")]
    public async Task<ActionResult<object>> ReadNotification(int customerId, long notificationId, CancellationToken ct)
    {
        var entity = await _db.ReadyDishNotifications.FirstOrDefaultAsync(x => x.ReadyDishNotificationId == notificationId, ct);
        if (entity is null) return NotFound(new { code = "notification_not_found", message = "Không tìm thấy thông báo." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu thông báo này." });
        if (entity.Status == "OPEN") { entity.Status = "RESOLVED"; entity.ResolvedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
        return Ok(new { notificationId, isRead = true, readAtUtc = entity.ResolvedAtUtc });
    }

    [HttpPost("customers/{customerId:int}/reviews")]
    public async Task<ActionResult<object>> CreateReview(int customerId, CreateReviewRequest request, CancellationToken ct)
    {
        if (!ValidReview(request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, request.Comment, request.ImageUrls)
            || request.InvoiceId <= 0 || request.DiningSessionId <= 0 || !ValidCommand(request.IdempotencyKey, request.RequestHash)) return Validation();
        var replay = await _db.CustomerReviews.FirstOrDefaultAsync(x => x.CreateIdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapReview(replay)) : IdempotencyConflict();
        if (await _db.CustomerReviews.AnyAsync(x => x.InvoiceId == request.InvoiceId, ct)) return Conflict(new { code = "review_already_exists", message = "Hóa đơn này đã được đánh giá." });
        var now = DateTime.UtcNow;
        var entity = new CustomerReview { CustomerId = customerId, InvoiceId = request.InvoiceId, DiningSessionId = request.DiningSessionId,
            OverallRating = request.OverallRating, FoodRating = request.FoodRating, ServiceRating = request.ServiceRating,
            SpaceRating = request.SpaceRating, Comment = Clean(request.Comment), ImageUrlsJson = JsonSerializer.Serialize(request.ImageUrls ?? Array.Empty<string>()),
            CreateIdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = request.RequestHash, CreatedAtUtc = now, UpdatedAtUtc = now };
        _db.CustomerReviews.Add(entity); await _db.SaveChangesAsync(ct); return Ok(MapReview(entity));
    }

    [HttpGet("customers/{customerId:int}/reviews")]
    public async Task<ActionResult<object>> Reviews(int customerId, CancellationToken ct)
    {
        var rows = await _db.CustomerReviews.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return Ok(new { items = rows.Select(MapReview), nextCursor = (string?)null });
    }

    [HttpPut("customers/{customerId:int}/reviews/{reviewId:int}")]
    public async Task<ActionResult<object>> UpdateReview(int customerId, int reviewId, UpdateReviewRequest request, CancellationToken ct)
    {
        var entity = await _db.CustomerReviews.FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (entity is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đánh giá này." });
        if (!ValidReview(request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, request.Comment, request.ImageUrls)) return Validation();
        entity.OverallRating = request.OverallRating; entity.FoodRating = request.FoodRating; entity.ServiceRating = request.ServiceRating;
        entity.SpaceRating = request.SpaceRating; entity.Comment = Clean(request.Comment); entity.ImageUrlsJson = JsonSerializer.Serialize(request.ImageUrls ?? Array.Empty<string>()); entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); return Ok(MapReview(entity));
    }

    [HttpDelete("customers/{customerId:int}/reviews/{reviewId:int}")]
    public async Task<ActionResult> DeleteReview(int customerId, int reviewId, CancellationToken ct)
    {
        var entity = await _db.CustomerReviews.FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (entity is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đánh giá này." });
        _db.CustomerReviews.Remove(entity); await _db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("support/faqs")]
    public ActionResult<object> Faqs() => Ok(new { items = new[]
    {
        new { faqId = "reservation", question = "Làm sao thay đổi đặt bàn?", answer = "Mở chi tiết đặt bàn sắp tới và chọn Thay đổi." },
        new { faqId = "payment", question = "Thanh toán thất bại thì làm gì?", answer = "Bạn có thể thử lại giao dịch hoặc chọn thanh toán tại bàn/quầy." },
        new { faqId = "order", question = "Món của tôi đang ở đâu?", answer = "Theo dõi trạng thái từng món trong mục Đơn hàng." }
    }});

    [HttpPost("customers/{customerId:int}/support/requests")]
    public async Task<ActionResult<object>> SubmitSupportRequest(int customerId, CreateSupportRequest request, CancellationToken ct)
    {
        var category = request.Category?.Trim().ToUpperInvariant();
        if (category is not ("GENERAL" or "ORDER" or "RESERVATION" or "PAYMENT") || string.IsNullOrWhiteSpace(request.Subject)
            || request.Subject.Length > 200 || string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 3000
            || !ValidCommand(request.IdempotencyKey, request.RequestHash)) return Validation();
        var replay = await _db.SupportRequests.FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapSupport(replay)) : IdempotencyConflict();
        var now = DateTime.UtcNow; var entity = new SupportRequest { RequestCode = $"SUP-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            CustomerId = customerId, Category = category, Subject = request.Subject.Trim(), Message = request.Message.Trim(), OrderId = request.OrderId,
            ReservationId = request.ReservationId, PaymentId = request.PaymentId, Status = "OPEN", IdempotencyKey = request.IdempotencyKey.Trim(),
            RequestHash = request.RequestHash, CreatedAtUtc = now, UpdatedAtUtc = now };
        _db.SupportRequests.Add(entity); await _db.SaveChangesAsync(ct); return Ok(MapSupport(entity));
    }

    private static object MapFavorite(CustomerFavorite x) => new { favoriteId = x.CustomerFavoriteId, x.DishId, dishName = x.DishNameSnapshot, x.CreatedAtUtc };
    private static object MapReview(CustomerReview x) => new { reviewId = x.CustomerReviewId, x.InvoiceId, x.DiningSessionId, x.OverallRating, x.FoodRating, x.ServiceRating, x.SpaceRating, x.Comment, imageUrls = JsonSerializer.Deserialize<string[]>(x.ImageUrlsJson) ?? Array.Empty<string>(), x.CreatedAtUtc, x.UpdatedAtUtc };
    private static object MapSupport(SupportRequest x) => new { x.SupportRequestId, x.RequestCode, x.Category, x.Subject, x.Message, x.OrderId, x.ReservationId, x.PaymentId, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc };
    private static bool ValidCommand(string? key, string? hash) => !string.IsNullOrWhiteSpace(key) && key.Length <= 140 && !string.IsNullOrWhiteSpace(hash) && hash.Length == 64;
    private static bool ValidReview(int overall, int food, int service, int space, string? comment, IReadOnlyList<string>? images) =>
        overall is >= 1 and <= 5 && food is >= 1 and <= 5 && service is >= 1 and <= 5 && space is >= 1 and <= 5
        && comment?.Length <= 2000 && (images?.Count ?? 0) <= 5 && (images?.All(x => Uri.TryCreate(x, UriKind.Absolute, out _)) ?? true);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private ActionResult Validation() => BadRequest(new { code = "validation_failed", message = "Dữ liệu không hợp lệ." });
    private ActionResult IdempotencyConflict() => Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });

    public sealed record FavoriteRequest(string DishName);
    public sealed record CreateReviewRequest(int InvoiceId, int DiningSessionId, int OverallRating, int FoodRating, int ServiceRating, int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls, string IdempotencyKey, string RequestHash);
    public sealed record UpdateReviewRequest(int OverallRating, int FoodRating, int ServiceRating, int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls);
    public sealed record CreateSupportRequest(string? Category, string? Subject, string? Message, int? OrderId, int? ReservationId, int? PaymentId, string IdempotencyKey, string RequestHash);
}
