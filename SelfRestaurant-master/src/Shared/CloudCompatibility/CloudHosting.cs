using System.Globalization;

namespace SelfRestaurant.CloudCompatibility;

internal static class CloudHosting
{
    public static void Configure(WebApplicationBuilder builder)
    {
        var portText = builder.Configuration["PORT"];
        if (string.IsNullOrWhiteSpace(portText)) return; // Existing local ASPNETCORE_URLS wins.
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be a valid TCP port.");
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }
}
