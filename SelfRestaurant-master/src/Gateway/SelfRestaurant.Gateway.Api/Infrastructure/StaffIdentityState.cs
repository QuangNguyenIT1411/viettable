using Microsoft.AspNetCore.Http;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

// Compatibility facade for existing staff controllers. Legacy mode still uses
// ASP.NET Session. CloudHost supplies request-local state hydrated from protected
// cookie claims; no server-session cache is used for staff authorization.
public static class StaffIdentityState
{
    public const string ItemKey = "VietTable.CloudStaffIdentity";
    public static ISession StaffIdentity(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var identity) && identity is ISession state
            ? state : context.Session;
}
