using System.Net;
using Microsoft.Extensions.Configuration;
using SelfRestaurant.Gateway.Api.Infrastructure.Media;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class SupabaseMediaStorageTests
{
    [Theory]
    [InlineData("../a.png")]
    [InlineData("https://evil.test/a.jpg")]
    [InlineData("a.svg")]
    [InlineData("a?b.jpg")]
    public async Task Rejects_unsafe_keys_before_network(string name)
    {
        using var handler = new UploadHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var storage = new SupabaseDishMediaStorage(http, Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(name, new MemoryStream([1]), null, CancellationToken.None));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task Upload_returns_immutable_public_URL_without_credential_and_does_not_delete_previous()
    {
        using var handler = new UploadHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var storage = new SupabaseDishMediaStorage(http, Settings());
        var first = await storage.SaveAsync("dish.webp", new MemoryStream([1, 2]), null, CancellationToken.None);
        var second = await storage.SaveAsync("dish.webp", new MemoryStream([3, 4]), first, CancellationToken.None);
        Assert.NotEqual(first, second);
        Assert.StartsWith("https://test.supabase.co/storage/v1/object/public/test-media/dishes/dish-", second);
        Assert.EndsWith(".webp", second);
        Assert.DoesNotContain(Settings().ServiceKey, second);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task Provider_errors_do_not_expose_response_or_secret()
    {
        using var handler = new UploadHandler(HttpStatusCode.Forbidden);
        using var http = new HttpClient(handler);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SupabaseDishMediaStorage(http, Settings())
            .SaveAsync("dish.png", new MemoryStream([1]), null, CancellationToken.None));
        Assert.Contains("HTTP 403", ex.Message);
        Assert.DoesNotContain(Settings().ServiceKey, ex.ToString());
        Assert.DoesNotContain(Settings().ServiceKey, Settings().ToString());
    }

    [Theory]
    [InlineData("http://test.supabase.co", "media", "server-test-only")]
    [InlineData("https://evil.test", "media", "server-test-only")]
    [InlineData("https://test.supabase.co", "../bucket", "server-test-only")]
    [InlineData("https://test.supabase.co", "media", "sb_publishable_not-server")]
    public void Invalid_config_fails_closed(string url, string bucket, string key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MediaStorage:Supabase:ProjectUrl"] = url, ["MediaStorage:Supabase:Bucket"] = bucket,
            ["MediaStorage:Supabase:ServiceKey"] = key
        }).Build();
        Assert.Throws<InvalidOperationException>(() => SupabaseMediaSettings.FromConfiguration(config));
    }

    private static SupabaseMediaSettings Settings() => new(new Uri("https://test.supabase.co"), "test-media", "server-test-only");
    private sealed class UploadHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Count;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("test.supabase.co", request.RequestUri!.Host);
            Assert.Equal(Settings().ServiceKey, request.Headers.GetValues("apikey").Single());
            Assert.Contains(request.Content!.Headers.ContentType!.MediaType, new[] { "image/webp", "image/png" });
            Assert.NotEmpty(await request.Content.ReadAsByteArrayAsync(ct));
            return new HttpResponseMessage(status) { Content = new StringContent(Settings().ServiceKey) };
        }
    }
}
