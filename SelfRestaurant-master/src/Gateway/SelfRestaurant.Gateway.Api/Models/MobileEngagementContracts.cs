namespace SelfRestaurant.Gateway.Api.Models;

public sealed record ReviewDishRatingBody(int DishId, int Rating);
public sealed record CreateReviewBody(int InvoiceId, int OverallRating, int FoodRating, int ServiceRating,
    int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls, IReadOnlyList<string>? Tags = null,
    bool IsAnonymous = false, IReadOnlyList<ReviewDishRatingBody>? DishRatings = null);
public sealed record UpdateReviewBody(int OverallRating, int FoodRating, int ServiceRating,
    int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls, IReadOnlyList<string>? Tags = null,
    bool IsAnonymous = false, IReadOnlyList<ReviewDishRatingBody>? DishRatings = null, int? InvoiceId = null);
public sealed record CreateSupportBody(string? Category, string? Subject, string? Message,
    int? OrderId, int? ReservationId, int? PaymentId);
