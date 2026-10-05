using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Eventing;

public sealed record IntegrationEventEnvelope(
    string EventName,
    DateTime OccurredAtUtc,
    string Source,
    string? CorrelationId,
    object Payload);

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default);
}

public sealed class FileIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<FileIntegrationEventPublisher> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly BillingDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public FileIntegrationEventPublisher(
        ILogger<FileIntegrationEventPublisher> logger,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        BillingDbContext db)
    {
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
        _db = db;
    }

    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!(_configuration.GetValue<bool?>("IntegrationEvents:Enabled") ?? true))
            {
                return;
            }

            var payloadJson = JsonSerializer.Serialize(envelope.Payload, JsonOptions);

            if (ShouldTrackInOutbox(envelope.EventName))
            {
                _db.OutboxEvents.Add(new OutboxEvents
                {
                    EventName = envelope.EventName,
                    OccurredAtUtc = envelope.OccurredAtUtc,
                    Source = envelope.Source,
                    CorrelationId = envelope.CorrelationId,
                    PayloadJson = payloadJson,
                    Status = "PENDING",
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            // Durable business delivery remains in the database outbox above.
            // JSONL is an optional local diagnostic copy, never a delivery dependency.
            if (_configuration.GetValue<bool?>("IntegrationEvents:WriteDiagnosticFiles") ?? _environment.IsDevelopment())
            {
                try
                {
                    var baseDir = _configuration["IntegrationEvents:Directory"];
                    if (string.IsNullOrWhiteSpace(baseDir))
                        baseDir = Path.Combine(_environment.ContentRootPath, "App_Data", "integration-events");
                    else if (!Path.IsPathRooted(baseDir))
                        baseDir = Path.Combine(_environment.ContentRootPath, baseDir);

                    Directory.CreateDirectory(baseDir);
                    var filePath = Path.Combine(baseDir, $"billing-events-{DateTime.UtcNow:yyyyMMdd}.jsonl");
                    await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning("Diagnostic event file unavailable ({ErrorType}); database outbox retained.", ex.GetType().Name);
                }
            }

            TryPublishToRabbitMq(envelope, json);

            _logger.LogInformation("Integration event {EventName} recorded; source {Source}, correlation {CorrelationId}", envelope.EventName, envelope.Source, envelope.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist integration event {EventName}", envelope.EventName);
        }
    }

    private bool ShouldTrackInOutbox(string eventName)
    {
        var configuredEvents = _configuration
            .GetSection("IntegrationEvents:OutboxTrackedEvents")
            .Get<string[]>();

        if (configuredEvents is null || configuredEvents.Length == 0)
        {
            return true;
        }

        return configuredEvents.Any(x => string.Equals(x, eventName, StringComparison.OrdinalIgnoreCase));
    }

    private void TryPublishToRabbitMq(IntegrationEventEnvelope envelope, string envelopeJson)
    {
        if (!(_configuration.GetValue<bool?>("RabbitMq:Enabled") ?? false))
        {
            return;
        }

        try
        {
            var factory = SelfRestaurant.CloudCompatibility.RabbitConnectionConfiguration.Create(_configuration);

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var exchange = _configuration["RabbitMq:Exchange"] ?? "selfrestaurant.events";
            var routingKeyPrefix = _configuration["RabbitMq:RoutingKeyPrefix"] ?? "selfrestaurant";
            channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = envelope.EventName;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (!string.IsNullOrWhiteSpace(envelope.CorrelationId))
            {
                properties.CorrelationId = envelope.CorrelationId;
            }

            var body = Encoding.UTF8.GetBytes(envelopeJson);
            var routingKey = $"{routingKeyPrefix}.{envelope.EventName.Replace('.', '_')}";
            channel.BasicPublish(exchange, routingKey, properties, body);

            _logger.LogInformation("Published integration event {EventName} to RabbitMQ exchange {Exchange}", envelope.EventName, exchange);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to publish integration event {EventName} to RabbitMQ ({ErrorType}); outbox delivery remains available", envelope.EventName, ex.GetType().Name);
        }
    }
}
