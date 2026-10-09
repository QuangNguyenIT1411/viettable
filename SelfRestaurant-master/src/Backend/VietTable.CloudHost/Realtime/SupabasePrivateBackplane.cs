using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using VietTable.CloudHost.Diagnostics;

namespace VietTable.CloudHost.Realtime;

// Server-to-server transport only. Clients continue authenticating at Gateway;
// no Supabase key/token is returned to browsers or Android. Private channels have
// no anon/authenticated RLS allow policy. The privileged backend bypass is explicit.
public sealed class SupabasePrivateBackplane<THub> : IDisposable where THub : Hub
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim gate = new(1);
    private readonly ILogger<SupabasePrivateBackplane<THub>> logger;
    private readonly RealtimeDiagnostics diagnostics;
    private readonly IHostApplicationLifetime application;
    private readonly string key;
    private readonly Uri? origin;
    private readonly string topic;
    private CancellationTokenSource? lifetime;
    private ClientWebSocket? socket;
    private Task? worker;
    private int connections;
    public bool Enabled { get; }

    public SupabasePrivateBackplane(IConfiguration configuration, RealtimeDiagnostics diagnostics,
        ILogger<SupabasePrivateBackplane<THub>> logger, IHostApplicationLifetime application)
    {
        this.diagnostics = diagnostics; this.logger = logger; this.application = application;
        Enabled = string.Equals(configuration["Realtime:Provider"], "SupabasePrivate", StringComparison.Ordinal);
        key = Enabled ? configuration["MediaStorage:Supabase:ServiceKey"] ?? "" : "";
        topic = "viettable-" + diagnostics.Reference("backplane:" +
            configuration["CloudHost:EnvironmentNamespace"] + ":" + typeof(THub).FullName);
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(configuration["MediaStorage:Supabase:ProjectUrl"], UriKind.Absolute, out origin)
            || origin.Scheme != "https" || !origin.Host.EndsWith(".supabase.co", StringComparison.Ordinal)
            || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/")
            throw new InvalidOperationException("Private realtime transport configuration is invalid.");
        http.BaseAddress = origin;
        http.DefaultRequestHeaders.Add("apikey", key); // Never place secret in a URL or loggable HttpClient factory.
    }

    public async Task AttachAsync(Func<PrivateGroupEvent, Task> deliver)
    {
        if (!Enabled) return;
        await gate.WaitAsync(application.ApplicationStopping);
        try
        {
            if (connections == 0)
            {
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(application.ApplicationStopping);
                try { socket = await OpenAsync(lifetime.Token); }
                catch { lifetime.Dispose(); lifetime = null; throw; }
                worker = RunAsync(socket, deliver, lifetime.Token);
            }
            connections++;
        }
        finally { gate.Release(); }
    }

    public async Task DetachAsync()
    {
        if (!Enabled) return;
        await gate.WaitAsync();
        try
        {
            if (connections == 0 || --connections != 0) return;
            lifetime!.Cancel(); socket?.Abort();
            if (worker is not null) await worker;
            lifetime.Dispose(); lifetime = null; worker = null; socket = null;
        }
        finally { gate.Release(); }
    }

    public async Task PublishAsync(PrivateGroupEvent message, CancellationToken ct)
    {
        // HTTP publishers need no persistent connection or local recipients.
        using var response = await http.PostAsJsonAsync("/realtime/v1/api/broadcast/" + topic + "/events/signalr?private=true", message, Json, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Private realtime publisher rejected HTTP " + (int)response.StatusCode);
        // Broker acceptance is NOT an acknowledgement from the customer's UI.
    }

    private async Task<ClientWebSocket> OpenAsync(CancellationToken ct)
    {
        var candidate = new ClientWebSocket();
        try
        {
            candidate.Options.SetRequestHeader("apikey", key);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var url = new UriBuilder(origin!) { Scheme = "wss", Path = "/realtime/v1/websocket", Query = "vsn=1.0.0" }.Uri;
            await candidate.ConnectAsync(url, timeout.Token);
            await SendAsync(candidate, new { topic = "realtime:" + topic, @event = "phx_join", @ref = "1", join_ref = "1",
                payload = new { config = new { @private = true, broadcast = new { self = true, ack = true }, presence = new { enabled = false } } } }, timeout.Token);
            while (true)
            {
                using var reply = await ReadAsync(candidate, timeout.Token);
                var root = reply.RootElement;
                if (root.GetProperty("event").GetString() != "phx_reply" || root.GetProperty("ref").GetString() != "1") continue;
                if (root.GetProperty("payload").GetProperty("status").GetString() != "ok")
                    throw new InvalidOperationException("Private realtime subscription rejected.");
                return candidate;
            }
        }
        catch { candidate.Dispose(); throw; }
    }

    private async Task RunAsync(ClientWebSocket initial, Func<PrivateGroupEvent, Task> deliver, CancellationToken ct)
    {
        var current = initial; var backoff = 1;
        while (!ct.IsCancellationRequested)
        {
            using var iteration = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var receive = ReceiveAsync(current, deliver, iteration.Token);
            var heartbeat = HeartbeatAsync(current, iteration.Token);
            try
            {
                var stopped = await Task.WhenAny(receive, heartbeat);
                await stopped;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception error) { Trace("bridge-disconnected", error.GetType().Name); }
            finally
            {
                iteration.Cancel(); current.Abort();
                try { await Task.WhenAll(receive, heartbeat); } catch { /* only type logged above; never dump key/URL/payload */ }
                current.Dispose();
            }
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoff), ct);
                    current = await OpenAsync(ct); socket = current; backoff = 1;
                    Trace("bridge-reconnected", "none"); break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception error) { Trace("bridge-reconnect-failed", error.GetType().Name); backoff = Math.Min(10, backoff * 2); }
            }
        }
    }

    private async Task ReceiveAsync(ClientWebSocket current, Func<PrivateGroupEvent, Task> deliver, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var message = await ReadAsync(current, ct); var root = message.RootElement;
            var kind = root.GetProperty("event").GetString();
            if (kind is "phx_error" or "phx_close") throw new InvalidOperationException("Private realtime channel closed.");
            if (kind != "broadcast" || root.GetProperty("topic").GetString() != "realtime:" + topic) continue;
            var payload = root.GetProperty("payload");
            if (payload.GetProperty("event").GetString() != "signalr") continue;
            var envelope = payload.GetProperty("payload").Deserialize<PrivateGroupEvent>(Json);
            if (envelope is null || !envelope.IsValid()) continue;
            await deliver(envelope);
        }
    }

    private static async Task HeartbeatAsync(ClientWebSocket current, CancellationToken ct)
    {
        var sequence = 1;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
            await SendAsync(current, new { topic = "phoenix", @event = "heartbeat", payload = new { }, @ref = (++sequence).ToString() }, ct);
        }
    }
    private static Task SendAsync(ClientWebSocket current, object message, CancellationToken ct) => current.SendAsync(
        new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Json)), WebSocketMessageType.Text, true, ct);
    private static async Task<JsonDocument> ReadAsync(ClientWebSocket current, CancellationToken ct)
    {
        using var stream = new MemoryStream(); var buffer = new byte[8192]; WebSocketReceiveResult result;
        do
        {
            result = await current.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType != WebSocketMessageType.Text) throw new InvalidOperationException("Private realtime transport closed.");
            stream.Write(buffer, 0, result.Count);
            if (stream.Length > 1024 * 1024) throw new InvalidOperationException("Private realtime frame exceeds limit.");
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(stream.ToArray());
    }
    private void Trace(string stage, string errorType) => logger.LogInformation(
        "RealtimeTrace stage={Stage} instance={Instance} transport=supabase-private errorType={ErrorType} utc={Utc}",
        stage, diagnostics.InstanceId, errorType, DateTimeOffset.UtcNow);
    public void Dispose() { lifetime?.Cancel(); socket?.Abort(); http.Dispose(); }
}

public sealed record PrivateGroupEvent(string EventId, string GroupReference, string Method, JsonElement[] Arguments)
{
    public bool IsValid() => Guid.TryParseExact(EventId, "N", out _) && GroupReference is { Length: 24 }
        && GroupReference.All(x => char.IsAsciiHexDigit(x))
        && Method is "orderUpdated" or "serviceRequestUpdated" or "orderReady" or "cashierDashboardChanged" or "orderReceived" or "paymentUpdated"
        && Arguments is { Length: <= 8 };
}

public sealed class DeliveryWindow
{
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    public bool First(string id)
    {
        lock (ids)
        {
            if (!ids.Add(id)) return false;
            order.Enqueue(id);
            if (order.Count > 4096) ids.Remove(order.Dequeue());
            return true;
        }
    }
}
