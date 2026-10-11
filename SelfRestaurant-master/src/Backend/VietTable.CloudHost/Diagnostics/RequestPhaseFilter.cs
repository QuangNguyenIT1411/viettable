using Microsoft.AspNetCore.Mvc.Filters;

namespace VietTable.CloudHost.Diagnostics;

// Only registered by Preview composition. Result timing includes formatting and
// buffered module output; the outer response's completion is logged separately.
public sealed class RequestPhaseFilter : IAsyncActionFilter, IAsyncResultFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        using var timing = CloudPerformanceDiagnostics.HasScope
            ? CloudPerformanceDiagnostics.Begin("MVC", "action") : null;
        await next();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        using var timing = CloudPerformanceDiagnostics.HasScope
            ? CloudPerformanceDiagnostics.Begin("MVC", "result") : null;
        await next();
    }
}
