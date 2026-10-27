using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// "Ưu đãi mới" (docs/PROMO_PLAN.md): each promotion that started running is announced once to every active customer.
/// Order: inbox rows first (one per customer and promotion, so a repeat adds nothing), then Billing marks it announced.
/// A failure leaves it unannounced and the next read tries again. Best effort: never fails the caller.
/// </summary>
public sealed class PromotionAnnouncer(BillingClient billing, IdentityClient identity, CustomersClient customers,
    ILogger<PromotionAnnouncer> logger)
{
    public const string EventName = "promotion.created.v1";

    public async Task AnnounceDueAsync(CancellationToken ct)
    {
        try
        {
            var due = await billing.GetDuePromotionAnnouncementsAsync(ct);
            if (due.Count == 0) return;
            var customerIds = await identity.GetActiveCustomerIdsAsync(ct);
            foreach (var promotion in due)
            {
                var id = promotion.GetProperty("promotionId").GetInt32();
                if (customerIds.Count > 0)
                    await customers.BroadcastInboxAsync(customerIds, EventName, id, Message(promotion), ct);
                await billing.MarkPromotionAnnouncedAsync(id, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogWarning("Promotion announcement deferred ({ErrorType}); the next read retries.", ex.GetType().Name); }
    }

    /// <summary>"Ưu đãi mới: Giờ vàng — Giảm 10% (tối đa 50.000đ) cho hóa đơn từ 300.000đ. Áp dụng đến 30/10/2026."</summary>
    public static string Message(JsonElement promotion)
    {
        var name = promotion.GetProperty("name").GetString();
        var summary = promotion.TryGetProperty("summary", out var s) ? s.GetString() : null;
        var until = promotion.TryGetProperty("endsAtUtc", out var e) && e.ValueKind == JsonValueKind.String
            ? TimeZoneInfo.ConvertTimeFromUtc(e.GetDateTime().ToUniversalTime(), VietnamTime()).ToString("dd/MM/yyyy") : null;
        return $"{name}: {summary}." + (until is null ? "" : $" Áp dụng đến {until}.") + " Tự áp vào hóa đơn khi đủ điều kiện.";
    }

    private static TimeZoneInfo VietnamTime()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); }
    }
}
