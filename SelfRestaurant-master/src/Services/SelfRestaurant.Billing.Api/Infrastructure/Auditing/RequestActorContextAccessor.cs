using Microsoft.AspNetCore.Http;

namespace SelfRestaurant.Billing.Api.Infrastructure.Auditing;

public sealed class RequestActorContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestActorContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public RequestActorContext GetCurrent()
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null)
        {
            return RequestActorContext.System(null);
        }

        return new RequestActorContext(
            headers["X-Actor-Type"].FirstOrDefault(),
            TryParseInt(headers["X-Actor-Id"].FirstOrDefault()),
            headers["X-Actor-Code"].FirstOrDefault(),
            headers["X-Actor-Name"].FirstOrDefault(),
            headers["X-Actor-RoleCode"].FirstOrDefault(),
            headers["X-Correlation-Id"].FirstOrDefault());
    }

    private static int? TryParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : null;
}
