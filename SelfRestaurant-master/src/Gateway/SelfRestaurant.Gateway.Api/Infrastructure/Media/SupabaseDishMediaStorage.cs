using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace SelfRestaurant.Gateway.Api.Infrastructure.Media;

public sealed record SupabaseMediaSettings(Uri ProjectUrl, string Bucket, string ServiceKey)
{
    public static SupabaseMediaSettings FromConfiguration(IConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration["MediaStorage:Supabase:ProjectUrl"], UriKind.Absolute, out var url)
            || url.Scheme != "https" || !url.IsDefaultPort || !string.IsNullOrEmpty(url.UserInfo)
            || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment)
            || url.AbsolutePath != "/" || !url.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid Supabase media project URL.");
        var bucket = configuration["MediaStorage:Supabase:Bucket"] ?? "";
        if (!Regex.IsMatch(bucket, "^[a-z0-9][a-z0-9-]{0,62}$"))
            throw new InvalidOperationException("Invalid Supabase media bucket.");
        var key = configuration["MediaStorage:Supabase:ServiceKey"] ?? "";
        if (string.IsNullOrWhiteSpace(key) || key.Contains('\r') || key.Contains('\n') || key.StartsWith("sb_publishable_"))
            throw new InvalidOperationException("Backend Supabase service key is required.");
        return new SupabaseMediaSettings(url, bucket, key);
    }

    // Prevent record.ToString() from accidentally including credential material.
    public override string ToString() => "SupabaseMediaSettings (credentials redacted)";
}

public sealed class SupabaseDishMediaStorage(HttpClient http, SupabaseMediaSettings settings) : IDishMediaStorage
{
    public async Task<string> SaveAsync(string fileName, Stream content, string? previousPublicPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 160
            || !Regex.IsMatch(fileName, "^[a-zA-Z0-9][a-zA-Z0-9_-]*\\.(jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Invalid media storage key.");
        var mime = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg"
        };
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await content.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > 5 * 1024 * 1024)
                throw new InvalidOperationException("Media exceeds 5 MiB limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        if (buffer.Length == 0) throw new InvalidOperationException("Media is empty.");
        // Immutable object names prevent CDN-cached same-key replacements. Keep previous
        // objects until an explicit cleanup AFTER Catalog commits the new URL. Never
        // delete a caller-supplied URL before the separate database operation succeeds.
        var objectKey = "dishes/" + Path.GetFileNameWithoutExtension(fileName) + "-" + Guid.NewGuid().ToString("N")
            + Path.GetExtension(fileName).ToLowerInvariant();
        var path = "storage/v1/object/" + settings.Bucket + "/" + objectKey;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.ProjectUrl, path));
        request.Headers.Add("apikey", settings.ServiceKey);
        if (!settings.ServiceKey.StartsWith("sb_secret_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ServiceKey);
        request.Content = new ByteArrayContent(buffer.ToArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(365) };
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (HttpRequestException) { throw new InvalidOperationException("Cloud media upload unavailable."); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Cloud media upload rejected (HTTP {(int)response.StatusCode}).");
        }
        return new Uri(settings.ProjectUrl, "storage/v1/object/public/" + settings.Bucket + "/" + objectKey).AbsoluteUri;
    }
}
