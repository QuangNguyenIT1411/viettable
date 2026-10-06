using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Catalog.Api.Infrastructure.Tables;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Infrastructure.Eventing;

namespace VietTable.CloudHost;

/// <summary>
/// A finite, request-triggered recovery pass. The Billing PostgreSQL advisory lock
/// serializes passes across all CloudHost instances without an in-memory lease.
/// Each processor still accesses only its owning module/DbContext.
/// </summary>
public sealed class CloudRecoveryCoordinator(
    IServiceProvider catalog,
    IServiceProvider orders,
    IServiceProvider customers,
    IServiceProvider billing)
{
    // False means another instance owns this pass; its durable work remains visible
    // and the caller can retry rather than turning ordinary contention into HTTP 500.
    public async Task<bool> RunAsync(bool includeIdleCleanup, CancellationToken ct)
    {
        await using var lockScope = billing.CreateAsyncScope();
        var lockDb = lockScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await using var transaction = await lockDb.Database.BeginTransactionAsync(ct);
        try
        {
            if (lockDb.Database.IsNpgsql())
            {
                var acquired = await lockDb.Database
                    .SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(731194610263) AS \"Value\"")
                    .SingleAsync(ct);
                if (!acquired) return false;
            }
            else
                await lockDb.Database.ExecuteSqlRawAsync(
                    "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='viettable-cloud-recovery', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Recovery busy', 1;", ct);
        }
        catch (Exception ex) { Report("lock", ex); throw; }

        try
        {
            await using var scope = billing.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PaymentReconciliationWorker>().RunOnceAsync(ct);
        }
        catch (Exception ex) { Report("billing", ex); throw; }
        try
        {
            await using var scope = orders.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PaymentCompletedConsumerService>().ConsumeBatchAsync(20, ct);
        }
        catch (Exception ex) { Report("orders", ex); throw; }
        try
        {
            await using var scope = customers.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OrderReadyConsumerService>().ConsumeBatchAsync(20, ct);
        }
        catch (Exception ex) { Report("customers", ex); throw; }
        if (includeIdleCleanup)
        {
            try
            {
                await using var scope = catalog.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TableAutoReleaseService>().RunOnceAsync(ct);
            }
            catch (Exception ex) { Report("catalog", ex); throw; }
        }

        await transaction.CommitAsync(ct);
        return true;
    }

    private static void Report(string stage, Exception ex)
    {
        var sqlState = (ex.GetBaseException() as Npgsql.PostgresException)?.SqlState ?? "none";
        Console.WriteLine($"CloudHostTransport recovery-stage={stage} error-type={ex.GetBaseException().GetType().Name} sqlstate={sqlState}");
    }
}
