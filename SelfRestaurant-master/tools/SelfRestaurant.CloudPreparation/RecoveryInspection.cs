using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence;

// Explicit operator-run, read-only test harness, not a microservice API.
// Each context uses its own runtime-owner credential, never cross-schema SQL.
internal static class RecoveryInspection
{
    public static async Task RunAsync(Input input)
    {
        if(input.RecoverySessionId<=0) throw new CloudAcceptanceException("Explicit QA session ID required.");
        await using var orders=new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(CloudWorkflow.RuntimeConnection(input,"orders")).Options);
        await using var billing=new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(CloudWorkflow.RuntimeConnection(input,"billing")).Options);
        await using var customers=new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(CloudWorkflow.RuntimeConnection(input,"customers")).Options);
        await using var catalog=new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(CloudWorkflow.RuntimeConnection(input,"catalog")).Options);
        var session=await orders.DiningSessions.AsNoTracking().SingleAsync(x=>x.DiningSessionId==input.RecoverySessionId);
        var ids=await orders.Orders.AsNoTracking().Where(x=>x.DiningSessionCode==session.SessionCode).Select(x=>x.OrderID).ToArrayAsync();
        var events=await orders.OutboxEvents.AsNoTracking().Where(x=>x.EventName=="order.status-ready.v1").ToListAsync();
        var pending=events.Where(x=> {using var json=JsonDocument.Parse(x.PayloadJson);return ids.Contains(json.RootElement.GetProperty("orderId").GetInt32());}).ToArray();
        // A read-only inspection must also support an unpaid active session.
        var invoice=await billing.Invoices.AsNoTracking().SingleOrDefaultAsync(x=>x.DiningSessionId==session.DiningSessionId);
        var orderStates=await orders.Orders.AsNoTracking().Where(x=>ids.Contains(x.OrderID))
            .Select(x=>new{x.OrderID,status=x.Status.StatusCode,x.OrderTime,x.CompletedTime}).ToArrayAsync();
        var itemStates=await orders.OrderItems.AsNoTracking().Where(x=>ids.Contains(x.OrderID))
            .Select(x=>new{x.OrderID,x.ItemID,x.StatusCode,x.Quantity,x.LineTotal}).ToArrayAsync();
        var auditTimes=await orders.BusinessAuditLogs.AsNoTracking().Where(x=>x.OrderId.HasValue&&ids.Contains(x.OrderId.Value))
            .OrderByDescending(x=>x.CreatedAtUtc).Take(20)
            .Select(x=>new{x.ActionType,x.OrderId,x.OrderItemId,x.CreatedAtUtc}).ToArrayAsync();
        var payments=await billing.Payments.AsNoTracking().Where(x=>x.DiningSessionId==session.DiningSessionId).Select(x=>new{x.PaymentId,x.Status}).ToArrayAsync();
        var notificationCount=await customers.ReadyDishNotifications.AsNoTracking().CountAsync(x=>ids.Contains(x.OrderId));
        var table=await catalog.DiningTables.AsNoTracking().Where(x=>x.TableID==session.TableId).Select(x=>new{x.TableID,x.Status.StatusCode,x.CurrentOrderID}).SingleAsync();
        var invoiceCount=await billing.Invoices.CountAsync(x=>x.DiningSessionId==session.DiningSessionId);
        Console.WriteLine("RECOVERY_STATUS: "+JsonSerializer.Serialize(new {sessionId=session.DiningSessionId,session.Status,
            orderIds=ids,tableId=table.TableID,tableStatus=table.StatusCode,tableOrder=table.CurrentOrderID,
            session.BranchId,session.StartedAtUtc,session.UpdatedAtUtc,session.PaymentRequestedAtUtc,session.EndedAtUtc,
            orderStates,itemStates,auditTimes,InvoiceId=invoice?.InvoiceId,TotalAmount=invoice?.TotalAmount,invoiceCount,payments,
            settled=invoice?.SettlementCompletedAtUtc.HasValue??false,RecoveryAttemptCount=invoice?.RecoveryAttemptCount,
            pendingReady=pending.Count(x=>x.Status=="PENDING"),readyEvents=pending.Length,notificationCount}));
    }
}
