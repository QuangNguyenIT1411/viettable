namespace SelfRestaurant.Orders.Api.Domain.ServiceRequests;

public static class ServiceRequestStateMachine
{
    public const string Open = "OPEN";
    public const string Acknowledged = "ACKNOWLEDGED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static bool IsActive(string status) =>
        string.Equals(status, Open, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Acknowledged, StringComparison.OrdinalIgnoreCase);

    public static bool CanTransition(string current, string target) =>
        (string.Equals(current, Open, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target, Acknowledged, StringComparison.OrdinalIgnoreCase))
        || (string.Equals(current, Acknowledged, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target, Completed, StringComparison.OrdinalIgnoreCase))
        || (string.Equals(current, Open, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target, Cancelled, StringComparison.OrdinalIgnoreCase));
}
