using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Infrastructure.Auditing;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Infrastructure.Eventing;
using SelfRestaurant.Orders.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RestaurantDb")));

var servicesTimeout = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("Services:TimeoutSeconds") ?? 10, 2, 60));

builder.Services.AddHttpClient<ICatalogReadModel, CatalogApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101");
    http.Timeout = servicesTimeout;
});

builder.Services.AddHttpClient<ICustomerLoyaltyReadModel, CustomersApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "http://localhost:5104");
    http.Timeout = servicesTimeout;
});

builder.Services.AddHttpClient<BillingEventsClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Billing"] ?? "http://localhost:5105");
    http.Timeout = servicesTimeout;
});

builder.Services.AddHttpClient<BillingCheckoutGuardClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Billing"] ?? "http://localhost:5105");
    http.Timeout = servicesTimeout;
});

builder.Services.AddScoped<IIntegrationEventPublisher, FileIntegrationEventPublisher>();
builder.Services.AddScoped<RequestActorContextAccessor>();
builder.Services.AddScoped<BusinessAuditLogger>();
builder.Services.AddHostedService<PaymentCompletedConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await OrdersDbBootstrapper.EnsureReadyAsync(app.Services, app.Logger);

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
app.MapGet("/readyz", async (OrdersDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/internal/diagnostics/eventing", async (OrdersDbContext db, IConfiguration cfg, CancellationToken ct) =>
{
    var pendingOutbox = await db.OutboxEvents.CountAsync(x => x.Status == "PENDING", ct);
    var retryInbox = await db.InboxEvents.CountAsync(x => x.Status == "RETRY", ct);
    var deadInbox = await db.InboxEvents.CountAsync(x => x.Status == "DEAD", ct);
    return Results.Ok(new
    {
        service = "Orders.Api",
        rabbitMqEnabled = cfg.GetValue<bool?>("RabbitMq:Enabled") ?? false,
        outboxPending = pendingOutbox,
        inboxRetry = retryInbox,
        inboxDead = deadInbox
    });
});

app.Run();
