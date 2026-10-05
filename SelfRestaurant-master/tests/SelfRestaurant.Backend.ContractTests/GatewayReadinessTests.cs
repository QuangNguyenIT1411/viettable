using System.Net;
using Microsoft.Extensions.Configuration;
using SelfRestaurant.Gateway.Api.Infrastructure;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class GatewayReadinessTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Readiness_requires_all_five_dependencies(bool unavailable, bool expected)
    {
        using var handler = new DependencyHandler(unavailable);
        using var http = new HttpClient(handler);
        var readiness = new GatewayReadiness(http, new ConfigurationBuilder().Build());
        Assert.Equal(expected, await readiness.IsReadyAsync(CancellationToken.None));
        Assert.Equal(5, handler.RequestCount);
    }

    private sealed class DependencyHandler(bool unavailable) : HttpMessageHandler
    {
        public int RequestCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref RequestCount);
            Assert.Equal("/readyz", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(unavailable && request.RequestUri.Port == 5105
                ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }
    }
}
