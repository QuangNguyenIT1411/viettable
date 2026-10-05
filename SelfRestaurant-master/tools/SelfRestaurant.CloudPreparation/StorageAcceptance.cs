using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SelfRestaurant.Gateway.Api.Infrastructure.Media;

internal static class StorageAcceptance
{
    private const string Bucket = "viettable-staging-media";

    public static async Task RunAsync(Input input)
    {
        if (string.IsNullOrWhiteSpace(input.StorageServiceKey))
            throw new InvalidOperationException("StorageServiceKey required through DPAPI stdin.");
        var project = new Uri($"https://{input.ProjectRef}.supabase.co/");
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(30) };
        var settings = new SupabaseMediaSettings(project, Bucket, input.StorageServiceKey);
        if (input.Action == "StorageUpload")
        {
            // Valid, tiny 1x1 PNG: test artifact only, never a production fallback.
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lZkAAAAASUVORK5CYII=");
            using var stream = new MemoryStream(bytes);
            IDishMediaStorage storage = new SupabaseDishMediaStorage(http, settings);
            var url = await storage.SaveAsync("qa-staging-storage.png", stream, null, CancellationToken.None);
            var receipt = new Uri(url).AbsolutePath;
            var read = await http.GetByteArrayAsync(url);
            if (!read.SequenceEqual(bytes)) throw new InvalidOperationException("Storage byte mismatch.");
            Console.WriteLine("PASS: Supabase abstraction upload and public read (PNG byte checksum).");
            // This is a public object path, not a credential. Loader stores outside Git.
            Console.WriteLine("STORAGE_RECEIPT:" + receipt);
            return;
        }
        var prefix = "/storage/v1/object/public/" + Bucket + "/dishes/qa-staging-storage-";
        if (!input.StorageReceipt.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(input.StorageReceipt[prefix.Length..].Replace(".png", "", StringComparison.Ordinal), "N", out _)
            || !input.StorageReceipt.EndsWith(".png", StringComparison.Ordinal))
            throw new InvalidOperationException("Only the exact staging test object may be verified/deleted.");
        var objectKey = input.StorageReceipt[("/storage/v1/object/public/" + Bucket + "/").Length..];
        var persisted = await http.GetByteArrayAsync(new Uri(project, input.StorageReceipt));
        if (persisted.Length == 0) throw new InvalidOperationException("Stored object missing after process restart.");
        Console.WriteLine("PASS: object persistence after uploader process/container exit.");
        using var anonRequest = new HttpRequestMessage(HttpMethod.Post,
            new Uri(project, "storage/v1/object/" + Bucket + "/qa-anonymous-write-" + Guid.NewGuid().ToString("N") + ".png"));
        anonRequest.Content = new ByteArrayContent(persisted);
        anonRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var anonymous = await http.SendAsync(anonRequest);
        if (anonymous.IsSuccessStatusCode || (int)anonymous.StatusCode < 400 || (int)anonymous.StatusCode >= 500)
            throw new InvalidOperationException("Unauthenticated write was not rejected; audit required.");
        Console.WriteLine($"PASS: unauthenticated write rejected HTTP {(int)anonymous.StatusCode} (not an anon-key RLS test).");
        using var delete = new HttpRequestMessage(HttpMethod.Delete, new Uri(project, "storage/v1/object/" + Bucket));
        delete.Headers.Add("apikey", settings.ServiceKey);
        if (!settings.ServiceKey.StartsWith("sb_secret_", StringComparison.Ordinal))
            delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ServiceKey);
        delete.Content = new StringContent(JsonSerializer.Serialize(new { prefixes = new[] { objectKey } }),
            System.Text.Encoding.UTF8, "application/json");
        using var removed = await http.SendAsync(delete);
        if (!removed.IsSuccessStatusCode) throw new InvalidOperationException("Temporary object cleanup rejected.");
        Console.WriteLine("PASS: exact temporary object deleted using authenticated Storage API.");
    }
}
