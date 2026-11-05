using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;

namespace SelfRestaurant.Customers.Api.Controllers;

/// <summary>
/// Admin → "Đánh giá" (owner 05/11): list with filters, the restaurant's reply (the guest sees it), hiding a
/// rule-breaking review with a reason and who hid it, and averages per branch and per dish. Reached only through the
/// Gateway's admin routes, which pass the admin's employee id and name.
/// </summary>
[ApiController]
[Route("api/internal/admin/reviews")]
public sealed class AdminReviewsController(CustomersDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] int? branchId, [FromQuery] int? stars, [FromQuery] string? visibility,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.CustomerReviews.AsNoTracking().Include(x => x.DishRatings).AsQueryable();
        if (branchId is > 0) query = query.Where(x => x.BranchId == branchId);
        if (stars is >= 1 and <= 5) query = query.Where(x => x.OverallRating == stars);
        query = visibility switch
        {
            "hidden" => query.Where(x => x.IsHidden),
            "visible" => query.Where(x => !x.IsHidden),
            "unanswered" => query.Where(x => !x.IsHidden && x.ReplyText == null),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.CustomerReviewId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new
        {
            page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items = rows.Select(x => new
            {
                reviewId = x.CustomerReviewId, x.CustomerId, x.InvoiceId, x.DiningSessionId, x.BranchId,
                authorName = x.AuthorNameSnapshot, publicName = ReviewRules.PublicName(x.AuthorNameSnapshot, x.IsAnonymous), x.IsAnonymous,
                x.OverallRating, x.FoodRating, x.ServiceRating, x.SpaceRating, comment = MobileEngagementController.Clean(x.Comment),
                tags = JsonSerializer.Deserialize<string[]>(x.TagsJson ?? "[]") ?? Array.Empty<string>(),
                dishRatings = x.DishRatings.OrderBy(d => d.DishNameSnapshot).Select(d => new { d.DishId, dishName = d.DishNameSnapshot, d.Rating }),
                x.ReplyText, x.RepliedAtUtc, x.RepliedByName, x.IsHidden, x.HiddenReason, x.HiddenAtUtc, x.HiddenByName,
                x.CreatedAtUtc, x.UpdatedAtUtc
            })
        });
    }

    public sealed record ReplyRequest(string? Text, int EmployeeId, string? EmployeeName);

    /// <summary>Writes or replaces the restaurant's answer; an empty text removes it.</summary>
    [HttpPost("{reviewId:int}/reply")]
    public async Task<ActionResult<object>> Reply(int reviewId, ReplyRequest request, CancellationToken ct)
    {
        var review = await db.CustomerReviews.FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (review is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        var text = MobileEngagementController.Clean(request.Text);
        if (text?.Length > ReviewRules.MaxReplyLength)
            return BadRequest(new { code = "validation_failed", message = $"Câu trả lời tối đa {ReviewRules.MaxReplyLength} ký tự." });
        review.ReplyText = text;
        review.RepliedAtUtc = text is null ? null : DateTime.UtcNow;
        review.RepliedByEmployeeId = text is null ? null : request.EmployeeId;
        review.RepliedByName = text is null ? null : Trim(request.EmployeeName, 200);
        await db.SaveChangesAsync(ct);
        return Ok(new { reviewId, review.ReplyText, review.RepliedAtUtc, review.RepliedByName });
    }

    public sealed record HideRequest(bool Hidden, string? Reason, int EmployeeId, string? EmployeeName);

    /// <summary>Hides (with a reason, kept with who and when) or shows again a review.</summary>
    [HttpPost("{reviewId:int}/visibility")]
    public async Task<ActionResult<object>> Visibility(int reviewId, HideRequest request, CancellationToken ct)
    {
        var review = await db.CustomerReviews.FirstOrDefaultAsync(x => x.CustomerReviewId == reviewId, ct);
        if (review is null) return NotFound(new { code = "review_not_found", message = "Không tìm thấy đánh giá." });
        var reason = MobileEngagementController.Clean(request.Reason);
        if (request.Hidden && (reason is null || reason.Length > ReviewRules.MaxHideReasonLength))
            return BadRequest(new { code = "validation_failed", message = $"Ghi lý do ẩn (tối đa {ReviewRules.MaxHideReasonLength} ký tự)." });
        review.IsHidden = request.Hidden;
        review.HiddenReason = request.Hidden ? reason : null;
        review.HiddenAtUtc = request.Hidden ? DateTime.UtcNow : null;
        review.HiddenByEmployeeId = request.Hidden ? request.EmployeeId : null;
        review.HiddenByName = request.Hidden ? Trim(request.EmployeeName, 200) : null;
        await db.SaveChangesAsync(ct);
        return Ok(new { reviewId, review.IsHidden, review.HiddenReason, review.HiddenAtUtc, review.HiddenByName });
    }

    /// <summary>Averages from visible reviews only: per branch (4 criteria) and per dish (stars per dish).</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> Stats(CancellationToken ct)
    {
        var reviews = await db.CustomerReviews.AsNoTracking().Where(x => !x.IsHidden)
            .Select(x => new { x.BranchId, x.OverallRating, x.FoodRating, x.ServiceRating, x.SpaceRating }).ToListAsync(ct);
        var dishes = await db.CustomerReviewDishRatings.AsNoTracking().Where(x => !x.Review.IsHidden)
            .Select(x => new { x.DishId, x.DishNameSnapshot, x.Rating }).ToListAsync(ct);
        return Ok(new
        {
            total = reviews.Count,
            overall = ReviewRules.Average(reviews.Select(x => x.OverallRating)),
            branches = reviews.GroupBy(x => x.BranchId).OrderBy(g => g.Key).Select(g => new
            {
                branchId = g.Key, count = g.Count(),
                overall = ReviewRules.Average(g.Select(x => x.OverallRating)), food = ReviewRules.Average(g.Select(x => x.FoodRating)),
                service = ReviewRules.Average(g.Select(x => x.ServiceRating)), space = ReviewRules.Average(g.Select(x => x.SpaceRating))
            }),
            dishes = dishes.GroupBy(x => x.DishId).Select(g => new
            {
                dishId = g.Key, dishName = g.OrderByDescending(x => x.DishNameSnapshot.Length).First().DishNameSnapshot,
                count = g.Count(), average = ReviewRules.Average(g.Select(x => x.Rating))
            }).OrderByDescending(x => x.count).ThenBy(x => x.dishName)
        });
    }

    private static string? Trim(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
    }
}
