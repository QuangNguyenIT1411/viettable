using System.Text.Json.Serialization;

namespace SelfRestaurant.Gateway.Api.Models;

/// <summary>
/// Stable error envelope reserved for the versioned mobile API.
/// Existing web-gateway responses remain unchanged during the mobile migration.
/// </summary>
public sealed record MobileApiError(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("details")] object? Details = null)
{
    public static MobileApiError Create(string code, string message, string correlationId, object? details = null) =>
        new(false, code, message, correlationId, details);
}
