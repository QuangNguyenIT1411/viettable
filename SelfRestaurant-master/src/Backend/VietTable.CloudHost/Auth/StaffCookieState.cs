using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SelfRestaurant.Gateway.Api.Infrastructure;

namespace VietTable.CloudHost.Auth;

public sealed class StaffCookieState : ISession
{
    public const string Scheme = "CloudStaff";
    private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
    public bool Changed { get; private set; }
    public bool IsAvailable => true;
    public string Id => "cookie-claims";
    public IEnumerable<string> Keys => values.Keys;
    public StaffCookieState(ClaimsPrincipal? principal)
    {
        foreach (var claim in principal?.FindAll("staff-state") ?? [])
        {
            var parts = claim.Value.Split(':', 2);
            if (parts.Length == 2) values[parts[0]] = Convert.FromBase64String(parts[1]);
        }
    }
    public void Clear() { values.Clear(); Changed = true; }
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Remove(string key) { if (values.Remove(key)) Changed = true; }
    public void Set(string key, byte[] value) { values[key] = value.ToArray(); Changed = true; }
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => values.TryGetValue(key, out value);
    public ClaimsPrincipal Principal(string version) => new(new ClaimsIdentity(
        values.Select(x => new Claim("staff-state", x.Key + ":" + Convert.ToBase64String(x.Value)))
            .Append(new Claim("auth-version", version)), Scheme));
}

public interface IStaffCookieValidator
{
    Task<string?> VersionAsync(int employeeId, string role, int branch, CancellationToken ct);
}

public sealed class StaffCookieMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IStaffCookieValidator validator)
    {
        var auth = await context.AuthenticateAsync(StaffCookieState.Scheme);
        var state = new StaffCookieState(auth.Succeeded ? auth.Principal : null);
        var id = state.GetInt32(SessionKeys.EmployeeId);
        string? version = null;
        if (id is > 0)
        {
            version = await validator.VersionAsync(id.Value, state.GetString(SessionKeys.EmployeeRoleCode) ?? "",
                state.GetInt32(SessionKeys.EmployeeBranchId) ?? 0, context.RequestAborted);
            if (version is null || version != auth.Principal?.FindFirst("auth-version")?.Value) state.Clear();
        }
        context.Items[StaffIdentityState.ItemKey] = state;
        if (version is not null && !state.Changed && auth.Principal is not null)
            context.User = auth.Principal; // JWT middleware still replaces this for a valid mobile bearer.
        // Cookie-authenticated unsafe requests must originate from this host. Mobile
        // JWT endpoints are unaffected. SameSite=Lax and this check stop cross-site POSTs.
        if (context.Request.Path.StartsWithSegments("/api/gateway") && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site" ||
                (!string.IsNullOrWhiteSpace(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase) || uri.Scheme != context.Request.Scheme)))
            { context.Response.StatusCode = 403; return; }
        }
        context.Response.OnStarting(async () =>
        {
            if (!state.Changed) return;
            var employee = state.GetInt32(SessionKeys.EmployeeId);
            if (employee is not > 0) { await context.SignOutAsync(StaffCookieState.Scheme); return; }
            var current = await validator.VersionAsync(employee.Value, state.GetString(SessionKeys.EmployeeRoleCode) ?? "",
                state.GetInt32(SessionKeys.EmployeeBranchId) ?? 0, context.RequestAborted);
            if (current is null) { await context.SignOutAsync(StaffCookieState.Scheme); return; }
            await context.SignInAsync(StaffCookieState.Scheme, state.Principal(current),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        });
        await next(context);
    }
}
