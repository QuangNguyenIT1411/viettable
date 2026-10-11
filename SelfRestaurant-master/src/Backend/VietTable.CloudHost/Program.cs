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
var mainEntryClock = System.Diagnostics.Stopwatch.StartNew();
Console.WriteLine("CloudHostTiming phase=main-entry");
var config = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
Console.WriteLine($"CloudHostTiming phase=configuration elapsedMs={mainEntryClock.ElapsedMilliseconds}");
var router = new ModuleRouter();
var modules = new System.Collections.Concurrent.ConcurrentDictionary<string, WebApplication>();
var owners = new[] { "Catalog", "Orders", "Customers", "Identity", "Billing" };
var servers = owners.ToDictionary(owner => owner, _ => new ModuleServer());
foreach (var owner in owners) router.Register(owner, servers[owner]);
using var performanceDiagnostics = config["VERCEL_ENV"] == "preview" ? new CloudPerformanceDiagnostics() : null;
var startupClock = System.Diagnostics.Stopwatch.StartNew();
var firstIncomingRequest = 0;
var requestSequence = 0L;
try
{
    async Task<WebApplication> InitializeOwnerAsync(string owner)
    {
        var server = servers[owner];
        using var startupMeasurement = performanceDiagnostics is not null
            ? CloudPerformanceDiagnostics.Begin(owner, "startup") : null;
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
            if (performanceDiagnostics is not null)
                builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<RequestPhaseFilter>());
        }
        var moduleClock = System.Diagnostics.Stopwatch.StartNew();
        var module = owner switch {
            "Catalog" => await CatalogModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Orders" => await OrdersModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Customers" => await CustomersModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Identity" => await IdentityModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            "Billing" => await BillingModule.BuildAsync(args, options, Configure, ModuleDiagnostics.Configure),
            _ => throw new InvalidOperationException("Unknown module.") };
        modules[owner] = module;
        Console.WriteLine($"CloudHostTiming module={owner} phase=build elapsedMs={moduleClock.ElapsedMilliseconds}");
        moduleClock.Restart();
        await module.StartAsync();
        Console.WriteLine($"CloudHostTiming module={owner} phase=start elapsedMs={moduleClock.ElapsedMilliseconds}");
        Console.WriteLine("CloudHostTransport started-module=" + owner);
        return module;
    }
    // Preview runtime only: no migrations/seeds or persistent workers run here.
    // Production and explicit migration/seed composition keep the previous order.
    var parallelStartup = ModuleStartup.CanInitializeInParallel(config);
    Console.WriteLine("CloudHostTiming startup-mode=" + (parallelStartup ? "parallel" : "sequential"));
    await ModuleStartup.InitializeAsync(owners, InitializeOwnerAsync, parallelStartup);
    var identityBoundary = new CloudIdentityBoundary(modules["Identity"].Services.GetRequiredService<IServiceScopeFactory>());
    var recovery = new CloudRecoveryCoordinator(
        modules["Catalog"].Services, modules["Orders"].Services, modules["Customers"].Services, modules["Billing"].Services);
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
            var securityClock = System.Diagnostics.Stopwatch.StartNew();
            CloudStaffConfiguration.Configure(builder, identityBoundary);
            performanceDiagnostics?.RecordStartupPhase("Gateway-security-certificate-registration", securityClock.Elapsed.TotalMilliseconds);
            builder.Services.AddHttpContextAccessor();
            if (performanceDiagnostics is not null)
                builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<RequestPhaseFilter>());
            builder.Services.AddSingleton<RealtimeDiagnostics>();
            builder.Services.AddSingleton(typeof(SupabasePrivateBackplane<>));
            builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(DiagnosticHubLifetimeManager<>));
            builder.Services.AddSingleton<RealtimeSubscriptionFilter>();
            builder.Services.AddSignalR(options => options.AddFilter<RealtimeSubscriptionFilter>());
        },
        app => {
            app.Use(async (context, next) => {
                if (Interlocked.Exchange(ref firstIncomingRequest, 1) == 0)
                    Console.WriteLine($"CloudHostTiming phase=first-incoming-request elapsedMs={mainEntryClock.ElapsedMilliseconds}");
                var media = CloudPublicMedia.Resolve(app.Configuration, context.Request.Path.Value ?? "");
                if (media is not null && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
                {
                    context.Response.Redirect(media);
                    return;
                }
                await next();
            });
            app.Use(async (context, next) => {
                using var measurement = performanceDiagnostics is not null
                    ? CloudPerformanceDiagnostics.Begin("Gateway", "request") : null;
                // Opaque process identifier; also present on WebSocket upgrade responses.
                context.Response.Headers["X-VietTable-Instance"] = app.Services.GetRequiredService<RealtimeDiagnostics>().InstanceId;
                if (performanceDiagnostics is not null && context.Request.Path == "/healthz")
                    context.Response.Headers["Server-Timing"] = performanceDiagnostics.StartupServerTiming;
                if (measurement is not null)
                {
                    var requestId = Guid.NewGuid().ToString("N");
                    var sequence = Interlocked.Increment(ref requestSequence);
                    context.Response.Headers["X-VietTable-Request"] = requestId;
                    context.Response.Headers["X-VietTable-Request-Sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    context.Response.Headers["X-VietTable-Process-Age-Ms"] = mainEntryClock.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    context.Response.OnStarting(() => {
                        if (context.Request.Path != "/healthz")
                            context.Response.Headers["Server-Timing"] = measurement.ServerTiming;
                        return Task.CompletedTask;
                    });
                    context.Response.OnCompleted(() => {
                        Console.WriteLine($"CloudPerformance completion request={requestId} sequence={sequence} status={context.Response.StatusCode} aborted={context.RequestAborted.IsCancellationRequested} {measurement.Summary}");
                        return Task.CompletedTask;
                    });
                }
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
    modules["Gateway"] = gateway;
    gateway.Lifetime.ApplicationStarted.Register(() => {
        performanceDiagnostics?.RecordStartupPhase("application-listener-ready", mainEntryClock.Elapsed.TotalMilliseconds);
        Console.WriteLine($"CloudHostTiming phase=listener-ready elapsedMs={startupClock.ElapsedMilliseconds}");
    });
    await gateway.RunAsync();
}
finally
{
    foreach (var owner in owners.Append("Gateway").Reverse())
    {
        if (!modules.TryGetValue(owner, out var module)) continue;
        await module.StopAsync(); await module.DisposeAsync();
    }
}
