using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

/// <summary>Who made the change, which decides how the branch is found.</summary>
public enum StaffEventSource { Staff, CustomerWeb, Mobile }

/// <summary>
/// After a successful change, nudges the staff screens of that branch ("staffBranchChanged") so the
/// kitchen and cashier reload at once instead of polling. The event carries only the branch id and topic
/// names, never order or customer data. Best effort: a failed nudge never fails the request; staff
/// screens keep a slow fallback poll.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StaffBranchEventAttribute(StaffEventSource source, params string[] topics) : Attribute, IAsyncActionFilter
{
    public StaffEventSource Source { get; } = source;
    public IReadOnlyList<string> Topics { get; } = topics;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled) return;
        var status = executed.Result is IStatusCodeActionResult result ? result.StatusCode ?? 200 : 200;
        if (status is < 200 or > 299) return;
        await context.HttpContext.RequestServices.GetRequiredService<StaffBranchEvents>()
            .PublishAsync(context.HttpContext, Source, Topics);
    }
}

public sealed class StaffBranchEvents(
    IHubContext<CustomerNotificationsHub> hub, OrdersClient orders, ILogger<StaffBranchEvents> logger)
{
    public const string Method = "staffBranchChanged";
    public const string Orders = "orders";
    public const string OnsitePayments = "onsitePayments";
    public const string ServiceRequests = "serviceRequests";
    public const string Reservations = "reservations";

    public async Task PublishAsync(HttpContext http, StaffEventSource source, IReadOnlyList<string> topics)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var branchId = await ResolveBranchAsync(http, source, timeout.Token);
            if (branchId is not > 0) return;
            await PublishAsync(branchId.Value, topics, timeout.Token);
        }
        catch (Exception error)
        {
            logger.LogWarning("Staff branch event skipped ({ErrorType}).", error.GetType().Name);
        }
    }

    public Task PublishAsync(int branchId, IReadOnlyList<string> topics, CancellationToken ct) =>
        hub.Clients.Group(CustomerNotificationsHub.BranchGroup(branchId)).SendAsync(Method, new { branchId, topics }, ct);

    private async Task<int?> ResolveBranchAsync(HttpContext http, StaffEventSource source, CancellationToken ct)
    {
        switch (source)
        {
            case StaffEventSource.Staff:
                var staff = http.Items.TryGetValue(StaffIdentityState.ItemKey, out var item) && item is ISession state
                    ? state : http.Features.Get<ISessionFeature>()?.Session;
                return staff?.GetInt32(SessionKeys.EmployeeId) is > 0 ? staff.GetInt32(SessionKeys.EmployeeBranchId) : null;
            case StaffEventSource.CustomerWeb:
                return http.Features.Get<ISessionFeature>()?.Session?.GetInt32(SessionKeys.CurrentBranchId);
            default:
                if (!int.TryParse(http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var customerId) || customerId <= 0) return null;
                if (http.GetRouteValue("sessionId") is string raw && int.TryParse(raw, out var sessionId))
                {
                    var session = await orders.GetDiningSessionAsync(sessionId, customerId, ct);
                    if (session is not null) return session.BranchId;
                }
                return (await orders.GetActiveDiningSessionAsync(customerId, ct))?.BranchId;
        }
    }
}
