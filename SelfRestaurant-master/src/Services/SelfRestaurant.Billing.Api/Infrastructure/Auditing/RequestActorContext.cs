namespace SelfRestaurant.Billing.Api.Infrastructure.Auditing;

public sealed record RequestActorContext(
    string? ActorType,
    int? ActorId,
    string? ActorCode,
    string? ActorName,
    string? ActorRoleCode,
    string? CorrelationId)
{
    public static RequestActorContext System(string? correlationId, string? actorName = "SYSTEM") =>
        new("SYSTEM", null, "system", actorName, "SYSTEM", correlationId);
}
