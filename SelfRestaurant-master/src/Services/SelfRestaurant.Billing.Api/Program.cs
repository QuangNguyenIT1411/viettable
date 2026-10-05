using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Auditing;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Eventing;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);
SelfRestaurant.CloudCompatibility.CloudHosting.Configure(builder);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers(options =>
{
    if (string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase))
        options.Filters.Add<SelfRestaurant.CloudCompatibility.PostgresConcurrencyFilter>();
});
builder.Services.AddAuthentication("CashierService").AddJwtBearer("CashierService", options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "SelfRestaurant.Gateway",
        ValidateAudience = true, ValidAudience = "SelfRestaurant.Billing.Cashier",
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(5),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["MobileJwt:SigningKey"]
                ?? throw new InvalidOperationException("MobileJwt:SigningKey required for cashier authentication"))),
        RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<PaymentFinalizationService>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BillingDbContext>(options =>
    SelfRestaurant.CloudCompatibility.DatabaseProvider.Configure(options, builder.Configuration, "Billing"));

var servicesTimeout = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("Services:TimeoutSeconds") ?? 10, 2, 60));

builder.Services.AddHttpClient<CustomersApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "http://localhost:5104");
    http.Timeout = servicesTimeout;
});

builder.Services.AddHttpClient<OrdersApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = servicesTimeout;
});

builder.Services.AddScoped<IIntegrationEventPublisher, FileIntegrationEventPublisher>();
builder.Services.AddScoped<RequestActorContextAccessor>();
builder.Services.AddScoped<BusinessAuditLogger>();
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("Payments:VnPay"));
builder.Services.AddSingleton<IPaymentProvider, VnPayPaymentProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await BillingDbBootstrapper.EnsureReadyAsync(app.Services, app.Logger);

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (BillingDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/internal/diagnostics/eventing", async (BillingDbContext db, IConfiguration cfg, CancellationToken ct) =>
{
    var pendingOutbox = await db.OutboxEvents.CountAsync(x => x.Status == "PENDING", ct);
    var processedOutbox = await db.OutboxEvents.CountAsync(x => x.Status == "PROCESSED", ct);
    return Results.Ok(new
    {
        service = "Billing.Api",
        rabbitMqEnabled = cfg.GetValue<bool?>("RabbitMq:Enabled") ?? false,
        outboxPending = pendingOutbox,
        outboxProcessed = processedOutbox
    });
});

app.Run();
