using System.Text.Json;
using VietTable.CloudHost.Realtime;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class PrivateBackplaneTests
{
    [Theory]
    [InlineData("orderUpdated")]
    [InlineData("serviceRequestUpdated")]
    [InlineData("orderReady")]
    [InlineData("orderReceived")]
    [InlineData("paymentUpdated")]
    [InlineData("cashierDashboardChanged")]
    [InlineData("staffBranchChanged")]
    public void Existing_gateway_events_roundtrip_without_changing_wire_payload(string method)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var message = new PrivateGroupEvent(Guid.NewGuid().ToString("N"), "ABCDEF0123456789ABCDEF01", method,
            [JsonSerializer.SerializeToElement(new { OrderId=3, Status="READY", items=new[]{new{ItemId=5,Status="READY"}} }, options)]);
        var restored = JsonSerializer.Deserialize<PrivateGroupEvent>(JsonSerializer.Serialize(message, options), options)!;
        Assert.True(restored.IsValid());
        Assert.Equal("READY", restored.Arguments[0].GetProperty("status").GetString());
        Assert.Equal(5, restored.Arguments[0].GetProperty("items")[0].GetProperty("itemId").GetInt32());
    }
    [Theory]
    [InlineData("invalid-event-id", "ABCDEF0123456789ABCDEF01", "orderUpdated")]
    [InlineData("11111111111111111111111111111111", "mobile-customer:1", "orderUpdated")]
    [InlineData("11111111111111111111111111111111", "ABCDEF0123456789ABCDEF01", "client-chosen-method")]
    public void Malformed_or_unrecognized_private_envelopes_are_rejected(string id,string group,string method)
        => Assert.False(new PrivateGroupEvent(id,group,method,[]).IsValid());
    [Fact]
    public void Delivery_dedupe_is_thread_safe_and_bounded()
    {
        var window = new DeliveryWindow(); var first = 0;
        Parallel.For(0,100,_=> { if(window.First("same")) Interlocked.Increment(ref first); });
        Assert.Equal(1, first);
        for(var i=0;i<4096;i++) Assert.True(window.First("event-"+i));
        Assert.True(window.First("same")); // Old entries evicted; no unbounded cache.
        Assert.False(window.First("same"));
    }
}
