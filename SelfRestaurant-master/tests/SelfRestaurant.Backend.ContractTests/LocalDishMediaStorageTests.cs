using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SelfRestaurant.Gateway.Api.Infrastructure.Media;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class LocalDishMediaStorageTests
{
    private sealed class Host(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MediaTest";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("sub/file.png")]
    [InlineData("sub\\file.png")]
    public async Task Rejects_path_traversal(string key)
    {
        var storage = new LocalDishMediaStorage(new Host(Path.GetTempPath()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SaveAsync(key, new MemoryStream(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_same_key_replacement_keeps_existing_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "viettable-media-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalDishMediaStorage(new Host(root));
            var path = await storage.SaveAsync("dish.png", new MemoryStream(new byte[] { 1, 2 }), null, CancellationToken.None);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync("dish.png",
                new MemoryStream(new byte[] { 3, 4 }), path, new CancellationToken(canceled: true)));
            var imageRoot = Path.Combine(root, "wwwroot", "images", "dishes");
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(Path.Combine(imageRoot, "dish.png")));
            Assert.Single(Directory.GetFiles(imageRoot));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Local_replacement_retains_public_path_and_replaces_sibling_extension()
    {
        var root = Path.Combine(Path.GetTempPath(), "viettable-media-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalDishMediaStorage(new Host(root));
            var previous = await storage.SaveAsync("dish.png", new MemoryStream(new byte[] { 1, 2 }), null, CancellationToken.None);
            var next = await storage.SaveAsync("dish.webp", new MemoryStream(new byte[] { 3, 4 }), previous, CancellationToken.None);
            Assert.Equal("/images/dishes/dish.webp", next);
            var imageRoot = Path.Combine(root, "wwwroot", "images", "dishes");
            Assert.False(File.Exists(Path.Combine(imageRoot, "dish.png")));
            Assert.Equal(new byte[] { 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(imageRoot, "dish.webp")));
        }
        finally { Directory.Delete(root, recursive: true); } // Only the unique directory created by this test.
    }
}
