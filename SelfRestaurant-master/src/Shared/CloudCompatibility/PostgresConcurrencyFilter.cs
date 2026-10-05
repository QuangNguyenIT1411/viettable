using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

namespace SelfRestaurant.CloudCompatibility;

// Runs after the action's transaction has been disposed. Never retries a whole
// action: it may already have called another service. Clients resync and retry
// the command using its original idempotency key.
internal sealed class PostgresConcurrencyFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (!IsConflict(context.Exception)) return;
        context.Result = new ConflictObjectResult(new {
            code = "command_in_progress",
            message = "Dữ liệu đang được cập nhật. Vui lòng kiểm tra lại và thử lại.",
            correlationId = context.HttpContext.Response.Headers["X-Correlation-Id"].ToString()
        });
        context.ExceptionHandled = true;
    }

    public static bool IsConflict(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException { SqlState: "40001" or "40P01" }) return true;
            // Two requests may refresh the same Orders-owned snapshot from a cold
            // cache. These exact PK conflicts are retriable, not dish-unavailable.
            if (exception is PostgresException { SqlState: "23505", ConstraintName:
                "PK_CatalogDishSnapshots" or "PK_CatalogTableSnapshots" or "PK_CatalogBranchSnapshots" }) return true;
            exception = exception.InnerException;
        }
        return false;
    }
}
