namespace SelfRestaurant.Gateway.Api.Models;

public sealed record CreateReviewBody(int InvoiceId, int OverallRating, int FoodRating, int ServiceRating,
    int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls);
public sealed record UpdateReviewBody(int OverallRating, int FoodRating, int ServiceRating,
    int SpaceRating, string? Comment, IReadOnlyList<string>? ImageUrls);
public sealed record CreateSupportBody(string? Category, string? Subject, string? Message,
    int? OrderId, int? ReservationId, int? PaymentId);
