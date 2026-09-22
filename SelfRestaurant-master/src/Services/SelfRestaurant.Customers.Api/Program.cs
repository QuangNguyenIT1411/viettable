using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CustomersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RestaurantDb")));

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
builder.Services.AddHostedService<OrderReadyConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.Run();
