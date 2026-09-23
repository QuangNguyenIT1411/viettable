using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SelfRestaurant.Gateway.Api.Models;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var knownProxyText = builder.Configuration["ReverseProxy:KnownProxy"];
var publicHost = builder.Configuration["ReverseProxy:PublicHost"];
if (!string.IsNullOrWhiteSpace(knownProxyText) || !string.IsNullOrWhiteSpace(publicHost))
{
    if (!IPAddress.TryParse(knownProxyText, out var knownProxy) ||
        string.IsNullOrWhiteSpace(publicHost) ||
        Uri.CheckHostName(publicHost) != UriHostNameType.Dns ||
        publicHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("ReverseProxy requires a known proxy IP and a public DNS host.");

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 1;
        options.KnownProxies.Add(knownProxy);
        options.AllowedHosts.Add(publicHost);
    });
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api/mobile/v1"))
            return new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));

        var correlationId = context.HttpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.HttpContext.TraceIdentifier;
        var fields = context.ModelState
            .Where(pair => pair.Value?.Errors.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Giá trị không hợp lệ." : error.ErrorMessage).ToArray());
        return new BadRequestObjectResult(MobileApiError.Create(
            "validation_failed", "Dữ liệu gửi lên không hợp lệ.", correlationId, new { fields }));
    };
});
builder.Services.AddSignalR();

var mobileJwtIssuer = builder.Configuration["MobileJwt:Issuer"] ?? "SelfRestaurant.Identity";
var mobileJwtAudience = builder.Configuration["MobileJwt:Audience"] ?? "SelfRestaurant.Mobile";
var mobileJwtSigningKey = builder.Configuration["MobileJwt:SigningKey"] ?? "";
if (Encoding.UTF8.GetByteCount(mobileJwtSigningKey) < 32)
{
    throw new InvalidOperationException("MobileJwt:SigningKey must contain at least 32 UTF-8 bytes.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = mobileJwtIssuer,
            ValidateAudience = true,
            ValidAudience = mobileJwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mobileJwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(token)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/mobile-notifications"))
                    context.Token = token;
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var correlationId = context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? context.HttpContext.TraceIdentifier;
                await context.Response.WriteAsJsonAsync(MobileApiError.Create(
                    "unauthorized", "Access token không hợp lệ hoặc đã hết hạn.", correlationId));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var correlationId = context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? context.HttpContext.TraceIdentifier;
                await context.Response.WriteAsJsonAsync(MobileApiError.Create(
                    "forbidden", "Bạn không có quyền thực hiện thao tác này.", correlationId));
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<ActorContextHandler>();
var serviceTimeout = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("Services:TimeoutSeconds") ?? 10, 2, 60));

builder.Services.AddHttpClient<CatalogClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient<OrdersClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient<CustomersClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Customers"] ?? "http://localhost:5103");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient<IdentityClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "http://localhost:5104");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient<BillingClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Billing"] ?? "http://localhost:5105");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient("IdentityFallbackCustomers", http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Customers"] ?? "http://localhost:5103");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient("CustomersFallbackIdentity", http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "http://localhost:5104");
    http.Timeout = serviceTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>()
  .AddHttpMessageHandler<ActorContextHandler>();

builder.Services.AddHttpClient("Gemini", http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/");
    http.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<CustomerDishRecommendationService>();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(knownProxyText))
    app.UseForwardedHeaders();

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

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api/mobile/v1"))
    {
        await next();
        return;
    }

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled mobile API error at {Path}", context.Request.Path);
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        await context.Response.WriteAsJsonAsync(MobileApiError.Create(
            "service_unavailable", "Dịch vụ tạm thời không khả dụng.", correlationId));
    }
});

if (!app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Security:EnableHsts"))
{
    app.UseHsts();
}

if (app.Configuration.GetValue<bool?>("HttpsRedirection:Enabled") ?? false)
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/app", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            ApplyNoCacheHeaders(context.Response);
            return Task.CompletedTask;
        });
    }

    await next();
});

var sharedStaticRoot = ResolveStaticRoot(app.Environment.ContentRootPath);
if (sharedStaticRoot is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(sharedStaticRoot),
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase))
            {
                ApplyNoCacheHeaders(context.Context.Response);
            }
        }
    });
}

