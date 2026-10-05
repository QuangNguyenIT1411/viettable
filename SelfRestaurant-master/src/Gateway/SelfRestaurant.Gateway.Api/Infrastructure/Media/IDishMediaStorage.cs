namespace SelfRestaurant.Gateway.Api.Infrastructure.Media;

public interface IDishMediaStorage
{
    // The key is a sanitized filename, not an absolute filesystem path. A future
    // object-storage adapter can return a public CDN URL without changing callers.
    Task<string> SaveAsync(string fileName, Stream content, string? previousPublicPath, CancellationToken ct);
}
