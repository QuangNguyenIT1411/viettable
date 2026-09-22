using System.Net.Http.Json;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace SelfRestaurant.Orders.Api.Infrastructure;

/// <summary>
/// Cached read boundary for customer loyalty lookups used by Orders.Api.
/// Orders keeps customer ownership outside its write-model, so phone-based customer
/// attachment must go through this client instead of querying Identity data directly.
/// </summary>
public sealed class CustomersApiClient : ICustomerLoyaltyReadModel
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan LoyaltyLookupCacheTtl = TimeSpan.FromMinutes(2);

    public CustomersApiClient(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public Task<CustomerLoyaltySnapshot?> GetLoyaltyByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var normalizedPhone = phoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return Task.FromResult<CustomerLoyaltySnapshot?>(null);
        }

        return _cache.GetOrCreateAsync($"identity:customer-loyalty-by-phone:{normalizedPhone}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LoyaltyLookupCacheTtl;
            var response = await _http.GetAsync(
                $"/api/internal/customers/loyalty/by-phone?phoneNumber={Uri.EscapeDataString(normalizedPhone)}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CustomerLoyaltySnapshot>(cancellationToken);
        })!;
    }

}
