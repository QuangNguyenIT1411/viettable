using Microsoft.AspNetCore.Http;
using VietTable.CloudHost.Auth;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class CloudStaffOriginTests
{
    [Theory]
    [InlineData("https://preview.example", false, true)]
    [InlineData("http://preview.example", false, false)]
    [InlineData("https://foreign.example", false, false)]
    [InlineData("https://preview.example:444", false, false)]
    [InlineData("null", false, false)]
    [InlineData("https://user@preview.example", false, false)]
    [InlineData("https://preview.example/path", false, false)]
    [InlineData("http://preview.example", true, true)]
    [InlineData("https://preview.example", true, false)]
    public void Cloud_TLS_edge_origin_is_not_compared_to_internal_HTTP(string origin, bool development, bool allowed)
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http"; request.Host = new HostString("preview.example");
        request.Headers.Origin = origin;
        Assert.Equal(allowed, CloudStaffOriginPolicy.IsAllowed(request, development));
    }

    [Fact]
    public void Cross_site_and_duplicate_origins_are_rejected_even_when_host_matches()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http"; request.Host = new HostString("preview.example");
        request.Headers.Origin = "https://preview.example";
        request.Headers["Sec-Fetch-Site"] = "cross-site";
        Assert.False(CloudStaffOriginPolicy.IsAllowed(request, false));
        request.Headers.Remove("Sec-Fetch-Site");
        request.Headers.Origin = new Microsoft.Extensions.Primitives.StringValues(["https://preview.example", "https://preview.example"]);
        Assert.False(CloudStaffOriginPolicy.IsAllowed(request, false));
    }

    [Fact]
    public void Forwarded_headers_cannot_authorize_a_foreign_origin_and_no_origin_server_calls_remain_supported()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http"; request.Host = new HostString("preview.example");
        Assert.True(CloudStaffOriginPolicy.IsAllowed(request, false));
        request.Headers.Origin = "https://foreign.example";
        request.Headers["X-Forwarded-Host"] = "foreign.example";
        request.Headers["X-Forwarded-Proto"] = "https";
        Assert.False(CloudStaffOriginPolicy.IsAllowed(request, false));
    }
}
