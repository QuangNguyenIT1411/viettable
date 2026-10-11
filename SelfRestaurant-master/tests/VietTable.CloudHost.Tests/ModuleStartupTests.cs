using VietTable.CloudHost.Composition;
using VietTable.CloudHost.Diagnostics;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace VietTable.CloudHost.Tests;

public sealed class ModuleStartupTests
{
    [Theory]
    [InlineData("production", "Postgres", "None", "false", false)]
    [InlineData("preview", "Postgres", "None", "false", true)]
    [InlineData("preview", "Postgres", "None", "true", false)]
    [InlineData("preview", "Postgres", "Demo", "false", false)]
    [InlineData("preview", "SqlServer", "None", "false", false)]
    public void MigrationSeedAndProductionCompositionRemainSequential(string env, string provider, string seed, string migrate, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["VERCEL_ENV"] = env, ["Database:Provider"] = provider,
            ["Database:SeedProfile"] = seed, ["Database:ApplyMigrations"] = migrate }).Build();
        Assert.Equal(expected, ModuleStartup.CanInitializeInParallel(configuration));
    }
    [Fact]
    public async Task ParallelStartupStartsEveryOwnerBeforeWaitingAndKeepsOwnerOrder()
    {
        var names = new[] { "Catalog", "Orders", "Customers", "Identity", "Billing" };
        var gates = names.ToDictionary(name => name, _ => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
        var started = new List<string>();
        var pending = ModuleStartup.InitializeAsync(names, name => { started.Add(name); return gates[name].Task; }, true);
        Assert.Equal(names, started);
        Assert.False(pending.IsCompleted);
        foreach (var name in names.Reverse()) gates[name].SetResult(name);
        Assert.Equal(names, await pending);
    }
    [Fact]
    public async Task SequentialBootstrapDoesNotStartNextOwnerEarly()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<string>();
        var pending = ModuleStartup.InitializeAsync(new[] { "Catalog", "Orders" }, name =>
        { started.Add(name); return name == "Catalog" ? gate.Task : Task.FromResult(name); }, false);
        Assert.Equal(new[] { "Catalog" }, started);
        gate.SetResult("Catalog");
        Assert.Equal(new[] { "Catalog", "Orders" }, await pending);
    }
    [Fact]
    public async Task ParallelStartupCannotSucceedWhenAnyRequiredOwnerFails()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startup = ModuleStartup.InitializeAsync(new[] { "Catalog", "Orders" },
            name => name == "Catalog" ? gate.Task : Task.FromException<string>(new InvalidOperationException("required schema unavailable")), true);
        Assert.False(startup.IsCompleted);
        gate.SetResult("Catalog");
        await Assert.ThrowsAsync<InvalidOperationException>(() => startup);
    }
    [Fact]
    public void DiagnosticSummaryContainsOnlyTimingAndProviderMetadata()
    {
        using var measurement = CloudPerformanceDiagnostics.Begin("Orders", "startup");
        measurement.RecordQuery(TimeSpan.FromMilliseconds(12));
        measurement.RecordConnection(TimeSpan.FromMilliseconds(4));
        measurement.RecordContext("Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.Contains("queryCount=1 queryMs=12.0", measurement.Summary);
        Assert.Contains("connectionCount=1 connectionMs=4.0", measurement.Summary);
        Assert.Contains("contextCount=1 provider=Postgres", measurement.Summary);
        Assert.DoesNotContain("SELECT", measurement.Summary);
    }
}
