using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace VietTable.CloudHost.Composition;

// Internal adapters preserve module MVC filters, transactions and independent DI
// scopes. No sockets, self-HTTP, public internal routes, or cross-owner DbContext.
public sealed class ModuleRouter
{
    private readonly Dictionary<string, ModuleServer> modules = new(StringComparer.OrdinalIgnoreCase);
    public void Register(string name, ModuleServer server) => modules.Add(name + ".module.invalid", server);
    public bool TryGet(Uri uri, out ModuleServer server) => modules.TryGetValue(uri.Host, out server!);
}

public sealed class ModuleClientFilter(ModuleRouter router) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        builder.PrimaryHandler = new ModuleDispatchHandler(router) { InnerHandler = builder.PrimaryHandler };
    };
}

public sealed class ModuleDispatchHandler(ModuleRouter router) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri is not null && router.TryGet(request.RequestUri, out var server))
            return server.SendAsync(request, ct);
        // Internal hosts must fail closed, never escape to DNS/network.
        if (request.RequestUri?.Host.EndsWith(".module.invalid", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("Module is not available.");
        return base.SendAsync(request, ct); // Actual external providers only.
    }
}

public sealed class ModuleServer : IServer
{
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? dispatch;
    public IFeatureCollection Features { get; } = new FeatureCollection();
    public ModuleServer() => Features.Set<IServerAddressesFeature>(new ServerAddressesFeature());

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken ct)
        where TContext : notnull
    {
        dispatch = async (request, token) =>
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Module URI missing.");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var headers = new HeaderDictionary();
            foreach (var header in request.Headers) headers[header.Key] = new StringValues(header.Value.ToArray());
            using var input = request.Content is null ? new MemoryStream() : new MemoryStream(await request.Content.ReadAsByteArrayAsync(token));
            if (request.Content is not null)
                foreach (var header in request.Content.Headers) headers[header.Key] = new StringValues(header.Value.ToArray());
            headers["Host"] = uri.Authority;
            using var output = new MemoryStream();
            var response = new BufferedResponseFeature(output);
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature {
                // IHttpRequestFeature.Path is decoded, unlike Uri.AbsolutePath.
                // Use ASP.NET's request-line decoder (including encoded-slash rules).
                Method = request.Method.Method, Scheme = uri.Scheme, Path = PathString.FromUriComponent(uri).Value!,
                QueryString = uri.Query, RawTarget = uri.PathAndQuery, Protocol = "HTTP/1.1", Headers = headers, Body = input });
            features.Set<IHttpResponseFeature>(response);
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));
            features.Set<IHttpRequestLifetimeFeature>(new RequestLifetime(token));
            // Restore outer Gateway context after nested dispatch (actor/correlation handlers).
            var accessor = new HttpContextAccessor();
            var outer = accessor.HttpContext;
            var context = application.CreateContext(features);
            Exception? failure = null;
            try
            {
                await application.ProcessRequestAsync(context);
                await response.StartingAsync();
                var result = new HttpResponseMessage((HttpStatusCode)response.StatusCode) {
                    Content = new ByteArrayContent(output.ToArray()), RequestMessage = request };
                foreach (var header in response.Headers)
                    if (!result.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        result.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                return result;
            }
            catch (Exception error) { failure = error;
                Console.WriteLine("CloudHostTransport failure-type=" + error.GetBaseException().GetType().Name);
                throw; }
            finally
            {
                try { await response.CompletedAsync(); }
                finally { application.DisposeContext(context, failure); accessor.HttpContext = outer;
                    if (elapsed.ElapsedMilliseconds > 1000)
                        Console.WriteLine($"CloudHostTransport module={uri.Host} path={uri.AbsolutePath} elapsed-ms={elapsed.ElapsedMilliseconds}"); }
            }
        };
        return Task.CompletedTask;
    }
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var invoke = dispatch ?? throw new InvalidOperationException("Module has not started.");
        // HttpContextAccessor holders are shared by inherited ExecutionContexts.
        // A nested module request must not clear/replace the Gateway's holder when
        // its DefaultHttpContextFactory creates/disposes a different context.
        // Only explicit request headers/body cross this module boundary.
        using (ExecutionContext.SuppressFlow()) return Task.Run(() => invoke(request, ct), ct);
    }
    public Task StopAsync(CancellationToken ct) { dispatch = null; return Task.CompletedTask; }
    public void Dispose() => dispatch = null;

    private sealed class RequestLifetime(CancellationToken token) : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; } = token;
        public void Abort() { }
    }
    private sealed class BufferedResponseFeature(Stream body) : IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Action, object State)> starting = new(), completed = new();
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = body;
        public bool HasStarted { get; private set; }
        public void OnStarting(Func<object, Task> callback, object state) => starting.Push((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) => completed.Push((callback, state));
        public async Task StartingAsync() { while (starting.TryPop(out var item)) await item.Action(item.State); HasStarted = true; }
        public async Task CompletedAsync() { while (completed.TryPop(out var item)) await item.Action(item.State); }
    }
}
