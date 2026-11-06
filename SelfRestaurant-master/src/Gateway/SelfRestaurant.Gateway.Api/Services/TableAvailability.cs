using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// Which tables of a branch can take a party at a time: bookable now, enough seats, not held by another booking in
/// the 2-hour window. One place for booking, the "Hết bàn" suggestions and the waitlist (owner 06/11).
/// </summary>
public sealed class TableAvailability(CatalogClient catalog, CustomersClient customers)
{
    public sealed record Result(BranchTablesResponse? Layout, IReadOnlyList<BranchTableDto> Available);

    public async Task<Result> FindAsync(int branchId, DateTimeOffset at, int partySize, int? excludeReservationId, CancellationToken ct)
    {
        var layout = await catalog.GetBranchTablesAsync(branchId, ct);
        if (layout is null) return new Result(null, Array.Empty<BranchTableDto>());
        return await FindAsync(layout, branchId, at, partySize, excludeReservationId, ct);
    }

    /// <summary>Same, with a layout already read (the waitlist checks many times of one branch).</summary>
    public async Task<Result> FindAsync(BranchTablesResponse layout, int branchId, DateTimeOffset at, int partySize,
        int? excludeReservationId, CancellationToken ct)
    {
        var occupied = (await customers.GetOccupiedTableIdsAsync(branchId, at.UtcDateTime,
            at.AddHours(2).UtcDateTime, excludeReservationId, ct)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        return new Result(layout, layout.Tables.Where(x => ReservationRules.TableBookableAt(x, at, now)
            && x.NumberOfSeats >= partySize && !occupied.Contains(x.TableId)).ToArray());
    }

    /// <summary>The smallest table that fits (ties: lowest id), or null.</summary>
    public static BranchTableDto? Best(IEnumerable<BranchTableDto> available) =>
        available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();
}
