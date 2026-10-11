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
    internal static Measurement? CurrentMeasurement => Current.Value;
    internal static Measurement BeginLinked(Measurement parent) => new("Module", "dispatch", parent);
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
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> phases = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ReadTotals> reads = new();
        private sealed record ReadTotals(long QueryTicks, long ConnectionTicks, int Queries);
        internal Measurement(string owner, string phase, Measurement? linkedParent = null)
        {
            // The caller supplies fixed module/phase names, not user identifiers.
            this.owner = owner; this.phase = phase;
            parent = linkedParent ?? Current.Value; Current.Value = this;
        }
        public void RecordQuery(TimeSpan duration)
        { Interlocked.Increment(ref queries); Interlocked.Add(ref queryTicks, duration.Ticks); parent?.RecordQuery(duration); }
        public void RecordConnection(TimeSpan duration)
        { Interlocked.Increment(ref connections); Interlocked.Add(ref connectionTicks, duration.Ticks); parent?.RecordConnection(duration); }
        public void RecordContext(string? name)
        {
            Interlocked.Increment(ref contexts);
            provider = name == "Npgsql.EntityFrameworkCore.PostgreSQL" ? "Postgres"
                : name == "Microsoft.EntityFrameworkCore.SqlServer" ? "SqlServer" : "other";
            parent?.RecordContext(name);
        }
        private void RecordPhase(string name, long ticks)
        {
            // Fixed code-owned names only. Nested/parallel durations are sums,
            // not additive wall-clock partitions.
            if (name is "staff-auth" or "cookie-authenticate" or "live-staff-validation" or "action" or "result" || IsRead(name))
                phases.AddOrUpdate(name, ticks, (_, previous) => previous + ticks);
            parent?.RecordPhase(name, ticks);
        }
        private static bool IsRead(string name) => name is "read-chef-orders" or "read-chef-history"
            or "read-orders-cashier" or "read-billing-cashier" or "read-cashier-report" or "read-invoices";
        private void RecordRead(string name, ReadTotals value)
        {
            reads.AddOrUpdate(name, value, (_, previous) => new(previous.QueryTicks + value.QueryTicks,
                previous.ConnectionTicks + value.ConnectionTicks, previous.Queries + value.Queries));
            parent?.RecordRead(name, value);
        }
        public string ServerTiming => FormattableString.Invariant(
            $"app-preheaders;dur={clock.Elapsed.TotalMilliseconds:F1}, sql-sum;dur={TimeSpan.FromTicks(Interlocked.Read(ref queryTicks)).TotalMilliseconds:F1}, connect-sum;dur={TimeSpan.FromTicks(Interlocked.Read(ref connectionTicks)).TotalMilliseconds:F1}, sql-count;desc=\"{Volatile.Read(ref queries)}\", context-count;desc=\"{Volatile.Read(ref contexts)}\"")
            + string.Concat(phases.OrderBy(x => x.Key).Select(x => FormattableString.Invariant($", {x.Key}-sum;dur={TimeSpan.FromTicks(x.Value).TotalMilliseconds:F1}")))
            + string.Concat(reads.OrderBy(x => x.Key).Select(x => FormattableString.Invariant(
                $", {x.Key}-sql;dur={TimeSpan.FromTicks(x.Value.QueryTicks).TotalMilliseconds:F1}, {x.Key}-connect;dur={TimeSpan.FromTicks(x.Value.ConnectionTicks).TotalMilliseconds:F1}, {x.Key}-count;desc=\"{x.Value.Queries}\"")));
        public string Summary => FormattableString.Invariant(
            $"CloudPerformance owner={owner} phase={phase} elapsedMs={clock.ElapsedMilliseconds} queryCount={queries} queryMs={TimeSpan.FromTicks(queryTicks).TotalMilliseconds:F1} connectionCount={connections} connectionMs={TimeSpan.FromTicks(connectionTicks).TotalMilliseconds:F1} contextCount={contexts} provider={provider}");
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            clock.Stop(); Current.Value = parent;
            parent?.RecordPhase(phase, clock.Elapsed.Ticks);
            if (IsRead(phase)) parent?.RecordRead(phase, new(Interlocked.Read(ref queryTicks),
                Interlocked.Read(ref connectionTicks), Volatile.Read(ref queries)));
            Console.WriteLine(Summary);
        }
    }
}
