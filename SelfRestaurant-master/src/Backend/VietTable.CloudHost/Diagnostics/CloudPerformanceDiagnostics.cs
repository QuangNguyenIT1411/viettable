using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VietTable.CloudHost.Diagnostics;

// Preview-only metadata. Never inspect commands, parameters, connection strings,
// claims, headers or payloads. Connection duration includes pool/network/TLS;
// it must not be reported as an isolated pool-wait or PostgreSQL engine duration.
public sealed class CloudPerformanceDiagnostics : IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private static readonly AsyncLocal<Measurement?> Current = new();
    private readonly List<IDisposable> subscriptions = [];
    private readonly IDisposable listener;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> startupPhases = new();
    public void RecordStartupPhase(string name, double milliseconds) => startupPhases[name] = milliseconds;
    public string StartupServerTiming => string.Join(", ", startupPhases.OrderBy(x => x.Key)
        .Select(x => FormattableString.Invariant($"{x.Key};dur={x.Value:F1}")));
    public CloudPerformanceDiagnostics() => listener = DiagnosticListener.AllListeners.Subscribe(this);
    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "VietTable.Startup")
        {
            lock (subscriptions) subscriptions.Add(value.Subscribe(this));
            return;
        }
        if (value.Name != "Microsoft.EntityFrameworkCore") return;
        lock (subscriptions) subscriptions.Add(value.Subscribe(this, name =>
            name.EndsWith(".CommandExecuted", StringComparison.Ordinal)
            || name.EndsWith(".ConnectionOpened", StringComparison.Ordinal)
            || name.EndsWith(".ContextInitialized", StringComparison.Ordinal)));
    }
    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == "Completed" && value.Value is KeyValuePair<string, double> phase)
        {
            startupPhases.AddOrUpdate(phase.Key, phase.Value, (_, previous) => previous + phase.Value);
            return;
        }
        var measurement = Current.Value;
        if (measurement is null) return;
        if (value.Value is CommandExecutedEventData command)
            measurement.RecordQuery(command.Duration);
        else if (value.Value is ConnectionEndEventData connection
            && value.Key.EndsWith(".ConnectionOpened", StringComparison.Ordinal))
            measurement.RecordConnection(connection.Duration);
        else if (value.Key.EndsWith(".ContextInitialized", StringComparison.Ordinal)
            && value.Value is DbContextEventData context)
            measurement.RecordContext(context.Context?.Database.ProviderName);
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void Dispose()
    {
        listener.Dispose();
        lock (subscriptions) foreach (var subscription in subscriptions) subscription.Dispose();
    }
    public static Measurement Begin(string owner, string phase) => new(owner, phase);
    public static bool HasScope => Current.Value is not null;
    public sealed class Measurement : IDisposable
    {
        private readonly Measurement? parent;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly string owner, phase;
        private long queryTicks, connectionTicks;
        private int queries, connections, contexts;
        private string provider = "unobserved";
        private int disposed;
        internal Measurement(string owner, string phase)
        {
            // The caller supplies fixed module/phase names, not user identifiers.
            this.owner = owner; this.phase = phase;
            parent = Current.Value; Current.Value = this;
        }
        public void RecordQuery(TimeSpan duration)
        { Interlocked.Increment(ref queries); Interlocked.Add(ref queryTicks, duration.Ticks); }
        public void RecordConnection(TimeSpan duration)
        { Interlocked.Increment(ref connections); Interlocked.Add(ref connectionTicks, duration.Ticks); }
        public void RecordContext(string? name)
        {
            Interlocked.Increment(ref contexts);
            provider = name == "Npgsql.EntityFrameworkCore.PostgreSQL" ? "Postgres"
                : name == "Microsoft.EntityFrameworkCore.SqlServer" ? "SqlServer" : "other";
        }
        public string Summary => FormattableString.Invariant(
            $"CloudPerformance owner={owner} phase={phase} elapsedMs={clock.ElapsedMilliseconds} queryCount={queries} queryMs={TimeSpan.FromTicks(queryTicks).TotalMilliseconds:F1} connectionCount={connections} connectionMs={TimeSpan.FromTicks(connectionTicks).TotalMilliseconds:F1} contextCount={contexts} provider={provider}");
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            clock.Stop(); Current.Value = parent;
            Console.WriteLine(Summary);
        }
    }
}
