using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

public sealed class PostgresReservationConcurrencyTests
{
    [Fact]
    [Trait("Category", "PostgresWorkflow")]
    public async Task Competing_reservations_cannot_hold_the_same_table_slot()
    {
        using var catalog = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:6101") };
        using var customers = new HttpClient { BaseAddress = new Uri(
            Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_CUSTOMERS_BASE_URL") ?? "http://127.0.0.1:6103") };
        var branches = await catalog.GetFromJsonAsync<JsonElement>("/api/branches");
        var branchId = branches[0].GetProperty("branchId").GetInt32();
        var layout = await catalog.GetFromJsonAsync<JsonElement>($"/api/branches/{branchId}/tables");
        var tableId = layout.GetProperty("tables")[0].GetProperty("tableId").GetInt32();
        var marker = "PG-RACE-" + Guid.NewGuid().ToString("N");
        var connection = Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Disposable test connection required.");
        await using var db = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
        var latest = await db.Reservations.Where(x => x.TableId == tableId)
            .MaxAsync(x => (DateTime?)x.ReservedAt);
        var at = (latest is not null && latest > DateTime.UtcNow.AddDays(4)
            ? latest.Value : DateTime.UtcNow.AddDays(4)).AddHours(3);
        object Body(string key) => new { customerName = "Disposable concurrency test", phoneNumber = "0900000000",
            branchId, tableId, partySize = 2, reservedAt = at, idempotencyKey = key,
            requestHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") };
        using var firstBody = JsonContent.Create(Body(marker + "a"));
        using var secondBody = JsonContent.Create(Body(marker + "b"));
        var responses = await Task.WhenAll(customers.PostAsync("/api/customers/reservations", firstBody),
            customers.PostAsync("/api/customers/reservations", secondBody));
        try
        {
            Assert.Single(responses, x => x.IsSuccessStatusCode);
            Assert.True(responses.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1,
                "Competing response statuses: " + string.Join(",", responses.Select(x => (int)x.StatusCode)));
            Assert.Single(await db.Reservations.Where(x => x.IdempotencyKey != null && x.IdempotencyKey.StartsWith(marker)).ToListAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
