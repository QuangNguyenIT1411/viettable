using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
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
        // title/category come from the event: the app shows them as given and files the row under that tab.
        return Ok(new { items = rows.Select(x => new { notificationId = x.ReadyDishNotificationId, type = x.EventName,
            title = CustomerInbox.Title(x.EventName), category = CustomerInbox.Category(x.EventName),
            refId = x.OrderId > 0 ? x.OrderId : x.OrderItemId ?? 0, x.Message, x.OrderId, x.OrderItemId, x.DishId, x.DishName,
            isRead = x.Status != "OPEN", x.CreatedAtUtc, readAtUtc = x.ResolvedAtUtc }),
            unreadCount = await _db.ReadyDishNotifications.CountAsync(x => x.CustomerId == customerId && x.Status == "OPEN", ct),
            nextCursor = hasMore ? rows[^1].ReadyDishNotificationId.ToString() : null });
    }

    /// <summary>One inbox row for a known event (Gateway: kitchen, service requests, cashier cancels). Idempotent.</summary>
    [HttpPost("customers/{customerId:int}/notifications")]
    public async Task<ActionResult<object>> AddNotification(int customerId, AddNotificationRequest request, CancellationToken ct)
    {
        if (customerId <= 0 || !CustomerInbox.IsKnown(request.EventName) || request.RefId < 0 || (request.OrderId ?? 0) < 0
            || string.IsNullOrWhiteSpace(request.Message)) return Validation();
        var (row, created) = await CustomerInbox.AddAsync(_db, customerId, request.EventName, request.RefId,
            request.Message.Trim(), request.OrderId ?? 0, ct);
        if (created) await _db.SaveChangesAsync(ct);
        return Ok(new { notificationId = row.ReadyDishNotificationId, created });
    }

    /// <summary>Points earned for one paid invoice, and the new tier when it changed. Idempotent per payment.</summary>
    [HttpPost("customers/{customerId:int}/notifications/loyalty")]
    public async Task<ActionResult<object>> LoyaltySettled(int customerId, LoyaltyNotificationRequest request, CancellationToken ct)
    {
        if (customerId <= 0 || request.PaymentId <= 0 || request.PointsEarned < 0 || request.PointsAfter < 0) return Validation();
        var created = 0;
        if (request.PointsEarned > 0)
        {
            var (_, added) = await CustomerInbox.AddAsync(_db, customerId, CustomerInbox.PointsEarned, request.PaymentId,
                $"Bạn được cộng {Points(request.PointsEarned)} điểm thưởng. Tổng điểm hiện có: {Points(request.PointsAfter)}.", 0, ct);
            if (added) created++;
        }
        var before = CustomerInbox.Tier(request.PointsBefore);
        var after = CustomerInbox.Tier(request.PointsAfter);
        if (request.PointsAfter > request.PointsBefore && before.Code != after.Code)
        {
            var (_, added) = await CustomerInbox.AddAsync(_db, customerId, CustomerInbox.TierUp, request.PaymentId,
                $"Chúc mừng! Bạn đã lên hạng {after.Name}.", 0, ct);
            if (added) created++;
        }
        if (created > 0) await _db.SaveChangesAsync(ct);
        return Ok(new { created });
    }

    /// <summary>
    /// One inbox row per customer for an announcement (e.g. "Ưu đãi mới"); a repeat adds nothing (one per customer and
    /// item). Only events the inbox knows are accepted. One read finds who already has it, then the rest are inserted in
    /// batches: the old one-query-per-customer loop ran past the Gateway's 10 s timeout with many test accounts, so
    /// nobody got the row and every retry started over (owner's test 28/10).
    /// </summary>
    [HttpPost("~/api/internal/inbox/broadcast")]
    public async Task<ActionResult<object>> Broadcast(BroadcastRequest request, CancellationToken ct)
    {
        if (!CustomerInbox.IsKnown(request.EventName) || request.RefId <= 0 || string.IsNullOrWhiteSpace(request.Message)
            || request.CustomerIds is null || request.CustomerIds.Count > 20000) return Validation();
        var created = await CustomerInbox.BroadcastAsync(_db, request.CustomerIds, request.EventName!, request.RefId,
            request.Message.Trim(), ct);
        return Ok(new { created });
    }

    public sealed record BroadcastRequest(IReadOnlyList<int>? CustomerIds, string? EventName, int RefId, string? Message);

    [HttpPost("customers/{customerId:int}/notifications/read-all")]
    public async Task<ActionResult<object>> ReadAllNotifications(int customerId, CancellationToken ct)
    {
        if (customerId <= 0) return Validation();
        var now = DateTime.UtcNow;
        var open = await _db.ReadyDishNotifications.Where(x => x.CustomerId == customerId && x.Status == "OPEN").ToListAsync(ct);
        foreach (var row in open) { row.Status = "RESOLVED"; row.ResolvedAtUtc = now; }
        if (open.Count > 0) await _db.SaveChangesAsync(ct);
        return Ok(new { read = open.Count });
    }

    public const string PaymentSucceededEvent = CustomerInbox.PaymentSucceeded;

    // Inbox row for a collected payment. Payment rows have no order: OrderId is 0 and OrderItemId carries the
    // PaymentId, so the existing (OrderId, OrderItemId, EventName) index makes the call idempotent per payment.
    // TableId stays null so the legacy table-wide ready-dish feed can never show another customer's payment.
    [HttpPost("customers/{customerId:int}/notifications/payment-succeeded")]
    public async Task<ActionResult<object>> PaymentSucceeded(int customerId, PaymentSucceededRequest request, CancellationToken ct)
    {
        if (customerId <= 0 || request.PaymentId <= 0 || request.DiningSessionId <= 0 || request.Amount < 0) return Validation();
        var existing = await _db.ReadyDishNotifications.AsNoTracking().FirstOrDefaultAsync(
            x => x.OrderId == 0 && x.OrderItemId == request.PaymentId && x.EventName == PaymentSucceededEvent, ct);
        if (existing is not null) return Ok(new { notificationId = existing.ReadyDishNotificationId, created = false });
        var entity = new ReadyDishNotifications
        {
            OrderId = 0, OrderItemId = request.PaymentId, CustomerId = customerId,
            EventName = PaymentSucceededEvent, Message = PaymentSucceededMessage(request.Amount, request.DiningSessionId),
            Status = "OPEN", CreatedAtUtc = DateTime.UtcNow
        };
        _db.ReadyDishNotifications.Add(entity);
        // Ask for a review once per payment (owner 05/11), in the same "Thanh toán" tab; idempotent on the same key.
        _db.ReadyDishNotifications.Add(new ReadyDishNotifications
        {
            OrderId = 0, OrderItemId = request.PaymentId, CustomerId = customerId, EventName = CustomerInbox.ReviewReminder,
            Message = "Bữa ăn thế nào? Chạm để đánh giá trong mục Hóa đơn — nhà hàng đọc từng đánh giá.",
            Status = "OPEN", CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { notificationId = entity.ReadyDishNotificationId, created = true });
    }

    private static string Points(int value) =>
        value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

    public static string PaymentSucceededMessage(decimal amount, int diningSessionId) =>
        // Vietnamese thousands separator without depending on ICU culture data in the container.
        $"Thanh toán thành công {decimal.Round(amount).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.')}đ cho phiên #{diningSessionId}. Cảm ơn quý khách!";

    [HttpPost("customers/{customerId:int}/notifications/{notificationId:long}/read")]
    public async Task<ActionResult<object>> ReadNotification(int customerId, long notificationId, CancellationToken ct)
    {
        var entity = await _db.ReadyDishNotifications.FirstOrDefaultAsync(x => x.ReadyDishNotificationId == notificationId, ct);
        if (entity is null) return NotFound(new { code = "notification_not_found", message = "Không tìm thấy thông báo." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu thông báo này." });
        if (entity.Status == "OPEN") { entity.Status = "RESOLVED"; entity.ResolvedAtUtc = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
        return Ok(new { notificationId, isRead = true, readAtUtc = entity.ResolvedAtUtc });
    }

    // Reviews (owner 05/11): one per paid invoice, editable for 24 hours, 3 criteria + optional stars per dish of that
    // invoice, quick tags, anonymous name. The Gateway checks the invoice belongs to the guest and passes its branch,
    // its dishes and the guest's name; nothing here trusts dish names or branch from the app.
    [HttpPost("customers/{customerId:int}/reviews")]
    public async Task<ActionResult<object>> CreateReview(int customerId, CreateReviewRequest request, CancellationToken ct)
    {
        if (!ValidReview(request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, request.Comment, request.ImageUrls)
            || request.InvoiceId <= 0 || request.DiningSessionId <= 0 || !ValidCommand(request.IdempotencyKey, request.RequestHash)) return Validation();
        var tags = ReviewRules.NormalizeTags(request.Tags);
        var dishes = ReviewRules.NormalizeDishRatings(request.DishRatings?.Select(x => (x.DishId, x.Rating)), InvoiceDishes(request.InvoiceDishes));
        if (tags is null || dishes is null) return Validation();
        var replay = await _db.CustomerReviews.Include(x => x.DishRatings).FirstOrDefaultAsync(x => x.CreateIdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.RequestHash == request.RequestHash ? Ok(MapReview(replay, DateTime.UtcNow)) : IdempotencyConflict();
        if (await _db.CustomerReviews.AnyAsync(x => x.InvoiceId == request.InvoiceId, ct)) return Conflict(new { code = "review_already_exists", message = "Hóa đơn này đã được đánh giá." });
        var now = DateTime.UtcNow;
        var entity = new CustomerReview { CustomerId = customerId, InvoiceId = request.InvoiceId, DiningSessionId = request.DiningSessionId,
            OverallRating = request.OverallRating, FoodRating = request.FoodRating, ServiceRating = request.ServiceRating,
            SpaceRating = request.SpaceRating, Comment = Clean(request.Comment), ImageUrlsJson = JsonSerializer.Serialize(request.ImageUrls ?? Array.Empty<string>()),
            TagsJson = JsonSerializer.Serialize(tags), IsAnonymous = request.IsAnonymous, BranchId = request.BranchId is > 0 ? request.BranchId : null,
            AuthorNameSnapshot = Trim(request.AuthorName, 200),
            CreateIdempotencyKey = request.IdempotencyKey.Trim(), RequestHash = request.RequestHash, CreatedAtUtc = now, UpdatedAtUtc = now };
        foreach (var d in dishes) entity.DishRatings.Add(new CustomerReviewDishRating { DishId = d.DishId, DishNameSnapshot = Trim(d.DishName, 200) ?? $"Món #{d.DishId}", Rating = d.Rating });
        _db.CustomerReviews.Add(entity); await _db.SaveChangesAsync(ct); return Ok(MapReview(entity, now));
    }

    [HttpGet("customers/{customerId:int}/reviews")]
    public async Task<ActionResult<object>> Reviews(int customerId, CancellationToken ct)
    {
        var rows = await _db.CustomerReviews.AsNoTracking().Include(x => x.DishRatings).Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return Ok(new { items = rows.Select(x => MapReview(x, now)), nextCursor = (string?)null, tags = ReviewRules.Tags });
    }

    [HttpPut("customers/{customerId:int}/reviews/{reviewId:int}")]
    public async Task<ActionResult<object>> UpdateReview(int customerId, int reviewId, UpdateReviewRequest request, CancellationToken ct)
    {
        var entity = await _db.CustomerReviews.Include(x => x.DishRatings).FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (entity is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đánh giá này." });
        var now = DateTime.UtcNow;
        if (!ReviewRules.CanEdit(entity.CreatedAtUtc, now))
            return Conflict(new { code = "review_edit_window_closed", message = "Chỉ sửa được đánh giá trong 24 giờ sau khi gửi." });
        if (!ValidReview(request.OverallRating, request.FoodRating, request.ServiceRating, request.SpaceRating, request.Comment, request.ImageUrls)) return Validation();
        var tags = ReviewRules.NormalizeTags(request.Tags);
        // Dish names come from the stored ratings or the invoice the Gateway sends; the app can only re-rate those dishes.
        var known = entity.DishRatings.ToDictionary(x => x.DishId, x => x.DishNameSnapshot);
        foreach (var d in InvoiceDishes(request.InvoiceDishes)) known.TryAdd(d.Key, d.Value);
        var dishes = ReviewRules.NormalizeDishRatings(request.DishRatings?.Select(x => (x.DishId, x.Rating)), known);
        if (tags is null || dishes is null) return Validation();
        entity.OverallRating = request.OverallRating; entity.FoodRating = request.FoodRating; entity.ServiceRating = request.ServiceRating;
        entity.SpaceRating = request.SpaceRating; entity.Comment = Clean(request.Comment); entity.ImageUrlsJson = JsonSerializer.Serialize(request.ImageUrls ?? Array.Empty<string>());
        entity.TagsJson = JsonSerializer.Serialize(tags); entity.IsAnonymous = request.IsAnonymous; entity.UpdatedAtUtc = now;
        _db.CustomerReviewDishRatings.RemoveRange(entity.DishRatings);
        entity.DishRatings.Clear();
        foreach (var d in dishes) entity.DishRatings.Add(new CustomerReviewDishRating { DishId = d.DishId, DishNameSnapshot = Trim(d.DishName, 200) ?? $"Món #{d.DishId}", Rating = d.Rating });
        await _db.SaveChangesAsync(ct); return Ok(MapReview(entity, now));
    }

    [HttpDelete("customers/{customerId:int}/reviews/{reviewId:int}")]
    public async Task<ActionResult> DeleteReview(int customerId, int reviewId, CancellationToken ct)
    {
        var entity = await _db.CustomerReviews.FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (entity is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        if (entity.CustomerId != customerId) return StatusCode(403, new { code = "forbidden", message = "Bạn không sở hữu đánh giá này." });
        if (!ReviewRules.CanEdit(entity.CreatedAtUtc, DateTime.UtcNow))
            return Conflict(new { code = "review_edit_window_closed", message = "Chỉ xóa được đánh giá trong 24 giờ sau khi gửi." });
        _db.CustomerReviews.Remove(entity); await _db.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>
    /// "Chi tiết món": average, count and the latest comments of one dish, from the stars guests gave that dish
    /// (hidden reviews never count). Nothing yet = count 0 and average null ("Chưa có đánh giá").
    /// </summary>
    [HttpGet("dishes/{dishId:int}/review-summary")]
    public async Task<ActionResult<object>> DishReviewSummary(int dishId, [FromQuery] int latest = 3, CancellationToken ct = default)
    {
        if (dishId <= 0) return Validation();
        latest = Math.Clamp(latest, 0, 10);
        var visible = _db.CustomerReviewDishRatings.AsNoTracking().Where(x => x.DishId == dishId && !x.Review.IsHidden);
        var stars = await visible.Select(x => x.Rating).ToListAsync(ct);
        var recent = await visible.OrderByDescending(x => x.Review.CreatedAtUtc)
            .Select(x => new { x.Rating, x.Review.Comment, x.Review.IsAnonymous, x.Review.AuthorNameSnapshot, x.Review.CreatedAtUtc, x.Review.ReplyText, x.Review.TagsJson })
            .Take(latest * 3).ToListAsync(ct);
        var latestRows = recent.Where(x => Clean(x.Comment) is not null).Take(latest).Select(x => new
        {
            authorName = ReviewRules.PublicName(x.AuthorNameSnapshot, x.IsAnonymous), rating = x.Rating, comment = Clean(x.Comment),
            tags = JsonSerializer.Deserialize<string[]>(x.TagsJson ?? "[]") ?? Array.Empty<string>(), x.CreatedAtUtc, replyText = Clean(x.ReplyText)
        });
        return Ok(new { dishId, count = stars.Count, average = ReviewRules.Average(stars), latest = latestRows });
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
    private static object MapReview(CustomerReview x, DateTime nowUtc) => new
    {
        reviewId = x.CustomerReviewId, x.InvoiceId, x.DiningSessionId, x.BranchId, x.OverallRating, x.FoodRating, x.ServiceRating,
        x.SpaceRating, Comment = Clean(x.Comment), imageUrls = JsonSerializer.Deserialize<string[]>(x.ImageUrlsJson) ?? Array.Empty<string>(),
        tags = JsonSerializer.Deserialize<string[]>(x.TagsJson ?? "[]") ?? Array.Empty<string>(), x.IsAnonymous,
        dishRatings = x.DishRatings.OrderBy(d => d.DishNameSnapshot).Select(d => new { d.DishId, dishName = d.DishNameSnapshot, d.Rating }),
        canEdit = ReviewRules.CanEdit(x.CreatedAtUtc, nowUtc), editableUntilUtc = ReviewRules.EditableUntil(x.CreatedAtUtc),
        replyText = Clean(x.ReplyText), x.RepliedAtUtc, x.IsHidden, hiddenReason = x.IsHidden ? x.HiddenReason : null,
        x.CreatedAtUtc, x.UpdatedAtUtc
    };
    private static IReadOnlyDictionary<int, string> InvoiceDishes(IReadOnlyList<InvoiceDish>? dishes) =>
        (dishes ?? Array.Empty<InvoiceDish>()).Where(x => x.DishId > 0).GroupBy(x => x.DishId)
            .ToDictionary(g => g.Key, g => string.IsNullOrWhiteSpace(g.First().DishName) ? $"Món #{g.Key}" : g.First().DishName!.Trim());
    private static string? Trim(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
    }
    private static object MapSupport(SupportRequest x) => new { x.SupportRequestId, x.RequestCode, x.Category, x.Subject, x.Message, x.OrderId, x.ReservationId, x.PaymentId, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc };
    private static bool ValidCommand(string? key, string? hash) => !string.IsNullOrWhiteSpace(key) && key.Length <= 140 && !string.IsNullOrWhiteSpace(hash) && hash.Length == 64;
    private static bool ValidReview(int overall, int food, int service, int space, string? comment, IReadOnlyList<string>? images) =>
        overall is >= 1 and <= 5 && food is >= 1 and <= 5 && service is >= 1 and <= 5 && space is >= 1 and <= 5
        && comment?.Length <= 2000 && (images?.Count ?? 0) <= 5 && (images?.All(x => Uri.TryCreate(x, UriKind.Absolute, out _)) ?? true);
    // Free text from clients: blank or a leftover "null"/"undefined" (older apps sent the word) is stored and returned as null.
    public static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            || text.Equals("undefined", StringComparison.OrdinalIgnoreCase) ? null : text;
    }
    private ActionResult Validation() => BadRequest(new { code = "validation_failed", message = "Dữ liệu không hợp lệ." });
    private ActionResult IdempotencyConflict() => Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });

    public sealed record FavoriteRequest(string DishName);
    public sealed record PaymentSucceededRequest(int PaymentId, int DiningSessionId, decimal Amount);
    public sealed record AddNotificationRequest(string EventName, int RefId, string Message, int? OrderId = null);
    public sealed record LoyaltyNotificationRequest(int PaymentId, int PointsBefore, int PointsEarned, int PointsAfter);
    public sealed record DishRatingInput(int DishId, int Rating);
    public sealed record InvoiceDish(int DishId, string? DishName);
    public sealed record CreateReviewRequest(int InvoiceId, int DiningSessionId, int OverallRating, int FoodRating, int ServiceRating, int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls, string IdempotencyKey, string RequestHash,
        IReadOnlyList<string>? Tags = null, bool IsAnonymous = false, IReadOnlyList<DishRatingInput>? DishRatings = null,
        int? BranchId = null, string? AuthorName = null, IReadOnlyList<InvoiceDish>? InvoiceDishes = null);
    public sealed record UpdateReviewRequest(int OverallRating, int FoodRating, int ServiceRating, int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls,
        IReadOnlyList<string>? Tags = null, bool IsAnonymous = false, IReadOnlyList<DishRatingInput>? DishRatings = null,
        IReadOnlyList<InvoiceDish>? InvoiceDishes = null);
    public sealed record CreateSupportRequest(string? Category, string? Subject, string? Message, int? OrderId, int? ReservationId, int? PaymentId, string IdempotencyKey, string RequestHash);
}
