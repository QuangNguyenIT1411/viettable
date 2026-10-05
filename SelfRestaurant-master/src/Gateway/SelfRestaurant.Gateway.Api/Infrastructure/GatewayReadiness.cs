namespace SelfRestaurant.Gateway.Api.Infrastructure;

public sealed class GatewayReadiness(HttpClient http, IConfiguration configuration)
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
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
