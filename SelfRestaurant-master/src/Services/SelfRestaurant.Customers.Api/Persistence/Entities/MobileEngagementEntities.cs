namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public sealed class CustomerFavorite
{
    public int CustomerFavoriteId { get; set; }
    public int CustomerId { get; set; }
    public int DishId { get; set; }
    public string DishNameSnapshot { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CustomerReview
{
    public int CustomerReviewId { get; set; }
    public int CustomerId { get; set; }
    public int InvoiceId { get; set; }
    public int DiningSessionId { get; set; }
    public int OverallRating { get; set; }
    public int FoodRating { get; set; }
    public int ServiceRating { get; set; }
    public int SpaceRating { get; set; }
    public string? Comment { get; set; }
    public string ImageUrlsJson { get; set; } = "[]";
    /// <summary>Quick tags from ReviewRules.Tags (owner 05/11), JSON array.</summary>
    public string TagsJson { get; set; } = "[]";
    public bool IsAnonymous { get; set; }
    /// <summary>Full name when the review was written (Identity owns customers); shown as "Vinh Q." unless anonymous.</summary>
    public string? AuthorNameSnapshot { get; set; }
    /// <summary>Branch of the invoice (from Billing via the Gateway), for the admin filter and statistics.</summary>
    public int? BranchId { get; set; }
    /// <summary>The restaurant's answer, shown to the guest under the review.</summary>
    public string? ReplyText { get; set; }
    public DateTime? RepliedAtUtc { get; set; }
    public int? RepliedByEmployeeId { get; set; }
    public string? RepliedByName { get; set; }
    /// <summary>Hidden by an admin (rule-breaking): kept, but out of every public list and average.</summary>
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    public DateTime? HiddenAtUtc { get; set; }
    public int? HiddenByEmployeeId { get; set; }
    public string? HiddenByName { get; set; }
    public ICollection<CustomerReviewDishRating> DishRatings { get; set; } = new List<CustomerReviewDishRating>();
    public string CreateIdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Optional stars for one dish of the reviewed invoice (owner 05/11).</summary>
public sealed class CustomerReviewDishRating
{
    public int CustomerReviewDishRatingId { get; set; }
    public int CustomerReviewId { get; set; }
    public int DishId { get; set; }
    public string DishNameSnapshot { get; set; } = null!;
    public int Rating { get; set; }
    public CustomerReview Review { get; set; } = null!;
}

public sealed class SupportRequest
{
    public int SupportRequestId { get; set; }
    public string RequestCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public string Category { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int? PaymentId { get; set; }
    public string Status { get; set; } = "OPEN";
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
