using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using VietTable.CloudHost.Realtime;

namespace VietTable.CloudHost.Diagnostics;

// Observability only: no payloads, identity claims, credentials or raw group names.
// A process-local recipient count is NOT an acknowledgement of client delivery.
public sealed class RealtimeDiagnostics(IConfiguration configuration)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(configuration["MobileJwt:SigningKey"]
        ?? throw new InvalidOperationException("Realtime diagnostics requires the configured signing key."));
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public string Reference(string value) => Convert.ToHexString(
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("diagnostics:" + value)))[..24];
    public static string GroupType(string group) => group.StartsWith("mobile-customer:", StringComparison.Ordinal)
        ? "customer" : group.StartsWith("branch:", StringComparison.Ordinal) ? "branch"
        : group.StartsWith("table:", StringComparison.Ordinal) ? "table" : "other";
    public static string EventType(string method) => method is "orderUpdated" or "serviceRequestUpdated"
        or "orderReady" or "kitchenUpdated" ? method : "other";
}

public sealed class DiagnosticHubLifetimeManager<THub>(
    ILogger<DefaultHubLifetimeManager<THub>> baseLogger,
    ILogger<DiagnosticHubLifetimeManager<THub>> logger,
    RealtimeDiagnostics diagnostics,
    IHttpContextAccessor accessor,
    SupabasePrivateBackplane<THub> backplane) : DefaultHubLifetimeManager<THub>(baseLogger) where THub : Hub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> groups = new();
    private readonly ConcurrentDictionary<string, string> references = new();
    private readonly ConcurrentDictionary<string, byte> attached = new();
    private readonly DeliveryWindow deliveries = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override async Task OnConnectedAsync(HubConnectionContext connection)
    {
        await base.OnConnectedAsync(connection);
        await backplane.AttachAsync(DeliverAsync);
        attached[connection.ConnectionId] = 0;
    }

    private async Task DeliverAsync(PrivateGroupEvent message)
    {
        if (!references.TryGetValue(message.GroupReference, out var group) || !deliveries.First(message.EventId)) return;
        Record("bridge-received", group, RealtimeDiagnostics.EventType(message.Method), message.EventId);
        await base.SendGroupAsync(group, message.Method, message.Arguments.Cast<object?>().ToArray());
        Record("bridge-dispatched-local", group, RealtimeDiagnostics.EventType(message.Method), message.EventId);
    }

    public override async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        await base.AddToGroupAsync(connectionId, groupName, ct);
        references[diagnostics.Reference(groupName)] = groupName;
        groups.GetOrAdd(groupName, _ => new())[connectionId] = 0;
        Record("subscription-accepted", groupName, "subscription", Guid.NewGuid().ToString("N"));
    }

    public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        await base.RemoveFromGroupAsync(connectionId, groupName, ct);
        if (groups.TryGetValue(groupName, out var members)) members.TryRemove(connectionId, out _);
    }

    public override async Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        foreach (var members in groups.Values) members.TryRemove(connection.ConnectionId, out _);
        await base.OnDisconnectedAsync(connection);
        if (attached.TryRemove(connection.ConnectionId, out _)) await backplane.DetachAsync();
    }

    public override async Task SendGroupAsync(string groupName, string methodName, object?[] args, CancellationToken ct = default)
    {
        var eventId = Guid.NewGuid().ToString("N");
        Record("publish-attempted", groupName, RealtimeDiagnostics.EventType(methodName), eventId);
        try
        {
            if (backplane.Enabled)
            {
                var message = new PrivateGroupEvent(eventId, diagnostics.Reference(groupName), methodName,
                    args.Select(x => JsonSerializer.SerializeToElement(x, Json)).ToArray());
                if (!message.IsValid()) throw new InvalidOperationException("Unsupported private realtime event.");
                await backplane.PublishAsync(message, ct);
                Record("publish-accepted-broker", groupName, RealtimeDiagnostics.EventType(methodName), eventId);
            }
            else
            {
                await base.SendGroupAsync(groupName, methodName, args, ct);
                Record("publish-completed-local", groupName, RealtimeDiagnostics.EventType(methodName), eventId);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested && backplane.Enabled)
        {
            // Business writes already committed. A transport outage must not turn
            // a valid order/payment into an API failure. REST remains canonical;
            // disconnected clients resync on reconnect. This is NOT cloud delivery PASS.
            Record("publish-failed", groupName, RealtimeDiagnostics.EventType(methodName), eventId);
            await base.SendGroupAsync(groupName, methodName, args, ct);
        }
        catch
        {
            Record("publish-failed", groupName, RealtimeDiagnostics.EventType(methodName), eventId);
            throw;
        }
    }

    private void Record(string stage, string group, string eventType, string eventId)
    {
        var context = accessor.HttpContext;
        logger.LogInformation("RealtimeTrace stage={Stage} instance={Instance} request={Request} correlationRef={CorrelationRef} event={Event} eventId={EventId} groupType={GroupType} groupRef={GroupRef} localRecipients={Recipients} utc={Utc}",
            stage, diagnostics.InstanceId, context?.TraceIdentifier ?? "none",
            diagnostics.Reference(context?.Request.Headers["X-Correlation-Id"].ToString() ?? "none"),
            eventType, eventId, RealtimeDiagnostics.GroupType(group), diagnostics.Reference(group),
            groups.TryGetValue(group, out var members) ? members.Count : 0, DateTimeOffset.UtcNow);
    }
}

public sealed class RealtimeSubscriptionFilter(RealtimeDiagnostics diagnostics, ILogger<RealtimeSubscriptionFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try { return await next(context); }
        catch (HubException)
        {
            // Branch rejection is logged without raw method arguments, user or branch ID.
            logger.LogInformation("RealtimeTrace stage=subscription-rejected instance={Instance} request={Request} groupType=branch utc={Utc}",
                diagnostics.InstanceId, context.Context.GetHttpContext()?.TraceIdentifier ?? "none", DateTimeOffset.UtcNow);
            throw;
        }
    }
}
