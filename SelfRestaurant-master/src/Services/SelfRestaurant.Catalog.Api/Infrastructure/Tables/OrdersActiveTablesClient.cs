using System.Net.Http.Json;

namespace SelfRestaurant.Catalog.Api.Infrastructure.Tables;

// Only the Orders owner can tell whether an occupied table has an active session.
public sealed class OrdersActiveTablesClient(HttpClient http)
{
    public async Task<HashSet<int>> GetAsync(CancellationToken ct) =>
        (await http.GetFromJsonAsync<int[]>("/api/internal/dining-sessions/active-table-ids", ct) ?? [])
            .ToHashSet();
}
