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
        }, builder => CloudStaffConfiguration.Configure(builder, identityBoundary),
        app => {
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
        });
    modules.Add(gateway);
    await gateway.RunAsync();
}
finally
{
    foreach (var module in modules.AsEnumerable().Reverse())
    { await module.StopAsync(); await module.DisposeAsync(); }
}
