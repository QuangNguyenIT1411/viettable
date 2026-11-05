namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>
/// Review rules (owner 05/11): one review per paid invoice, editable for 24 hours, quick tags from a fixed list,
/// optional stars per dish of that invoice, optional anonymous name. Pure, so every rule is unit tested.
/// </summary>
public static class ReviewRules
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);
    public const int MaxTags = 6;
    public const int MaxDishRatings = 30;
    public const int MaxCommentLength = 2000;
    public const int MaxReplyLength = 1000;
    public const int MaxHideReasonLength = 300;

    /// <summary>The quick tags the app offers; anything else is refused (no free-text tags).</summary>
    public static readonly IReadOnlyList<string> Tags = new[]
    {
        "Ngon", "Nhanh", "Đáng tiền", "Phục vụ tốt", "Không gian đẹp", "Sạch sẽ", "Cần cải thiện", "Chờ lâu"
    };

    public static bool CanEdit(DateTime createdAtUtc, DateTime nowUtc) => nowUtc - createdAtUtc <= EditWindow;
    public static DateTime EditableUntil(DateTime createdAtUtc) => createdAtUtc + EditWindow;

    public static bool ValidStars(int value) => value is >= 1 and <= 5;

    /// <summary>The tags kept in the order of <see cref="Tags"/>, or null when one is unknown or there are too many.</summary>
    public static IReadOnlyList<string>? NormalizeTags(IEnumerable<string>? tags)
    {
        var wanted = (tags ?? Array.Empty<string>()).Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct().ToArray();
        if (wanted.Length > MaxTags || wanted.Any(x => !Tags.Contains(x))) return null;
        return Tags.Where(wanted.Contains).ToArray();
    }

    /// <summary>
    /// Stars per dish: only dishes of this invoice (by id), 1–5, one per dish. Null = invalid. The dish name comes
    /// from the invoice, never from the client.
    /// </summary>
    public static IReadOnlyList<(int DishId, string DishName, int Rating)>? NormalizeDishRatings(
        IEnumerable<(int DishId, int Rating)>? ratings, IReadOnlyDictionary<int, string> invoiceDishes)
    {
        var list = (ratings ?? Array.Empty<(int, int)>()).ToArray();
        if (list.Length > MaxDishRatings) return null;
        if (list.Select(x => x.DishId).Distinct().Count() != list.Length) return null;
        if (list.Any(x => !ValidStars(x.Rating) || !invoiceDishes.ContainsKey(x.DishId))) return null;
        return list.Select(x => (x.DishId, invoiceDishes[x.DishId], x.Rating)).ToArray();
    }

    /// <summary>How another guest sees the author: "Vinh Q." style, or "Khách ẩn danh".</summary>
    public static string PublicName(string? fullName, bool anonymous)
    {
        if (anonymous) return "Khách ẩn danh";
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "Khách hàng";
        var given = Capitalize(parts[^1]);
        return parts.Length == 1 ? given : $"{given} {char.ToUpperInvariant(parts[0][0])}.";
    }

    private static string Capitalize(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    /// <summary>Average to one decimal (half up); null when there is nothing to average.</summary>
    public static double? Average(IEnumerable<int> stars)
    {
        var list = stars.ToArray();
        return list.Length == 0 ? null : Math.Round(list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}
