using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Infrastructure.Auditing;
using SelfRestaurant.Catalog.Api.Infrastructure.Inventory;
using SelfRestaurant.Catalog.Api.Infrastructure.Tables;
using SelfRestaurant.Catalog.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var catalogConnectionString =
    builder.Configuration.GetConnectionString("CatalogDb") ??
    builder.Configuration.GetConnectionString("RestaurantDb") ??
    throw new InvalidOperationException("Missing connection string: ConnectionStrings:CatalogDb");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(catalogConnectionString));
builder.Services.AddScoped<RequestActorContextAccessor>();
builder.Services.AddScoped<BusinessAuditLogger>();
builder.Services.AddScoped<IngredientStockAvailabilityService>();
builder.Services.Configure<TableAutoReleaseOptions>(builder.Configuration.GetSection("TableAutoRelease"));
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

app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (CatalogDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.Run();
