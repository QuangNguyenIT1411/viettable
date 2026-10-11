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

    [Fact]
    public void Nested_metrics_are_counted_once_in_parent_and_remain_payload_free()
    {
        using var parent = CloudPerformanceDiagnostics.Begin("Gateway", "request");
        using (var child = CloudPerformanceDiagnostics.Begin("Identity", "staff-auth"))
        {
            child.RecordQuery(TimeSpan.FromMilliseconds(17));
            child.RecordConnection(TimeSpan.FromMilliseconds(5));
            child.RecordContext("Npgsql.EntityFrameworkCore.PostgreSQL");
        }
        Assert.Contains("sql-sum;dur=17.0", parent.ServerTiming);
        Assert.Contains("connect-sum;dur=5.0", parent.ServerTiming);
        Assert.Contains("sql-count;desc=\"1\"", parent.ServerTiming);
        Assert.Contains("staff-auth-sum;dur=", parent.ServerTiming);
        Assert.DoesNotContain("Npgsql", parent.ServerTiming);
    }

    [Fact]
    public async Task Parallel_scopes_aggregate_without_sharing_request_context()
    {
        using var parent = CloudPerformanceDiagnostics.Begin("Gateway", "request");
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => {
            using var child = CloudPerformanceDiagnostics.Begin("Orders", "action");
            await Task.Yield();
            child.RecordQuery(TimeSpan.FromMilliseconds(3));
        }));
        Assert.Contains("sql-sum;dur=12.0", parent.ServerTiming);
        Assert.Contains("sql-count;desc=\"4\"", parent.ServerTiming);
        using var independent = CloudPerformanceDiagnostics.Begin("Gateway", "independent");
        Assert.Contains("sql-count;desc=\"0\"", independent.ServerTiming);
    }
}
