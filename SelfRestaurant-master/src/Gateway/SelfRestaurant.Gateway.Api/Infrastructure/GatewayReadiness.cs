namespace SelfRestaurant.Gateway.Api.Infrastructure;

public sealed class GatewayReadiness(HttpClient http, IConfiguration configuration, StackExchange.Redis.IConnectionMultiplexer? redis = null)
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (redis is not null)
        {
            try { await redis.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromSeconds(3), ct); }
            catch { return false; }
        }
        var services = new[] { ("Catalog", 5101), ("Orders", 5102), ("Customers", 5103), ("Identity", 5104), ("Billing", 5105) };
        var results = await Task.WhenAll(services.Select(async service =>
        {
            try
            {
                var root = configuration["Services:" + service.Item1] ?? $"http://localhost:{service.Item2}";
                using var response = await http.GetAsync(root.TrimEnd('/') + "/readyz", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
            {
                return false; // Status only: never expose an internal URL or credential.
            }
        }));
        return results.All(ready => ready);
    }
}
