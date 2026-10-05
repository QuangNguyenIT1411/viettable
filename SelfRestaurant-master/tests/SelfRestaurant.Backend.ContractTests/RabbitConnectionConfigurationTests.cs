using Microsoft.Extensions.Configuration;
using SelfRestaurant.CloudCompatibility;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class RabbitConnectionConfigurationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(x =>
            new KeyValuePair<string, string?>(x.Key, x.Value))).Build();

    [Theory]
    [InlineData("amqp://broker.invalid/qa", false, 5672)]
    [InlineData("amqps://broker.invalid/qa", true, 5671)]
    public void Uri_selects_tls_port_and_virtual_host(string uri, bool tls, int port)
    {
        var factory = RabbitConnectionConfiguration.Create(Config(("RabbitMq:Uri", uri)));
        Assert.Equal("broker.invalid", factory.HostName);
        Assert.Equal("qa", factory.VirtualHost);
        Assert.Equal(tls, factory.Ssl.Enabled);
        Assert.Equal(port, factory.Port);
        if (tls) Assert.Equal("broker.invalid", factory.Ssl.ServerName);
    }

    [Fact]
    public void Split_configuration_remains_supported()
    {
        var factory = RabbitConnectionConfiguration.Create(Config(
            ("RabbitMq:Host", "local-broker"), ("RabbitMq:Port", "5673"),
            ("RabbitMq:VirtualHost", "/demo")));
        Assert.Equal("local-broker", factory.HostName);
        Assert.Equal(5673, factory.Port);
        Assert.Equal("/demo", factory.VirtualHost);
        Assert.False(factory.Ssl.Enabled);
    }

    [Theory]
    [InlineData("https://broker.invalid/")]
    [InlineData("invalid")]
    public void Invalid_uri_error_does_not_echo_configuration(string uri)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            RabbitConnectionConfiguration.Create(Config(("RabbitMq:Uri", uri))));
        Assert.DoesNotContain(uri, error.Message);
    }
}
