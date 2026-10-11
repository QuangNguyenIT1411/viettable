using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Infrastructure;

namespace SelfRestaurant.Customers.Api;

// Shared composition root; standalone entrypoint and CloudHost use the same pipeline.
public static class CustomersModule
{
public static async Task<WebApplication> BuildAsync(string[] args,
    WebApplicationOptions? options = null, Action<WebApplicationBuilder>? configure = null,
    Action<WebApplication>? configurePipeline = null)
{
using var createBuilderTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "create-builder");
var builder = options is null
    ? WebApplication.CreateBuilder(args)
    : WebApplication.CreateBuilder(options);
createBuilderTiming?.Dispose();
using var registrationTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "registrations");
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

builder.Services.AddDbContext<CustomersDbContext>(options =>
    SelfRestaurant.CloudCompatibility.DatabaseProvider.Configure(options, builder.Configuration, "Customers"));

var servicesTimeout = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("Services:TimeoutSeconds") ?? 10, 2, 60));
builder.Services.AddHttpClient<OrdersQueryClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = servicesTimeout;
});
builder.Services.AddHttpClient<OrdersEventsClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = servicesTimeout;
});
builder.Services.AddTransient<OrderReadyConsumerService>();
if (builder.Configuration.GetValue<bool?>("Runtime:LegacyPersistentWorkers") ?? true)
    builder.Services.AddHostedService<OrderReadyConsumerService>();

registrationTiming?.Dispose();
using var buildTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "builder-build");
var app = builder.Build();
buildTiming?.Dispose();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "bootstrap"))
    await CustomersDbBootstrapper.EnsureReadyAsync(app.Services, app.Logger);

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

configurePipeline?.Invoke(app);
app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (CustomersDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/internal/diagnostics/eventing", async (CustomersDbContext db, CancellationToken ct) =>
{
    var openReadyNotifications = await db.ReadyDishNotifications.CountAsync(x => x.Status == "OPEN", ct);
    var retryInbox = await db.InboxEvents.CountAsync(x => x.Status == "RETRY", ct);
    var deadInbox = await db.InboxEvents.CountAsync(x => x.Status == "DEAD", ct);
    return Results.Ok(new
    {
        service = "Customers.Api",
        openReadyNotifications,
        inboxRetry = retryInbox,
        inboxDead = deadInbox
    });
});

return app;
}

}
