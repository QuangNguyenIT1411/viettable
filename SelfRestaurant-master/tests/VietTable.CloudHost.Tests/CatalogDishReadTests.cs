using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class CatalogDishReadTests
{
    [Fact]
    public async Task Live_batch_does_not_read_fallback_before_http_and_persists_authoritative_values()
    {
        var queries = new QueryCounter();
        await using var db = Database(queries);
        db.CatalogDishSnapshots.Add(new CatalogDishSnapshots { DishId = 7, Name = "Old", Price = 1 });
        await db.SaveChangesAsync();
        using var handler = new Handler(() => {
            Assert.Equal(0, queries.Count);
            return JsonContent.Create(new[] { new CatalogApiClient.DishSnapshotResponse(7, "Live", 1, "Category", 35000, "Plate", null, true, false) });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.fixture.invalid") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CatalogApiClient(http, cache, db);

        var result = await client.GetDishesAsync(new[] { 7, 7, -1 }, CancellationToken.None);

        var dish = Assert.Single(result!);
        Assert.Equal("Live", dish.Name);
        Assert.False(dish.Available);
        Assert.Equal(1, queries.Count); // Only the tracked upsert query; no eager fallback query.
        Assert.Equal(1, handler.Calls);
        Assert.Equal("/api/internal/dishes:batch?ids=7", handler.Path);
        db.ChangeTracker.Clear();
        var persisted = await db.CatalogDishSnapshots.SingleAsync();
        Assert.Equal(35000, persisted.Price);
        Assert.False(persisted.Available);
    }

    [Fact]
    public async Task Failed_catalog_reads_only_requested_local_snapshots_after_http()
    {
        var queries = new QueryCounter();
        await using var db = Database(queries);
        db.CatalogDishSnapshots.AddRange(
            new CatalogDishSnapshots { DishId = 7, Name = "Fallback", Price = 35000 },
            new CatalogDishSnapshots { DishId = 8, Name = "Other", Price = 1 });
        await db.SaveChangesAsync();
        using var handler = new Handler(() => {
            Assert.Equal(0, queries.Count);
            throw new HttpRequestException("Fixture unavailable");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.fixture.invalid") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new CatalogApiClient(http, cache, db).GetDishesAsync(new[] { 7 }, CancellationToken.None);
        Assert.Equal("Fallback", Assert.Single(result!).Name);
        Assert.Equal(1, queries.Count);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Empty_ids_perform_no_http_or_database_read()
    {
        var queries = new QueryCounter();
        await using var db = Database(queries);
        using var handler = new Handler(() => throw new InvalidOperationException("Must not call Catalog"));
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new CatalogApiClient(http, cache, db).GetDishesAsync(new[] { 0, -1 }, CancellationToken.None);
        Assert.Empty(result!);
        Assert.Equal(0, queries.Count);
        Assert.Equal(0, handler.Calls);
    }

    private static OrdersDbContext Database(QueryCounter queries) => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).EnableServiceProviderCaching(false).AddInterceptors(queries).Options);

    private sealed class QueryCounter : IQueryExpressionInterceptor
    {
        public int Count { get; private set; }
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        { Count++; return queryExpression; }
    }

    private sealed class Handler(Func<HttpContent> content) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content() });
        }
    }
}
