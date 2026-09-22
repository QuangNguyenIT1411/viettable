using Microsoft.AspNetCore.SignalR;

namespace SelfRestaurant.Gateway.Api.Hubs;

public sealed class CustomerNotificationsHub : Hub
{
    public static string TableGroup(int tableId) => $"table:{tableId}";
    public static string BranchGroup(int branchId) => $"branch:{branchId}";

    public Task SubscribeTable(int tableId)
    {
        if (tableId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, TableGroup(tableId));
    }

    public Task UnsubscribeTable(int tableId)
    {
        if (tableId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, TableGroup(tableId));
    }

    public Task SubscribeBranch(int branchId)
    {
        if (branchId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(branchId));
    }

    public Task UnsubscribeBranch(int branchId)
    {
        if (branchId <= 0)
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BranchGroup(branchId));
    }
}
