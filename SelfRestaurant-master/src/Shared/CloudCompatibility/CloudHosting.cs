using System.Globalization;

namespace SelfRestaurant.CloudCompatibility;

internal static class CloudHosting
{
    // Preview instrumentation only; phase names are compile-time constants.
    // Registration timings do not imply a database connection was opened.
    public static IDisposable? Measure(string owner, string phase) =>
        Environment.GetEnvironmentVariable("VERCEL_ENV") == "preview" ? new StartupPhase(owner, phase) : null;

    private sealed class StartupPhase(string owner, string phase) : IDisposable
    {
        private static readonly System.Diagnostics.DiagnosticListener Events = new("VietTable.Startup");
        private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Console.WriteLine(FormattableString.Invariant($"CloudStartup owner={owner} phase={phase} elapsedMs={elapsed:F1}"));
            if (Events.IsEnabled("Completed")) Events.Write("Completed", new KeyValuePair<string, double>(owner + "-" + phase, elapsed));
        }
    }
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
