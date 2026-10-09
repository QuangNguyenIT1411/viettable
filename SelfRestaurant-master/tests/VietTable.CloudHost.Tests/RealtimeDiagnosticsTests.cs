using Microsoft.Extensions.Configuration;
using VietTable.CloudHost.Diagnostics;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class RealtimeDiagnosticsTests
{
    private static RealtimeDiagnostics Create() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> {
            ["MobileJwt:SigningKey"] = "unit-test-only-signing-material-not-a-runtime-secret" }).Build());

    [Fact]
    public void References_match_between_instances_but_do_not_disclose_identity()
    {
        var first = Create(); var second = Create();
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.Equal(first.Reference("mobile-customer:987654"), second.Reference("mobile-customer:987654"));
        Assert.NotEqual(first.Reference("mobile-customer:987654"), first.Reference("mobile-customer:987655"));
        Assert.Matches("^[A-F0-9]{24}$", first.Reference("mobile-customer:987654"));
        Assert.DoesNotContain("987654", first.Reference("mobile-customer:987654"));
    }

    [Theory]
    [InlineData("mobile-customer:7", "customer")]
    [InlineData("branch:3", "branch")]
    [InlineData("table:8", "table")]
    [InlineData("untrusted-value", "other")]
    public void Group_classification_never_returns_raw_input(string group, string kind)
        => Assert.Equal(kind, RealtimeDiagnostics.GroupType(group));

    [Fact]
    public void Unknown_event_names_are_not_emitted()
    {
        Assert.Equal("other", RealtimeDiagnostics.EventType("secret-or-client-input"));
        Assert.Equal("orderUpdated", RealtimeDiagnostics.EventType("orderUpdated"));
        Assert.Equal("serviceRequestUpdated", RealtimeDiagnostics.EventType("serviceRequestUpdated"));
    }
}
