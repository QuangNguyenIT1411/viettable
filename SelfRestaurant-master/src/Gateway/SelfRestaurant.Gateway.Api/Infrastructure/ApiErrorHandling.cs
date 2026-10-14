using System.Text.RegularExpressions;
using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

// Last resort for /api: an unhandled exception becomes a JSON error with a Vietnamese message instead of an
// empty 500 body. The log keeps only what is needed to find the fault (error type, method, path with ids
// masked, status, correlation id, time); never the exception message, query string, headers or body.
public static partial class ApiErrorHandling
{
    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next();
            return;
        }

        try
        {
            await next();
        }
        catch (Exception error)
        {
            var aborted = context.RequestAborted.IsCancellationRequested;
            var (status, code, message) = Describe(error, context.Request.Path.StartsWithSegments("/api/mobile/v1"));
            var correlationId = context.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier;
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SelfRestaurant.ApiErrors").LogError(
                "ApiError type={ErrorType} root={RootType} method={Method} path={Path} status={Status} aborted={Aborted} correlationId={CorrelationId} utc={Utc}",
                error.GetType().Name, error.GetBaseException().GetType().Name, context.Request.Method,
                SafePath(context.Request.Path.Value), aborted ? 499 : status, aborted, correlationId, DateTimeOffset.UtcNow);
            if (context.Response.HasStarted) throw;
            if (aborted) return; // Nobody is waiting for an answer.

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            await context.Response.WriteAsJsonAsync(MobileApiError.Create(code, message, correlationId));
        }
    });

    public static (int Status, string Code, string Message) Describe(Exception error, bool mobile)
    {
        // The versioned mobile API keeps its published contract.
        if (mobile) return (StatusCodes.Status503ServiceUnavailable, "service_unavailable", "Dịch vụ tạm thời không khả dụng.");
        return error is TimeoutException || error.GetBaseException() is TimeoutException || error is TaskCanceledException or OperationCanceledException
            ? (StatusCodes.Status504GatewayTimeout, "timeout", "Máy chủ phản hồi quá lâu (mã 504). Vui lòng thử lại.")
            : (StatusCodes.Status500InternalServerError, "server_error", "Máy chủ đang gặp sự cố (mã 500). Vui lòng thử lại sau ít phút.");
    }

    /// <summary>Path for logs: numeric ids and long opaque segments (codes, tokens) are masked.</summary>
    public static string SafePath(string? path) => string.Join('/', (path ?? "").Split('/').Select(segment =>
        Digits().IsMatch(segment) ? "{id}" : segment.Length > 32 ? "{value}" : segment));

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();
}
