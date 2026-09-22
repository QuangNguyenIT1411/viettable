using System.Text.Json;
using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileApiErrorTests
{
    [Fact]
    public void Mobile_error_serializes_with_stable_camel_case_shape()
    {
        var error = MobileApiError.Create("validation_failed", "Dữ liệu không hợp lệ.", "correlation-123");
        var json = JsonSerializer.Serialize(error);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("validation_failed", root.GetProperty("code").GetString());
        Assert.Equal("Dữ liệu không hợp lệ.", root.GetProperty("message").GetString());
        Assert.Equal("correlation-123", root.GetProperty("correlationId").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("details").ValueKind);
    }
}
