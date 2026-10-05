using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SelfRestaurant.CloudCompatibility;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

public sealed class PostgresConcurrencyFilterTests
{
    [Theory]
    [InlineData("PK_CatalogDishSnapshots", true)]
    [InlineData("PK_Customers", false)]
    public void Unique_conflict_mapping_is_limited_to_snapshot_refresh(string constraint, bool expected)
    {
        var exception = new PostgresException("test", "ERROR", "ERROR", "23505", constraintName: constraint);
        Assert.Equal(expected, PostgresConcurrencyFilter.IsConflict(exception));
    }

    [Theory]
    [InlineData("40001", true)]
    [InlineData("40P01", true)]
    [InlineData("23505", false)]
    public void Only_transaction_conflicts_are_mapped(string sqlState, bool expected)
    {
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(action, Array.Empty<IFilterMetadata>()) {
            Exception = new InvalidOperationException("wrapped", new PostgresException("test", "ERROR", "ERROR", sqlState)) };
        new PostgresConcurrencyFilter().OnException(context);
        Assert.Equal(expected, context.ExceptionHandled);
        if (expected) Assert.Equal(409, Assert.IsType<ConflictObjectResult>(context.Result).StatusCode);
        else Assert.Null(context.Result);
    }
}