var customerAppRoot = ResolveAppRoot("customer-app", builder.Configuration["Frontend:CustomerDistPath"], "selfrestaurant-customer-web", app.Environment.ContentRootPath);
MapSpa(app, "/app/chef", ResolveAppRoot("chef-app", builder.Configuration["Frontend:ChefDistPath"], "selfrestaurant-chef-web", app.Environment.ContentRootPath));
MapSpa(app, "/app/cashier", ResolveAppRoot("cashier-app", builder.Configuration["Frontend:CashierDistPath"], "selfrestaurant-cashier-web", app.Environment.ContentRootPath));
MapSpa(app, "/app/admin", ResolveAppRoot("admin-app", builder.Configuration["Frontend:AdminDistPath"], "selfrestaurant-admin-web", app.Environment.ContentRootPath));
UseRootSpaFiles(app, customerAppRoot);

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CustomerNotificationsHub>("/hubs/customer-notifications");
app.MapHub<MobileNotificationsHub>("/hubs/mobile-notifications");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", () => Results.Ok(new { status = "ready" }));
app.MapGet("/Staff/Account/Login", (HttpContext context) => Results.Redirect($"/app/chef/Staff/Account/Login{context.Request.QueryString}"));
app.MapGet("/Staff/Account/ForgotPassword", (HttpContext context) => Results.Redirect($"/app/chef/Staff/Account/ForgotPassword{context.Request.QueryString}"));
app.MapGet("/Staff/Account/ResetPassword", (HttpContext context) => Results.Redirect($"/app/chef/Staff/Account/ResetPassword{context.Request.QueryString}"));
app.MapGet("/Staff/Chef", () => Results.Redirect("/app/chef/Staff/Chef/Index"));
app.MapGet("/Staff/Chef/{*path}", (string? path) =>
    Results.Redirect(string.IsNullOrWhiteSpace(path) ? "/app/chef/Staff/Chef/Index" : $"/app/chef/Staff/Chef/{path}"));
app.MapGet("/Staff/Cashier", () => Results.Redirect("/app/cashier/Staff/Cashier/Index"));
app.MapGet("/Staff/Cashier/{*path}", (string? path) =>
    Results.Redirect(string.IsNullOrWhiteSpace(path) ? "/app/cashier/Staff/Cashier/Index" : $"/app/cashier/Staff/Cashier/{path}"));
app.MapGet("/Admin/Account/Login", (HttpContext context) => Results.Redirect($"/app/chef/Staff/Account/Login{context.Request.QueryString}"));
app.MapGet("/Admin", () => Results.Redirect("/app/admin/Admin/Dashboard/Index"));
app.MapGet("/Admin/{*path}", (string? path) =>
    Results.Redirect(string.IsNullOrWhiteSpace(path) ? "/app/admin/Admin/Dashboard/Index" : $"/app/admin/Admin/{path}"));
app.MapGet("/app/customer", () => Results.Redirect("/"));
app.MapGet("/app/customer/{*path}", (string? path) => Results.Redirect(string.IsNullOrWhiteSpace(path) ? "/" : $"/{path}"));
MapRootSpa(app, customerAppRoot);
app.Run();

static void MapSpa(WebApplication app, string requestPath, string? root)
{
    if (root is null) return;

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(root),
        RequestPath = requestPath
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(root),
        RequestPath = requestPath,
        OnPrepareResponse = context => ApplyNoCacheHeaders(context.Context.Response)
    });

    app.MapGet($"{requestPath}/{{*path}}", async context =>
    {
        var indexFile = Path.Combine(root, "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyNoCacheHeaders(context.Response);
        await context.Response.SendFileAsync(indexFile);
    });
}

static void MapRootSpa(WebApplication app, string? root)
{
    if (root is null) return;

    app.MapGet("/{*path}", async context =>
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/app", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/assets", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/readyz", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var indexFile = Path.Combine(root, "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyNoCacheHeaders(context.Response);
        await context.Response.SendFileAsync(indexFile);
    });
}

static void UseRootSpaFiles(WebApplication app, string? root)
{
    if (root is null) return;

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(root)
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(root),
        OnPrepareResponse = context => ApplyNoCacheHeaders(context.Context.Response)
    });
}

static void ApplyNoCacheHeaders(HttpResponse response)
{
    response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    response.Headers["Pragma"] = "no-cache";
    response.Headers["Expires"] = "0";
}


static string? ResolveStaticRoot(string contentRootPath)
{
    foreach (var candidate in GetStaticRootCandidates(contentRootPath))
    {
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static IEnumerable<string> GetStaticRootCandidates(string contentRootPath)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var items = new List<string>();

    void Add(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return;
        var normalized = Path.GetFullPath(candidate);
        if (seen.Add(normalized)) items.Add(normalized);
    }

    Add(Path.Combine(contentRootPath, "wwwroot"));
    Add(Path.Combine(contentRootPath, "..", "..", "..", "wwwroot"));
    return items;
}

static string? ResolveAppRoot(string embeddedFolder, string? configuredPath, string frontendFolder, string contentRootPath)
{
    foreach (var candidate in GetAppCandidates(embeddedFolder, configuredPath, frontendFolder, contentRootPath))
    {
        if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
        {
            return candidate;
        }
    }

    return null;
}

static IEnumerable<string> GetAppCandidates(string embeddedFolder, string? configuredPath, string frontendFolder, string contentRootPath)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var items = new List<string>();

    void Add(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return;
        var normalized = Path.GetFullPath(candidate);
        if (seen.Add(normalized)) items.Add(normalized);
    }

    Add(Path.Combine(contentRootPath, "wwwroot", embeddedFolder));

    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        Add(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath));
    }

    Add(Path.Combine(contentRootPath, "..", "..", "Frontend", frontendFolder, "dist"));
    Add(Path.Combine(contentRootPath, "..", "..", "..", "..", "..", "Frontend", frontendFolder, "dist"));
    return items;
}
