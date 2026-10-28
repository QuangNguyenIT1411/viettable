using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Controllers;

/// <summary>
/// Vouchers for the admin (owner 28/10/2026): code, discount, minimum bill, period, uses per customer (1, N or no limit)
/// and an optional total. The Gateway checks the admin. A voucher is only ever applied when the guest picks it.
/// </summary>
[ApiController]
[Route("api/internal/vouchers")]
public sealed partial class InternalVouchersController(BillingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct)
    {
        var rows = await db.Vouchers.AsNoTracking().OrderByDescending(x => x.VoucherId).Take(500).ToListAsync(ct);
        var used = await UsedTotalsAsync(rows.Select(x => x.VoucherId).ToArray(), ct);
        var now = DateTime.UtcNow;
        return Ok(rows.Select(x => Map(x, used.TryGetValue(x.VoucherId, out var n) ? n : 0, now)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(VoucherAdminRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return error;
        var code = NormalizeCode(request.Code);
        if (await db.Vouchers.AnyAsync(x => x.Code == code, ct)) return CodeTaken();
        var row = new Voucher();
        Fill(row, request);
        db.Vouchers.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return CodeTaken(); }
        return Ok(Map(row, 0, DateTime.UtcNow));
    }

    [HttpPut("{voucherId:int}")]
    public async Task<ActionResult<object>> Update(int voucherId, VoucherAdminRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return error;
        var row = await db.Vouchers.FirstOrDefaultAsync(x => x.VoucherId == voucherId, ct);
        if (row is null) return NotFound(new { code = VoucherRules.NotFound, message = "Không tìm thấy voucher." });
        var code = NormalizeCode(request.Code);
        if (await db.Vouchers.AnyAsync(x => x.Code == code && x.VoucherId != voucherId, ct)) return CodeTaken();
        Fill(row, request);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return CodeTaken(); }
        var used = await UsedTotalsAsync(new[] { voucherId }, ct);
        return Ok(Map(row, used.TryGetValue(voucherId, out var n) ? n : 0, DateTime.UtcNow));
    }

    [HttpPost("{voucherId:int}/toggle")]
    public async Task<ActionResult<object>> Toggle(int voucherId, ToggleRequest request, CancellationToken ct)
    {
        var row = await db.Vouchers.FirstOrDefaultAsync(x => x.VoucherId == voucherId, ct);
        if (row is null) return NotFound(new { code = VoucherRules.NotFound, message = "Không tìm thấy voucher." });
        row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        var used = await UsedTotalsAsync(new[] { voucherId }, ct);
        return Ok(Map(row, used.TryGetValue(voucherId, out var n) ? n : 0, DateTime.UtcNow));
    }

    /// <summary>Uses counted like the checkout does (paid or waiting for the money).</summary>
    private async Task<Dictionary<int, int>> UsedTotalsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        (await VoucherUsage.CountAsync(db, ids, 0, null, ct)).ToDictionary(x => x.Key, x => x.Value.UsedTotal);

    [GeneratedRegex("^[A-Z0-9_-]{3,50}$")]
    private static partial Regex CodePattern();

    private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private ObjectResult? Validate(VoucherAdminRequest r)
    {
        string? message = null;
        var type = r.DiscountType?.Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(NormalizeCode(r.Code))) message = "Mã voucher gồm 3–50 ký tự: chữ không dấu, số, gạch ngang hoặc gạch dưới.";
        else if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) message = "Tên voucher là bắt buộc (tối đa 200 ký tự).";
        else if (type is not (CheckoutPricing.Percent or CheckoutPricing.Fixed)) message = "Loại giảm phải là phần trăm hoặc số tiền cố định.";
        else if (r.DiscountValue <= 0) message = "Mức giảm phải lớn hơn 0.";
        else if (type == CheckoutPricing.Percent && r.DiscountValue > 100) message = "Giảm theo phần trăm tối đa 100%.";
        else if (r.MaxDiscount is < 0 || r.MinSubtotal < 0) message = "Trần giảm và đơn tối thiểu không được âm.";
        else if (r.EndsAtUtc <= r.StartsAtUtc) message = "Thời gian kết thúc phải sau thời gian bắt đầu.";
        else if (r.MaxUsesPerCustomer is < 1 or > 1000) message = "Số lần mỗi khách được dùng phải từ 1 đến 1000 (bỏ trống = không giới hạn).";
        else if (r.MaxTotalUses is < 1 or > 1_000_000) message = "Tổng số lượt phải từ 1 trở lên (bỏ trống = không giới hạn).";
        return message is null ? null : BadRequest(new { code = "validation_failed", message });
    }

    private static void Fill(Voucher row, VoucherAdminRequest r)
    {
        var type = r.DiscountType!.Trim().ToUpperInvariant();
        row.Code = NormalizeCode(r.Code);
        row.Name = r.Name!.Trim();
        row.DiscountType = type;
        row.DiscountValue = r.DiscountValue;
        row.MaxDiscount = type == CheckoutPricing.Percent && r.MaxDiscount is > 0 ? r.MaxDiscount : null;
        row.MinSubtotal = r.MinSubtotal;
        row.StartsAtUtc = DateTime.SpecifyKind(r.StartsAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        row.EndsAtUtc = DateTime.SpecifyKind(r.EndsAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        row.IsActive = r.IsActive;
        row.MaxUsesPerCustomer = r.MaxUsesPerCustomer;
        row.MaxTotalUses = r.MaxTotalUses;
    }

    /// <summary>"Giảm 10% (tối đa 50.000đ) cho hóa đơn từ 300.000đ".</summary>
    public static string Summary(Voucher v)
    {
        var amount = v.DiscountType == CheckoutPricing.Percent
            ? $"Giảm {v.DiscountValue:0.##}%" + (v.MaxDiscount is > 0 ? $" (tối đa {CheckoutPricing.Money(v.MaxDiscount.Value)})" : "")
            : $"Giảm {CheckoutPricing.Money(v.DiscountValue)}";
        return amount + (v.MinSubtotal > 0 ? $" cho hóa đơn từ {CheckoutPricing.Money(v.MinSubtotal)}" : " cho mọi hóa đơn");
    }

    public static object Map(Voucher v, int usedTotal, DateTime nowUtc) => new
    {
        v.VoucherId, v.Code, v.Name, v.DiscountType, v.DiscountValue, v.MaxDiscount, v.MinSubtotal, v.StartsAtUtc, v.EndsAtUtc,
        v.IsActive, v.MaxUsesPerCustomer, v.MaxTotalUses, usedTotal,
        remainingTotal = v.MaxTotalUses is > 0 ? Math.Max(0, v.MaxTotalUses.Value - usedTotal) : (int?)null,
        isRunning = v.IsActive && nowUtc >= v.StartsAtUtc && nowUtc <= v.EndsAtUtc, summary = Summary(v),
    };

    private ObjectResult CodeTaken() => Conflict(new { code = "voucher_code_taken", message = "Mã voucher đã tồn tại." });

    public sealed record VoucherAdminRequest(string? Code, string? Name, string? DiscountType, decimal DiscountValue, decimal? MaxDiscount,
        decimal MinSubtotal, DateTime StartsAtUtc, DateTime EndsAtUtc, int? MaxUsesPerCustomer, int? MaxTotalUses, bool IsActive = true);

    public sealed record ToggleRequest(bool IsActive);
}
