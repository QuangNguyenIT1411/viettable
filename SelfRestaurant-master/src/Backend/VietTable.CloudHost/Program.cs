using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Http;
using VietTable.CloudHost.Composition;
using SelfRestaurant.Catalog.Api;
using SelfRestaurant.Orders.Api;
using SelfRestaurant.Customers.Api;
using SelfRestaurant.Identity.Api;
using SelfRestaurant.Billing.Api;
using SelfRestaurant.Gateway.Api;
using SelfRestaurant.Identity.Api.Security;
using VietTable.CloudHost.Auth;
using VietTable.CloudHost;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using VietTable.CloudHost.Diagnostics;
using VietTable.CloudHost.Realtime;

// One public listener. Module containers are owned/disposed with this process.
var config = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var router = new ModuleRouter();
var modules = new List<WebApplication>();
try
{
    foreach (var owner in new[] { "Catalog", "Orders", "Customers", "Identity", "Billing" })
    {
        var server = new ModuleServer();
        router.Register(owner, server);
        var options = new WebApplicationOptions {
            ApplicationName = "SelfRestaurant." + owner + ".Api", Args = args,
            EnvironmentName = config["ASPNETCORE_ENVIRONMENT"] ?? "Production" };
        void Configure(WebApplicationBuilder builder)
        {
            builder.Configuration.AddConfiguration(config);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Runtime:LegacyPersistentWorkers"] = "false", ["HttpsRedirection:Enabled"] = "false",
                ["IntegrationEvents:Enabled"] = "true",
                ["IntegrationEvents:OutboxTrackedEvents:0"] = owner == "Orders"
                    ? "order.status-ready.v1" : "payment.completed.v1",
                ["CloudHost:DurableStaffReset"] = "true",
                ["Services:TimeoutSeconds"] = config["Services:TimeoutSeconds"] ?? "30",
                ["RabbitMq:Enabled"] = "false", ["IntegrationEvents:WriteDiagnosticFiles"] = "false" });
            foreach (var target in new[] { "Catalog", "Orders", "Customers", "Identity", "Billing" })
                builder.Configuration["Services:" + target] = "http://" + target + ".module.invalid";
            builder.Services.AddSingleton<IServer>(server);
            builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new ModuleClientFilter(router));
        }
        var module = owner switch {
            "Catalog" => await CatalogModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Orders" => await OrdersModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Customers" => await CustomersModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Identity" => await IdentityModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Billing" => await BillingModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            _ => throw new InvalidOperationException("Unknown module.") };
        modules.Add(module);
        await module.StartAsync();
        Console.WriteLine("CloudHostTransport started-module=" + owner);
    }
    var identityBoundary = new CloudIdentityBoundary(modules.Single(x =>
        x.Environment.ApplicationName == "SelfRestaurant.Identity.Api").Services.GetRequiredService<IServiceScopeFactory>());
    var recovery = new CloudRecoveryCoordinator(
        modules[0].Services, modules[1].Services, modules[2].Services, modules[4].Services);
    var gateway = await GatewayModule.BuildAsync(args, new WebApplicationOptions {
        ApplicationName = typeof(GatewayModule).Assembly.FullName, Args = args,
        EnvironmentName = config["ASPNETCORE_ENVIRONMENT"] ?? "Production" }, builder => {
            builder.Configuration.AddConfiguration(config);
            builder.Configuration["PORT"] = config["PORT"] ?? "5200";
            builder.Configuration["CloudHost:StatelessStaff"] = "true";
            builder.Configuration["Services:TimeoutSeconds"] = config["Services:TimeoutSeconds"] ?? "30";
            foreach (var target in new[] { "Catalog", "Orders", "Customers", "Identity", "Billing" })
                builder.Configuration["Services:" + target] = "http://" + target + ".module.invalid";
            builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new ModuleClientFilter(router));
        }, builder => {
            CloudStaffConfiguration.Configure(builder, identityBoundary);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<RealtimeDiagnostics>();
            builder.Services.AddSingleton(typeof(SupabasePrivateBackplane<>));
            builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(DiagnosticHubLifetimeManager<>));
            builder.Services.AddSingleton<RealtimeSubscriptionFilter>();
            builder.Services.AddSignalR(options => options.AddFilter<RealtimeSubscriptionFilter>());
        },
        app => {
            app.Use(async (context, next) => {
                var media = CloudPublicMedia.Resolve(app.Configuration, context.Request.Path.Value ?? "");
                if (media is not null && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
                {
                    context.Response.Redirect(media);
                    return;
                }
                await next();
            });
            app.Use(async (context, next) => {
                // Opaque process identifier; also present on WebSocket upgrade responses.
                context.Response.Headers["X-VietTable-Instance"] = app.Services.GetRequiredService<RealtimeDiagnostics>().InstanceId;
                await next();
            });
            app.Use(async (context,next) => {
                try { await next(); }
                catch(Exception error) {
                    Console.WriteLine("CloudHostTransport gateway-error-type="+error.GetBaseException().GetType().Name);
                    foreach(var frame in new System.Diagnostics.StackTrace(error.GetBaseException()).GetFrames()?.Take(8) ?? [])
                        Console.WriteLine("CloudHostTransport gateway-location="+frame.GetMethod()?.DeclaringType?.FullName+"."+frame.GetMethod()?.Name);
                    throw;
                }
            });
            app.UseMiddleware<StaffCookieMiddleware>();
            app.Use(async (context, next) => {
                await next();
                if (context.Response.StatusCode >= 400 || !HttpMethods.IsPost(context.Request.Method)) return;
                var path = context.Request.Path.Value ?? "";
                if (!(path.Contains("/chef/orders/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/ready", StringComparison.OrdinalIgnoreCase))
                    && !(path.Contains("/cashier/onsite-payments/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase))
                    && !(path.Contains("/cashier/orders/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/checkout", StringComparison.OrdinalIgnoreCase))) return;
                try {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    _ = await recovery.RunAsync(includeIdleCleanup: false, timeout.Token);
                }
                catch (Exception error) {
                    app.Logger.LogWarning("Cloud recovery fast path deferred ({ErrorType}); durable work remains for scheduled retry.", error.GetType().Name);
                }
            });
        });
    gateway.MapMethods("/api/internal/recovery", ["GET", "POST"], async (HttpContext context, CancellationToken ct) => {
        var secret = config["CRON_SECRET"];
        if (string.IsNullOrWhiteSpace(secret)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        var supplied = context.Request.Headers.Authorization.ToString();
        if (!supplied.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
        var actual = Encoding.UTF8.GetBytes(supplied[7..]);
        var expected = Encoding.UTF8.GetBytes(secret);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return Results.Unauthorized();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        var processed = await recovery.RunAsync(includeIdleCleanup: true, timeout.Token);
        return processed ? Results.Ok(new { status = "processed" })
            : Results.Accepted(value: new { status = "already_running" });
    });
    modules.Add(gateway);
    await gateway.RunAsync();
}
finally
{
    foreach (var module in modules.AsEnumerable().Reverse())
    { await module.StopAsync(); await module.DisposeAsync(); }
}
