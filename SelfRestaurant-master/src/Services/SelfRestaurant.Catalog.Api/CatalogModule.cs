using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Infrastructure.Auditing;
using SelfRestaurant.Catalog.Api.Infrastructure.Inventory;
using SelfRestaurant.Catalog.Api.Infrastructure.Tables;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api;

// Shared composition root; standalone entrypoint and CloudHost use the same pipeline.
public static class CatalogModule
{
public static async Task<WebApplication> BuildAsync(string[] args,
    WebApplicationOptions? options = null, Action<WebApplicationBuilder>? configure = null,
    Action<WebApplication>? configurePipeline = null)
{
var builder = options is null
    ? WebApplication.CreateBuilder(args)
    : WebApplication.CreateBuilder(options);
configure?.Invoke(builder);
SelfRestaurant.CloudCompatibility.CloudHosting.Configure(builder);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers(options =>
{
    if (string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase))
        options.Filters.Add<SelfRestaurant.CloudCompatibility.PostgresConcurrencyFilter>();
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    SelfRestaurant.CloudCompatibility.DatabaseProvider.Configure(options, builder.Configuration, "Catalog"));
builder.Services.AddScoped<RequestActorContextAccessor>();
builder.Services.AddScoped<BusinessAuditLogger>();
builder.Services.AddScoped<IngredientStockAvailabilityService>();
builder.Services.Configure<TableAutoReleaseOptions>(builder.Configuration.GetSection("TableAutoRelease"));
builder.Services.AddHttpClient<OrdersActiveTablesClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddTransient<TableAutoReleaseService>();
if (builder.Configuration.GetValue<bool?>("Runtime:LegacyPersistentWorkers") ?? true)
    builder.Services.AddHostedService<TableAutoReleaseService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await CatalogDbBootstrapper.EnsureReadyAsync(app.Services, app.Logger);

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
app.MapGet("/readyz", async (CatalogDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

return app;
}

}
