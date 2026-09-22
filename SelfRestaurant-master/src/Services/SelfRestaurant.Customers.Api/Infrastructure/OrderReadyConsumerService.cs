using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

public sealed class OrderReadyConsumerService : BackgroundService
{
    private const string ConsumerName = "Customers.Api/order-ready-consumer";
    private const int MaxRetries = 3;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderReadyConsumerService> _logger;
    private readonly IConfiguration _configuration;

    public OrderReadyConsumerService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderReadyConsumerService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
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

    private async Task ConsumeBatchAsync(int take, CancellationToken cancellationToken)
    {
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

            if (existingInbox is not null && string.Equals(existingInbox.Status, "DEAD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existingInbox is not null &&
                string.Equals(existingInbox.Status, "RETRY", StringComparison.OrdinalIgnoreCase) &&
                existingInbox.NextRetryAtUtc is DateTime nextRetryAtUtc &&
                nextRetryAtUtc > DateTime.UtcNow)
            {
                continue;
            }

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
                        Message = $"{dishName} đã sẵn sàng. Vui lòng đến nhận món cho đơn {payload.OrderCode}.",
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
                await ordersEvents.AckAsync(item.OutboxEventId, ConsumerName, cancellationToken);
            }
            catch (Exception ex)
            {
                var inbox = existingInbox ?? new InboxEvents
                {
                    Source = item.Source,
                    SourceEventId = item.OutboxEventId,
                    EventName = item.EventName,
                    CorrelationId = item.CorrelationId,
                    ReceivedAtUtc = DateTime.UtcNow
                };
                inbox.PayloadJson = item.Payload.GetRawText();
                var retryCount = (existingInbox?.RetryCount ?? 0) + 1;
                inbox.RetryCount = retryCount;
                inbox.Status = retryCount >= MaxRetries ? "DEAD" : "RETRY";
                inbox.NextRetryAtUtc = retryCount >= MaxRetries ? null : DateTime.UtcNow.AddSeconds(Math.Min(15 * retryCount, 60));
                inbox.ProcessedAtUtc = DateTime.UtcNow;
                inbox.Error = ex.ToString();
                if (existingInbox is null)
                {
                    db.InboxEvents.Add(inbox);
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Failed to process order.status-ready event {OutboxEventId}", item.OutboxEventId);
            }
        }
    }

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
