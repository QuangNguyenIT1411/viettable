using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Catalog.Api.Controllers;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11 large parties: pushed-together tables of one area, fewest tables then fewest empty seats.</summary>
public sealed class TableCombinerTests
{
    private static BranchTableDto T(int id, int seats, string? area, params int[] links) =>
        new(id, 1, id, seats, null, "AVAILABLE", "Trống", true, area, links);

    [Fact]
    public void No_group_when_a_single_table_seats_the_party()
    {
        Assert.Null(TableCombiner.Find(new[] { T(1, 4, "A", 2), T(2, 4, "A", 1), T(3, 8, "A") }, 6));
    }

    [Fact]
    public void Two_linked_tables_of_one_area_seat_a_party_of_eight()
    {
        var group = TableCombiner.Find(new[] { T(5, 4, "Tầng 1", 6), T(6, 4, "Tầng 1", 5, 7), T(7, 6, "Tầng 1", 6) }, 8);
        Assert.NotNull(group);
        Assert.Equal(new[] { 5, 6 }, group!.Select(x => x.TableId).OrderBy(x => x).ToArray()); // 8 seats beats 6+4=10
        Assert.Equal("Bàn 5 + 6", TableCombiner.Label(group));
    }

    [Fact]
    public void Fewest_tables_win_over_fewer_empty_seats()
    {
        // 6+6 (2 tables, 2 spare) vs 4+3+3 (3 tables, 0 spare): two tables win.
        var group = TableCombiner.Find(new[] { T(1, 6, "A", 2), T(2, 6, "A", 1), T(3, 4, "A", 4), T(4, 3, "A", 3, 5), T(5, 3, "A", 4) }, 10);
        Assert.Equal(new[] { 1, 2 }, group!.Select(x => x.TableId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Tables_of_different_areas_or_without_links_are_never_joined()
    {
        Assert.Null(TableCombiner.Find(new[] { T(1, 4, "Tầng 1", 2), T(2, 4, "Sân vườn", 1) }, 8));
        Assert.Null(TableCombiner.Find(new[] { T(1, 4, "A"), T(2, 4, "A") }, 8));
        Assert.Null(TableCombiner.Find(new[] { T(1, 4, null, 2), T(2, 4, null, 1) }, 8));
    }

    [Fact]
    public void A_group_must_be_connected_through_links()
    {
        // 1-2 linked, 3 linked to nothing: 1+2+3 is not a group even though seats add up.
        Assert.Null(TableCombiner.Find(new[] { T(1, 4, "A", 2), T(2, 4, "A", 1), T(3, 4, "A") }, 12));
        // A chain 1-2-3 is.
        var chain = TableCombiner.Find(new[] { T(1, 4, "A", 2), T(2, 4, "A", 1, 3), T(3, 4, "A", 2) }, 12);
        Assert.Equal(3, chain!.Count);
    }

    [Fact]
    public void Guest_choice_is_checked_again()
    {
        Assert.True(TableCombiner.IsValidGroup(new[] { T(5, 4, "A", 6), T(6, 4, "A", 5) }, 8));
        Assert.False(TableCombiner.IsValidGroup(new[] { T(5, 4, "A", 6), T(6, 4, "A", 5) }, 9));
        Assert.False(TableCombiner.IsValidGroup(new[] { T(5, 4, "A"), T(6, 4, "A") }, 8));
        Assert.False(TableCombiner.IsValidGroup(new[] { T(5, 4, "A", 6), T(6, 4, "B", 5) }, 8));
    }

    [Fact]
    public void Links_need_the_same_branch_active_tables_and_the_same_area()
    {
        Assert.Null(TableGroupingRules.Validate(1, "Tầng 1", new[] { (6, 1, (string?)"Tầng 1", true) }, new[] { 6 }));
        Assert.NotNull(TableGroupingRules.Validate(1, "Tầng 1", new[] { (6, 2, (string?)"Tầng 1", true) }, new[] { 6 }));
        Assert.NotNull(TableGroupingRules.Validate(1, "Tầng 1", new[] { (6, 1, (string?)"Sân vườn", true) }, new[] { 6 }));
        Assert.NotNull(TableGroupingRules.Validate(1, "Tầng 1", new[] { (6, 1, (string?)"Tầng 1", false) }, new[] { 6 }));
        Assert.NotNull(TableGroupingRules.Validate(1, "Tầng 1", Array.Empty<(int, int, string?, bool)>(), new[] { 99 }));
    }

    [Fact]
    public async Task A_group_booking_holds_every_table_of_the_group()
    {
        await using var db = new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var controller = new CustomersController(db, null!, null!);
        var at = DateTime.UtcNow.AddDays(1);
        var created = await controller.CreateReservation(new CustomersController.CreateReservationRequest("Nhóm", "0900000009", 21, 1, 5, 8, at,
            null, "group-1", "h", AdditionalTableIds: new[] { 6 }), CancellationToken.None);
        Assert.IsNotType<ConflictObjectResult>(created.Result);
        var booking = await db.Reservations.Include(x => x.ReservationTables).SingleAsync();
        Assert.Equal(new[] { 5, 6 }, booking.ReservationTables.Select(x => x.TableId).OrderBy(x => x).ToArray());

        var occupied = await controller.GetOccupiedTableIds(1, at, at.AddHours(2), null, CancellationToken.None);
        Assert.Equal(new[] { 5, 6 }, Assert.IsAssignableFrom<IReadOnlyList<int>>(Assert.IsType<OkObjectResult>(occupied.Result).Value).OrderBy(x => x).ToArray());

        // Table 6 alone, at the same time, is taken.
        var second = await controller.CreateReservation(new CustomersController.CreateReservationRequest("B", "0900000008", 22, 1, 6, 2, at.AddMinutes(30),
            null, "single-6", "h"), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(second.Result);
    }
}
