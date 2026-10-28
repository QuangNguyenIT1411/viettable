using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// Promotions (docs/PROMO_PLAN.md). The Gateway checks the admin for writes; reads of running promotions are public
/// through the Gateway. The discount itself is only ever computed by CheckoutPricing.
/// </summary>
[ApiController]
[Route("api/internal/promotions")]
public sealed class InternalPromotionsController(BillingDbContext db) : ControllerBase
{
    /// <summary>Admin list: every promotion, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await db.Promotions.AsNoTracking().OrderByDescending(x => x.PromotionId).Take(500).ToListAsync(ct);
        return Ok(rows.Select(x => Map(x, now)).ToArray());
    }

    /// <summary>Running now for one branch (or every branch when none is given), best discount first.</summary>
    [HttpGet("active")]
    public async Task<ActionResult<object>> Active([FromQuery] int? branchId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await RunningAsync(db, now, ct);
        var visible = rows.Where(x => branchId is not > 0 || CheckoutPricing.ParseBranches(x.BranchIds) is var b && (b.Count == 0 || b.Contains(branchId.Value)))
            .OrderByDescending(x => x.DiscountType == CheckoutPricing.Fixed ? x.DiscountValue : x.MaxDiscount ?? x.DiscountValue)
            .ThenBy(x => x.EndsAtUtc).ThenBy(x => x.PromotionId).ToArray();
        return Ok(visible.Select(x => Map(x, now)).ToArray());
    }

    [HttpGet("{promotionId:int}")]
    public async Task<ActionResult<object>> Get(int promotionId, CancellationToken ct)
    {
        var row = await db.Promotions.AsNoTracking().FirstOrDefaultAsync(x => x.PromotionId == promotionId, ct);
        return row is null ? NotFound(new { code = "promotion_not_found", message = "Không tìm thấy khuyến mãi." }) : Ok(Map(row, DateTime.UtcNow));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(PromotionRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return error;
        var now = DateTime.UtcNow;
        var row = new Promotion { CreatedAtUtc = now };
        Fill(row, request, now);
        db.Promotions.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(Map(row, now));
    }

    [HttpPut("{promotionId:int}")]
    public async Task<ActionResult<object>> Update(int promotionId, PromotionRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return error;
        var row = await db.Promotions.FirstOrDefaultAsync(x => x.PromotionId == promotionId, ct);
        if (row is null) return NotFound(new { code = "promotion_not_found", message = "Không tìm thấy khuyến mãi." });
        var now = DateTime.UtcNow;
        Fill(row, request, now);
        await db.SaveChangesAsync(ct);
        return Ok(Map(row, now));
    }

    [HttpPost("{promotionId:int}/toggle")]
    public async Task<ActionResult<object>> Toggle(int promotionId, ToggleRequest request, CancellationToken ct)
    {
        var row = await db.Promotions.FirstOrDefaultAsync(x => x.PromotionId == promotionId, ct);
        if (row is null) return NotFound(new { code = "promotion_not_found", message = "Không tìm thấy khuyến mãi." });
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(row, DateTime.UtcNow));
    }

    /// <summary>
    /// Promotions running now that customers were not told about yet. The Gateway sends "Ưu đãi mới" (the inbox keeps one
    /// per customer and promotion, so a repeat after a failure sends nothing twice) and then marks them announced.
    /// </summary>
    [HttpGet("due-announcements")]
    public async Task<ActionResult<object>> DueAnnouncements(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok((await RunningAsync(db, now, ct)).Where(x => x.NotifiedAtUtc == null).Select(x => Map(x, now)).ToArray());
    }

    [HttpPost("{promotionId:int}/announced")]
    public async Task<ActionResult<object>> MarkAnnounced(int promotionId, CancellationToken ct)
    {
        var row = await db.Promotions.FirstOrDefaultAsync(x => x.PromotionId == promotionId, ct);
        if (row is null) return NotFound(new { code = "promotion_not_found", message = "Không tìm thấy khuyến mãi." });
        row.NotifiedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(row, DateTime.UtcNow));
    }

    internal static async Task<List<Promotion>> RunningAsync(BillingDbContext db, DateTime nowUtc, CancellationToken ct) =>
        await db.Promotions.AsNoTracking().Where(x => x.IsActive && x.StartsAtUtc <= nowUtc && x.EndsAtUtc >= nowUtc).ToListAsync(ct);

    /// <summary>Offers the pricing may use for a branch now.</summary>
    internal static async Task<IReadOnlyList<PromotionOffer>> OffersAsync(BillingDbContext db, DateTime nowUtc, CancellationToken ct) =>
        (await RunningAsync(db, nowUtc, ct)).Select(PromotionOffer.From).ToArray();

    private ObjectResult? Validate(PromotionRequest r)
    {
        string? message = null;
        var type = r.DiscountType?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) message = "Tên khuyến mãi là bắt buộc (tối đa 200 ký tự).";
        else if (type is not (CheckoutPricing.Percent or CheckoutPricing.Fixed)) message = "Loại giảm phải là phần trăm hoặc số tiền cố định.";
        else if (r.DiscountValue <= 0) message = "Mức giảm phải lớn hơn 0.";
        else if (type == CheckoutPricing.Percent && r.DiscountValue > 100) message = "Giảm theo phần trăm tối đa 100%.";
        else if (r.MaxDiscount is < 0 || r.MinSubtotal < 0) message = "Trần giảm và đơn tối thiểu không được âm.";
        else if (CheckoutPricing.MoneyError("Đơn tối thiểu", r.MinSubtotal) is { } min) message = min;
        else if (type == CheckoutPricing.Percent && CheckoutPricing.MoneyError("Trần giảm", r.MaxDiscount) is { } cap) message = cap;
        else if (type == CheckoutPricing.Fixed && CheckoutPricing.MoneyError("Số tiền giảm", r.DiscountValue) is { } amount) message = amount;
        else if (r.EndsAtUtc <= r.StartsAtUtc) message = "Thời gian kết thúc phải sau thời gian bắt đầu.";
        else if ((r.Description?.Length ?? 0) > 1000 || (r.ImageUrl?.Length ?? 0) > 1000) message = "Mô tả hoặc đường dẫn ảnh quá dài.";
        else if (!string.IsNullOrWhiteSpace(r.ImageUrl)
                 && !(Uri.TryCreate(r.ImageUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            message = "Ảnh phải là đường dẫn https.";
        return message is null ? null : BadRequest(new { code = "validation_failed", message });
    }

    private static void Fill(Promotion row, PromotionRequest r, DateTime now)
    {
        var type = r.DiscountType!.Trim().ToUpperInvariant();
        row.Name = r.Name!.Trim();
        row.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        row.ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl.Trim();
        row.DiscountType = type;
        row.DiscountValue = r.DiscountValue;
        row.MaxDiscount = type == CheckoutPricing.Percent && r.MaxDiscount is > 0 ? r.MaxDiscount : null;
        row.MinSubtotal = r.MinSubtotal;
        var branches = (r.BranchIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        row.BranchIds = branches.Length == 0 ? null : string.Join(',', branches);
        row.StartsAtUtc = DateTime.SpecifyKind(r.StartsAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        row.EndsAtUtc = DateTime.SpecifyKind(r.EndsAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        row.IsActive = r.IsActive;
        row.UpdatedAtUtc = now;
    }

    /// <summary>"Giảm 10% (tối đa 50.000đ) cho hóa đơn từ 300.000đ".</summary>
    public static string Summary(Promotion p)
    {
        var amount = p.DiscountType == CheckoutPricing.Percent
            ? $"Giảm {p.DiscountValue:0.##}%" + (p.MaxDiscount is > 0 ? $" (tối đa {CheckoutPricing.Money(p.MaxDiscount.Value)})" : "")
            : $"Giảm {CheckoutPricing.Money(p.DiscountValue)}";
        return amount + (p.MinSubtotal > 0 ? $" cho hóa đơn từ {CheckoutPricing.Money(p.MinSubtotal)}" : " cho mọi hóa đơn");
    }

    public static object Map(Promotion p, DateTime nowUtc) => new
    {
        p.PromotionId, p.Name, p.Description, p.ImageUrl, p.DiscountType, p.DiscountValue, p.MaxDiscount, p.MinSubtotal,
        branchIds = CheckoutPricing.ParseBranches(p.BranchIds).OrderBy(x => x).ToArray(), p.StartsAtUtc, p.EndsAtUtc, p.IsActive,
        isRunning = p.IsActive && nowUtc >= p.StartsAtUtc && nowUtc <= p.EndsAtUtc, summary = Summary(p), p.NotifiedAtUtc,
    };

    public sealed record PromotionRequest(string? Name, string? Description, string? ImageUrl, string? DiscountType, decimal DiscountValue,
        decimal? MaxDiscount, decimal MinSubtotal, IReadOnlyList<int>? BranchIds, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsActive = true);

    public sealed record ToggleRequest(bool IsActive);
}
