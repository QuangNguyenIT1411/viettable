using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace SelfRestaurant.CloudCompatibility;

public static class RabbitConnectionConfiguration
{
    public static ConnectionFactory Create(IConfiguration configuration)
    {
        var factory = new ConnectionFactory { DispatchConsumersAsync = true };
        var configuredUri = configuration["RabbitMq:Uri"];
        if (!string.IsNullOrWhiteSpace(configuredUri))
        {
            if (!Uri.TryCreate(configuredUri, UriKind.Absolute, out var uri)
                || (uri.Scheme != "amqp" && uri.Scheme != "amqps"))
                throw new InvalidOperationException("RabbitMq:Uri must be an absolute amqp/amqps URI.");
            // RabbitMQ.Client sets TLS and server-name validation for amqps. Never
            // disable certificate verification or log the credential-bearing URI.
            factory.Uri = uri;
        }
        else
        {
            factory.HostName = configuration["RabbitMq:Host"] ?? "localhost";
            factory.Port = configuration.GetValue<int?>("RabbitMq:Port") ?? 5672;
            factory.UserName = configuration["RabbitMq:Username"] ?? "guest";
            factory.Password = configuration["RabbitMq:Password"] ?? "guest";
            factory.VirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";
        }
        return factory;
    }
}
