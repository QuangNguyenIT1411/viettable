namespace SelfRestaurant.Gateway.Api.Infrastructure.Media;

public sealed class LocalDishMediaStorage(IWebHostEnvironment environment) : IDishMediaStorage
{
    public async Task<string> SaveAsync(string fileName, Stream content, string? previousPublicPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new InvalidOperationException("Invalid media storage key.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        foreach (var root in Roots())
        {
            Directory.CreateDirectory(root);
            var target = Path.GetFullPath(Path.Combine(root, fileName));
            // Stage beside the destination, including same-key replacements.
            // An interrupted write must not truncate the current public image.
            var staging = target + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllBytesAsync(staging, bytes, ct);
                ct.ThrowIfCancellationRequested();
                File.Move(staging, target, overwrite: true);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
            if (previousPublicPath?.Replace('\\', '/').StartsWith("/images/dishes/", StringComparison.OrdinalIgnoreCase) == true)
            {
                var previous = Path.Combine(root, Path.GetFileName(previousPublicPath.Replace('\\', '/')));
                if (!string.Equals(previous, target, StringComparison.OrdinalIgnoreCase) && File.Exists(previous))
                    File.Delete(previous);
            }
            var slug = Path.GetFileNameWithoutExtension(fileName);
            foreach (var sibling in Directory.GetFiles(root, slug + ".*", SearchOption.TopDirectoryOnly))
                if (!string.Equals(sibling, target, StringComparison.OrdinalIgnoreCase)) File.Delete(sibling);
        }
        return "/images/dishes/" + fileName;
    }

    private IReadOnlyList<string> Roots()
    {
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var direct = Path.Combine(contentRoot, "wwwroot", "images", "dishes");
        var source = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "wwwroot", "images", "dishes"));
        var roots = new[] { direct, source }.Where(candidate =>
            Directory.Exists(Directory.GetParent(candidate)?.Parent?.FullName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return roots.Length == 0 ? new[] { direct } : roots;
    }
}
