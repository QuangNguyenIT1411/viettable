using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using SelfRestaurant.Identity.Api.Security;

namespace VietTable.CloudHost.Auth;

public static class CloudStaffConfiguration
{
    public static void Configure(WebApplicationBuilder builder, CloudIdentityBoundary identity)
    {
        var encoded = builder.Configuration["CloudHost:DataProtectionCertificate"];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("CloudHost requires a protected external DataProtection certificate.");
        X509Certificate2 certificate;
        try { certificate = new X509Certificate2(Convert.FromBase64String(encoded), (string?)null, X509KeyStorageFlags.EphemeralKeySet); }
        catch { throw new InvalidOperationException("Invalid CloudHost DataProtection certificate (value redacted)."); }
        if (!certificate.HasPrivateKey) throw new InvalidOperationException("DataProtection certificate requires private key.");
        builder.Services.AddSingleton(certificate);
        builder.Services.AddDataProtection().SetApplicationName("VietTable.CloudHost." +
            (builder.Configuration["CloudHost:EnvironmentNamespace"] ?? throw new InvalidOperationException("CloudHost environment namespace required.")))
            .ProtectKeysWithCertificate(certificate);
        builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = identity);
        builder.Services.AddSingleton<IStaffCookieValidator>(new StaffValidator(identity));
        builder.Services.AddAuthentication().AddCookie(StaffCookieState.Scheme, options =>
        {
            options.Cookie.Name = "VietTable.CloudStaff";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        // Customer Web is intentionally not deployed. Do not expose session-dependent
        // legacy Customer Web API while all existing /api/mobile/v1 routes remain.
        builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
            parts.FeatureProviders.Add(new WithoutCustomerWeb()));
    }
    private sealed class StaffValidator(CloudIdentityBoundary identity) : IStaffCookieValidator
    {
        public Task<string?> ValidateVersionAsync(int id, string role, int branch, string hash, CancellationToken ct) =>
            identity.ValidateStaffVersionAsync(id, role, branch, hash, ct);
        public Task<string?> VersionAsync(int id, string role, int branch, CancellationToken ct) => identity.StaffVersionAsync(id, role, branch, ct);
        public Task<bool> IsRevokedAsync(string hash, CancellationToken ct) => identity.StaffTicketRevokedAsync(hash, ct);
        public Task RevokeAsync(string hash, DateTimeOffset expiry, CancellationToken ct) => identity.RevokeStaffTicketAsync(hash, expiry, ct);
    }
    private sealed class WithoutCustomerWeb : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var type in feature.Controllers.Where(x => x.FullName ==
                "SelfRestaurant.Gateway.Api.Controllers.CustomerGatewayController").ToArray()) feature.Controllers.Remove(type);
        }
    }
}
