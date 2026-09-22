namespace SelfRestaurant.Orders.Api.Infrastructure;

public interface ICustomerLoyaltyReadModel
{
    Task<CustomerLoyaltySnapshot?> GetLoyaltyByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
}
