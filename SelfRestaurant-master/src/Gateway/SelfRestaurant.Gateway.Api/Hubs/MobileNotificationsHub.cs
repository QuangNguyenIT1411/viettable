using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SelfRestaurant.Gateway.Api.Hubs;

[Authorize(Roles = "customer")]
public sealed class MobileNotificationsHub : Hub
{
    public static string CustomerGroup(int customerId) => $"mobile-customer:{customerId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var customerId) && customerId > 0)
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomerGroup(customerId));
        await base.OnConnectedAsync();
    }
}
