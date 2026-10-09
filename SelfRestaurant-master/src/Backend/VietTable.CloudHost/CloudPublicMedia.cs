using System.Text.RegularExpressions;

namespace VietTable.CloudHost;

// Staging Catalog retains portable /images paths. Cloud containers do not own
// these files; the same reviewed assets are published to public Storage.
public static class CloudPublicMedia
{
    public static string? Resolve(IConfiguration configuration, string path)
    {
        if (!string.Equals(configuration["MediaStorage:Provider"], "Supabase", StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(path, "^/images/(?:[a-zA-Z0-9_-]+/)*[a-zA-Z0-9_-]+\\.(?:jpg|jpeg|png|webp|svg)$",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))) return null;
        if (!Uri.TryCreate(configuration["MediaStorage:Supabase:ProjectUrl"], UriKind.Absolute, out var origin)
            || origin.Scheme != "https" || !origin.IsDefaultPort || origin.AbsolutePath != "/"
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0
            || !origin.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase)) return null;
        var bucket = configuration["MediaStorage:Supabase:Bucket"] ?? "";
        if (!Regex.IsMatch(bucket, "^[a-z0-9][a-z0-9-]{0,62}$")) return null;
        return new Uri(origin, "storage/v1/object/public/" + bucket + "/canonical-v1" + path).AbsoluteUri;
    }
}
