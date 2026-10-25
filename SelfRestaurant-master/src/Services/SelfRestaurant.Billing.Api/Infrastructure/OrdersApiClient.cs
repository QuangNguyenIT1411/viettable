using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure;

public sealed class OrdersApiClient
{
    private readonly HttpClient _http;
    private readonly BillingDbContext _db;
    private static readonly TimeSpan SnapshotFreshnessTtl = TimeSpan.FromHours(12);

    public OrdersApiClient(HttpClient http, BillingDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<IReadOnlyList<CashierOrderAggregateResponse>> GetCashierOrdersAsync(int branchId, CancellationToken cancellationToken)
    {
        var list = await _http.GetFromJsonAsync<IReadOnlyList<CashierOrderAggregateResponse>>(
            $"/api/internal/branches/{branchId}/cashier/orders",
            cancellationToken);

        var materialized = list ?? Array.Empty<CashierOrderAggregateResponse>();
        await UpsertSnapshotsFromCashierQueueAsync(branchId, materialized, cancellationToken);
        return materialized;
    }

    public async Task<CheckoutContextResponse?> GetCheckoutContextAsync(int orderId, CancellationToken cancellationToken)
    {
        var response = await _http.GetFromJsonAsync<CheckoutContextResponse>($"/api/internal/orders/{orderId}/checkout-context", cancellationToken);
        if (response is not null)
        {
            await UpsertSnapshotsAsync(
                new[] { new OrderBillContextResponse(response.OrderId, response.OrderCode, response.TableId, response.TableName ?? "-", response.BranchId, response.BranchName) },
                cancellationToken);
        }

        return response;
    }

    public Task<DiningSessionCheckoutContext?> GetDiningSessionCheckoutContextAsync(int sessionId, int customerId, CancellationToken ct) =>
        _http.GetFromJsonAsync<DiningSessionCheckoutContext>($"/api/internal/dining-sessions/{sessionId}/checkout-context?customerId={customerId}", ct);

    /// <summary>Null when the session does not exist. Network/server failures throw (callers must not confirm then).</summary>
    public async Task<SessionPaymentState?> GetPaymentStateAsync(int sessionId, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/api/internal/dining-sessions/{sessionId}/payment-state", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionPaymentState>(cancellationToken: ct);
    }

    public sealed record SessionPaymentState(int DiningSessionId, string Status, int? CheckoutQuoteId, int? PaymentId, bool Ended);

    public async Task RequestPaymentAsync(int sessionId, int customerId, int quoteId, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/dining-sessions/{sessionId}/payment-request", new { customerId, checkoutQuoteId = quoteId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelPaymentRequestAsync(int sessionId, int customerId, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/dining-sessions/{sessionId}/cancel-payment-request", new { customerId }, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Unlocks the session only if it waits for exactly this quote; an already unlocked session is fine (retry).</summary>
    public async Task CancelPaymentRequestForQuoteAsync(int sessionId, int customerId, int quoteId, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/dining-sessions/{sessionId}/cancel-payment-request",
            new { customerId, checkoutQuoteId = quoteId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompletePaymentAsync(int sessionId, int customerId, int quoteId, int paymentId, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"/api/internal/dining-sessions/{sessionId}/payment-succeeded", new { customerId, checkoutQuoteId = quoteId, paymentId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<OrderBillContextResponse>> GetOrderBillContextsAsync(IEnumerable<int> orderIds, CancellationToken cancellationToken)
    {
        var ids = orderIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<OrderBillContextResponse>();
        }

        var cached = await _db.OrderContextSnapshots
            .AsNoTracking()
            .Where(x => ids.Contains(x.OrderId))
            .ToListAsync(cancellationToken);

        var freshLookup = cached
            .Where(x => IsFresh(x.RefreshedAtUtc))
            .ToDictionary(x => x.OrderId, Map);

        var missingIds = ids.Where(id => !freshLookup.ContainsKey(id)).ToArray();
        if (missingIds.Length == 0)
        {
            return ids.Where(freshLookup.ContainsKey).Select(id => freshLookup[id]).ToArray();
        }

        try
        {
            var query = string.Join("&", missingIds.Select(id => $"ids={id}"));
            var remote = await _http.GetFromJsonAsync<IReadOnlyList<OrderBillContextResponse>>(
                $"/api/internal/orders:bill-context?{query}",
                cancellationToken);

            if (remote is not null)
            {
                await UpsertSnapshotsAsync(remote, cancellationToken);
                foreach (var item in remote)
                {
                    freshLookup[item.OrderId] = item;
                }
            }
        }
        catch
        {
        }

        foreach (var stale in cached.Where(x => !freshLookup.ContainsKey(x.OrderId)))
        {
            freshLookup[stale.OrderId] = Map(stale);
        }

        return ids.Where(freshLookup.ContainsKey).Select(id => freshLookup[id]).ToArray();
    }

    private async Task UpsertSnapshotsFromCashierQueueAsync(
        int branchId,
        IEnumerable<CashierOrderAggregateResponse> orders,
        CancellationToken cancellationToken)
    {
        var list = orders
            .Select(x => new OrderBillContextResponse(
                x.OrderId,
                x.OrderCode,
                x.TableId,
                x.TableName,
                branchId,
                null))
            .ToArray();

        await UpsertSnapshotsAsync(list, cancellationToken);
    }

    private async Task UpsertSnapshotsAsync(
        IEnumerable<OrderBillContextResponse> snapshots,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var list = snapshots.ToList();
        var ids = list.Select(x => x.OrderId).Distinct().ToArray();
        var existing = await _db.OrderContextSnapshots
            .Where(x => ids.Contains(x.OrderId))
            .ToDictionaryAsync(x => x.OrderId, cancellationToken);

        foreach (var item in list)
        {
            if (!existing.TryGetValue(item.OrderId, out var entity))
            {
                entity = new OrderContextSnapshots { OrderId = item.OrderId };
                _db.OrderContextSnapshots.Add(entity);
            }

            entity.OrderCode = item.OrderCode;
            entity.TableId = item.TableId;
            entity.TableName = item.TableName;
            entity.BranchId = item.BranchId;
            entity.BranchName = item.BranchName;
            entity.RefreshedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsFresh(DateTime refreshedAtUtc)
        => refreshedAtUtc >= DateTime.UtcNow.Subtract(SnapshotFreshnessTtl);

    private static OrderBillContextResponse Map(OrderContextSnapshots snapshot)
        => new(snapshot.OrderId, snapshot.OrderCode, snapshot.TableId, snapshot.TableName ?? "-", snapshot.BranchId, snapshot.BranchName);

    public sealed record OrderBillContextResponse(
        int OrderId,
        string? OrderCode,
        int? TableId,
        string TableName,
        int? BranchId,
        string? BranchName);

    public sealed record DiningSessionCheckoutItem(int ItemId, int OrderId, int DishId, string DishName,
        int Quantity, decimal UnitPrice, decimal LineTotal);
    public sealed record DiningSessionCheckoutContext(int DiningSessionId, string DiningSessionCode, int CustomerId,
        int BranchId, int TableId, string Status, IReadOnlyList<int> OrderIds, decimal Subtotal,
        IReadOnlyList<DiningSessionCheckoutItem> Items);
}
