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
    public string CreateIdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
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
