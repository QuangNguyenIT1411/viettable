using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Infrastructure;

namespace SelfRestaurant.Gateway.Api.Hubs;

public sealed class CustomerNotificationsHub : Hub
{
    public static string TableGroup(int tableId) => $"table:{tableId}";
    public static string BranchGroup(int branchId) => $"branch:{branchId}";

    public Task SubscribeTable(int tableId)
    {
        CheckCloudSubscription(null); // Customer Web table subscriptions are legacy-only.
        if (tableId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, TableGroup(tableId));
    }

    public Task UnsubscribeTable(int tableId)
    {
        CheckCloudSubscription(null);
        if (tableId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, TableGroup(tableId));
    }

    public Task SubscribeBranch(int branchId)
    {
        CheckCloudSubscription(branchId);
        if (branchId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(branchId));
    }

    public Task UnsubscribeBranch(int branchId)
    {
        CheckCloudSubscription(branchId);
        if (branchId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BranchGroup(branchId));
    }

    private void CheckCloudSubscription(int? branchId)
    {
        var http = Context.GetHttpContext();
        if (http?.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("CloudHost:StatelessStaff") != true) return;
        var state = http.StaffIdentity();
        if (branchId is not > 0 || state.GetInt32(SessionKeys.EmployeeId) is not > 0 ||
            state.GetInt32(SessionKeys.EmployeeBranchId) != branchId)
            throw new HubException("Forbidden branch subscription.");
    }
}
