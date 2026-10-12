using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

public sealed class OrderReadyConsumerService : BackgroundService
{
    private const string ConsumerName = "Customers.Api/order-ready-consumer";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderReadyConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public OrderReadyConsumerService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderReadyConsumerService> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!(_configuration.GetValue<bool?>("IntegrationEvents:Consumers:OrderReady:Enabled") ?? true))
        {
            _logger.LogInformation("OrderReady consumer is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_configuration.GetValue<int?>("IntegrationEvents:Consumers:OrderReady:PollIntervalSeconds") ?? 2, 2, 60));
        var take = Math.Clamp(_configuration.GetValue<int?>("IntegrationEvents:Consumers:OrderReady:Take") ?? 20, 1, 100);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeBatchAsync(take, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OrderReady consumer iteration failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    // CloudHost calls the same one-shot processor; the durable source outbox remains pending
    // until the Customers inbox has committed and the source ACK succeeds.
    public async Task ConsumeBatchAsync(int take, CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment() && _configuration.GetValue<bool>("Recovery:TestFailReadyProjection"))
            throw new InvalidOperationException("Injected failure before READY projection.");
        using var scope = _scopeFactory.CreateScope();
        var ordersEvents = scope.ServiceProvider.GetRequiredService<OrdersEventsClient>();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();

        var events = await ordersEvents.GetPendingReadyEventsAsync(take, cancellationToken);
        if (events.Count == 0)
        {
            return;
        }

        foreach (var item in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingInbox = await db.InboxEvents.FirstOrDefaultAsync(
                x => x.Source == item.Source && x.SourceEventId == item.OutboxEventId,
                cancellationToken);

            if (existingInbox is not null && string.Equals(existingInbox.Status, "PROCESSED", StringComparison.OrdinalIgnoreCase))
            {
                await ordersEvents.AckAsync(item.OutboxEventId, ConsumerName, cancellationToken);
                continue;
            }

            if (existingInbox is not null &&
                string.Equals(existingInbox.Status, "RETRY", StringComparison.OrdinalIgnoreCase) &&
                existingInbox.NextRetryAtUtc is DateTime nextRetryAtUtc &&
                nextRetryAtUtc > DateTime.UtcNow)
            {
                continue;
            }

            var effectCommitted = false;
            try
            {
                var payload = item.Payload.Deserialize<OrderReadyPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (payload is null)
                {
                    throw new InvalidOperationException($"Unable to deserialize payload for outbox event {item.OutboxEventId}.");
                }

                var alreadyProjected = await db.ReadyDishNotifications.AnyAsync(
                    x => x.OrderId == payload.OrderId
                        && x.OrderItemId == payload.OrderItemId
                        && x.EventName == item.EventName,
                    cancellationToken);

                if (!alreadyProjected)
                {
                    var dishName = string.IsNullOrWhiteSpace(payload.DishName)
                        ? (payload.DishId is int dishId ? $"Món #{dishId}" : "Món ăn")
                        : payload.DishName.Trim();
                    db.ReadyDishNotifications.Add(new ReadyDishNotifications
                    {
                        OrderId = payload.OrderId,
                        OrderItemId = payload.OrderItemId,
                        DishId = payload.DishId,
                        DishName = dishName,
                        CustomerId = payload.CustomerId,
                        TableId = payload.TableId,
                        EventName = item.EventName,
                        Message = ReadyMessage(dishName, payload.OrderId),
                        Status = "OPEN",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                var inbox = existingInbox ?? new InboxEvents
                {
                    Source = item.Source,
                    SourceEventId = item.OutboxEventId,
                    EventName = item.EventName,
                    CorrelationId = item.CorrelationId,
                    ReceivedAtUtc = DateTime.UtcNow
                };
                inbox.PayloadJson = item.Payload.GetRawText();
                inbox.Status = "PROCESSED";
                inbox.RetryCount = existingInbox?.RetryCount ?? 0;
                inbox.NextRetryAtUtc = null;
                inbox.ProcessedAtUtc = DateTime.UtcNow;
                inbox.Error = null;
                if (existingInbox is null)
                {
                    db.InboxEvents.Add(inbox);
                }

                await db.SaveChangesAsync(cancellationToken);
                effectCommitted = true;
                await ordersEvents.AckAsync(item.OutboxEventId, ConsumerName, cancellationToken);
            }
            catch (Exception ex)
            {
                if (effectCommitted)
                {
                    _logger.LogWarning(ex, "READY projection committed; source ACK deferred for event {OutboxEventId}", item.OutboxEventId);
                    continue;
                }
                db.ChangeTracker.Clear();
                var inbox = existingInbox ?? new InboxEvents
                {
                    Source = item.Source,
                    SourceEventId = item.OutboxEventId,
                    EventName = item.EventName,
                    CorrelationId = item.CorrelationId,
                    ReceivedAtUtc = DateTime.UtcNow
                };
                inbox.PayloadJson = item.Payload.GetRawText();
                var retryCount = Math.Min((existingInbox?.RetryCount ?? 0) + 1, 1000000);
                inbox.RetryCount = retryCount;
                inbox.Status = "RETRY";
                inbox.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(15 * retryCount, 300));
                inbox.ProcessedAtUtc = DateTime.UtcNow;
                inbox.Error = ex.ToString();
                if (existingInbox is null)
                {
                    db.InboxEvents.Add(inbox);
                }
                else db.InboxEvents.Update(inbox);

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Failed to process order.status-ready event {OutboxEventId}", item.OutboxEventId);
            }
        }
    }

    // Customers see the short order number, not the internal order code.
    public static string ReadyMessage(string dishName, int orderId) =>
        $"{dishName} đã sẵn sàng. Vui lòng đến nhận món cho đơn #{orderId}.";

    private sealed record OrderReadyPayload(
        int OrderId,
        string OrderCode,
        int? TableId,
        int? CustomerId,
        string StatusCode,
        int? OrderItemId,
        int? DishId,
        string? DishName,
        int? Quantity,
        string? ItemStatusCode);
}
