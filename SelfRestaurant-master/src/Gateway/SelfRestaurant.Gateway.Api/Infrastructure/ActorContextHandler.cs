using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

public sealed class ActorContextHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActorContextHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is not null)
        {
            // Services do not own the web session, so the gateway forwards only
            // minimal actor headers needed for trustworthy business audit logs.
            var employeeId = session.GetInt32(SessionKeys.EmployeeId);
            if (employeeId is > 0)
            {
                request.Headers.TryAddWithoutValidation("X-Actor-Type", "EMPLOYEE");
                request.Headers.TryAddWithoutValidation("X-Actor-Id", employeeId.Value.ToString());
                AddOptionalHeader(request, "X-Actor-Code", session.GetString(SessionKeys.EmployeeUsername));
                AddOptionalHeader(request, "X-Actor-Name", session.GetString(SessionKeys.EmployeeName));
                AddOptionalHeader(request, "X-Actor-RoleCode", session.GetString(SessionKeys.EmployeeRoleCode));
            }
            else
            {
                var customerId = session.GetInt32(SessionKeys.CustomerId);
                if (customerId is > 0)
                {
                    request.Headers.TryAddWithoutValidation("X-Actor-Type", "CUSTOMER");
                    request.Headers.TryAddWithoutValidation("X-Actor-Id", customerId.Value.ToString());
                    AddOptionalHeader(request, "X-Actor-Code", session.GetString(SessionKeys.CustomerUsername));
                    AddOptionalHeader(request, "X-Actor-Name", session.GetString(SessionKeys.CustomerName));
                    request.Headers.TryAddWithoutValidation("X-Actor-RoleCode", "CUSTOMER");
                }
            }
        }

        if (!request.Headers.Accept.Any())
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void AddOptionalHeader(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.All(static ch => ch <= sbyte.MaxValue))
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }
}
