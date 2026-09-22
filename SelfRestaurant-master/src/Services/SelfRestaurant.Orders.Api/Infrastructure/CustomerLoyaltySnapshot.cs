namespace SelfRestaurant.Orders.Api.Infrastructure;

public sealed record CustomerLoyaltySnapshot(
    int CustomerId,
    string Name,
    string Phone,
    int CurrentPoints,
    int CardPoints,
    int? CardId);
