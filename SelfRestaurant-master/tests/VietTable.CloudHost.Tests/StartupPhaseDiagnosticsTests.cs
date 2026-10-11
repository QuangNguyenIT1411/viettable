using System.Diagnostics;
using VietTable.CloudHost.Diagnostics;
using Xunit;

public sealed class StartupPhaseDiagnosticsTests
{
    [Fact]
    public void Phase_metadata_can_be_read_without_request_or_database_payload()
    {
        using var diagnostics = new CloudPerformanceDiagnostics();
        using var events = new DiagnosticListener("VietTable.Startup");
        events.Write("Completed", new KeyValuePair<string, double>("Catalog-create-builder", 12.5));
        events.Write("Completed", new KeyValuePair<string, double>("Postgres-pending-migrations-io", 31));
        Assert.Contains("Catalog-create-builder;dur=12.5", diagnostics.StartupServerTiming);
        Assert.Contains("Postgres-pending-migrations-io;dur=31.0", diagnostics.StartupServerTiming);
        // Unrelated event data is not projected to public timing metadata.
        events.Write("Completed", new { Password = "test-not-a-real-secret" });
        Assert.DoesNotContain("Password", diagnostics.StartupServerTiming);
        Assert.DoesNotContain("test-not-a-real-secret", diagnostics.StartupServerTiming);
    }
}
