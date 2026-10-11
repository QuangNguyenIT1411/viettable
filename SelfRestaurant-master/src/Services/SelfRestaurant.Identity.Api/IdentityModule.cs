using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Infrastructure;
using SelfRestaurant.Identity.Api.Infrastructure.Auditing;
using SelfRestaurant.Identity.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SelfRestaurant.Identity.Api;

// Shared composition root; standalone entrypoint and CloudHost use the same pipeline.
public static class IdentityModule
{
public static async Task<WebApplication> BuildAsync(string[] args,
    WebApplicationOptions? options = null, Action<WebApplicationBuilder>? configure = null,
    Action<WebApplication>? configurePipeline = null)
{
using var createBuilderTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Identity", "create-builder");
var builder = options is null
    ? WebApplication.CreateBuilder(args)
    : WebApplication.CreateBuilder(options);
createBuilderTiming?.Dispose();
using var registrationTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Identity", "registrations");
configure?.Invoke(builder);
SelfRestaurant.CloudCompatibility.CloudHosting.Configure(builder);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers(options =>
{
    if (string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase))
        options.Filters.Add<SelfRestaurant.CloudCompatibility.PostgresConcurrencyFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
using var jwtTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Identity", "jwt-registration");
builder.Services.Configure<MobileJwtOptions>(builder.Configuration.GetSection(MobileJwtOptions.SectionName));
builder.Services.AddSingleton<MobileTokenService>();
var mobileJwt = builder.Configuration.GetSection(MobileJwtOptions.SectionName).Get<MobileJwtOptions>()
    ?? throw new InvalidOperationException("Missing MobileJwt configuration.");
if (Encoding.UTF8.GetByteCount(mobileJwt.SigningKey) < 32)
    throw new InvalidOperationException("MobileJwt:SigningKey must contain at least 32 UTF-8 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = mobileJwt.Issuer,
            ValidateAudience = true,
            ValidAudience = mobileJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mobileJwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();
jwtTiming?.Dispose();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("identity-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{context.Connection.RemoteIpAddress}:{context.Request.Path}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                // Legitimate cross-actor flows in the new gateway can trigger several
                // staff auth requests back-to-back (chef, cashier, admin, password flows).
                // Keep basic abuse protection, but avoid blocking normal usage and QA runs.
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddDbContext<IdentityDbContext>(options =>
    SelfRestaurant.CloudCompatibility.DatabaseProvider.Configure(options, builder.Configuration, "Identity"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.PostConfigure<SmtpOptions>(options =>
{
    if (!string.IsNullOrWhiteSpace(options.Password))
    {
        return;
    }

    options.Password = Environment.GetEnvironmentVariable("SELFRESTAURANT_SMTP_PASSWORD") ?? "";
});
builder.Services.AddSingleton<PasswordResetEmailSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BusinessAuditLogger>();
builder.Services.AddHttpClient<OrdersApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<BillingApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Billing"] ?? "http://localhost:5105");
    http.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<CatalogApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101");
    http.Timeout = TimeSpan.FromSeconds(10);
});

registrationTiming?.Dispose();
using var buildTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Identity", "builder-build");
var app = builder.Build();
buildTiming?.Dispose();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Identity", "bootstrap"))
    await IdentityDbBootstrapper.EnsureReadyAsync(app.Services, app.Logger);

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
    }

    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (app.Logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = correlationId }))
    {
        await next();
    }
});

if (app.Configuration.GetValue<bool?>("HttpsRedirection:Enabled") ?? app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

configurePipeline?.Invoke(app);
app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (IdentityDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

return app;
}

}
