using Microsoft.Extensions.Configuration;
using VietTable.CloudHost;
using Xunit;

public sealed class CloudPublicMediaTests
{
    private static IConfiguration Settings(string provider = "Supabase", string origin = "https://test.supabase.co", string bucket = "staging-media") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["MediaStorage:Provider"] = provider, ["MediaStorage:Supabase:ProjectUrl"] = origin,
            ["MediaStorage:Supabase:Bucket"] = bucket }).Build();

    [Theory]
    [InlineData("/images/dishes/food.webp")]
    [InlineData("/images/branches/branch-cover.webp")]
    [InlineData("/images/food.jpg")]
    public void PortableCatalogPathResolvesToPublicStorage(string path) =>
        Assert.Equal("https://test.supabase.co/storage/v1/object/public/staging-media/canonical-v1" + path,
            CloudPublicMedia.Resolve(Settings(), path));

    [Theory]
    [InlineData("/images/../secret.png")]
    [InlineData("/images/%2e%2e/secret.png")]
    [InlineData("/images//food.png")]
    [InlineData("/images/file.dpapi")]
    [InlineData("/images/food.png?token=value")]
    [InlineData("/api/mobile/v1/me")]
    public void OtherAndUnsafePathsAreNotRedirected(string path) =>
        Assert.Null(CloudPublicMedia.Resolve(Settings(), path));

    [Fact]
    public void LocalStorageUnchanged() => Assert.Null(CloudPublicMedia.Resolve(Settings("Local"), "/images/food.png"));

    [Theory]
    [InlineData("http://test.supabase.co", "staging-media")]
    [InlineData("https://foreign.invalid", "staging-media")]
    [InlineData("https://test.supabase.co/path", "staging-media")]
    [InlineData("https://test.supabase.co", "../other")]
    public void InvalidDestinationIsNotAnOpenRedirect(string origin, string bucket) =>
        Assert.Null(CloudPublicMedia.Resolve(Settings(origin: origin, bucket: bucket), "/images/food.png"));
}
