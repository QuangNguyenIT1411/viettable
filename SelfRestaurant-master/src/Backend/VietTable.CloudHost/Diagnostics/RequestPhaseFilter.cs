using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace VietTable.CloudHost.Diagnostics;

// Only registered by Preview composition. Result timing includes formatting and
// buffered module output; the outer response's completion is logged separately.
public sealed class RequestPhaseFilter : IAsyncActionFilter, IAsyncResultFilter
{
    // Action descriptors are code-owned. Never use URLs, IDs or user input as metric names.
    internal static string? ReadPhase(string controller, string action) => (controller, action) switch {
        ("Orders", "GetChefOrders") => "read-chef-orders",
        ("Orders", "GetChefHistory") => "read-chef-history",
        ("Orders", "GetCashierOrdersInternal") => "read-orders-cashier",
        ("Cashier", "GetCashierOrders") => "read-billing-cashier",
        ("CashierBills", "GetBranchReport") => "read-cashier-report",
        ("MobileCheckout", "CustomerInvoices") => "read-invoices",
        _ => null
    };
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        using var timing = CloudPerformanceDiagnostics.HasScope
            ? CloudPerformanceDiagnostics.Begin("MVC", "action") : null;
        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var phase = descriptor is null ? null : ReadPhase(descriptor.ControllerName, descriptor.ActionName);
        using var readTiming = phase is not null && CloudPerformanceDiagnostics.HasScope
            ? CloudPerformanceDiagnostics.Begin("MVC", phase) : null;
        await next();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        using var timing = CloudPerformanceDiagnostics.HasScope
            ? CloudPerformanceDiagnostics.Begin("MVC", "result") : null;
        await next();
    }
}
