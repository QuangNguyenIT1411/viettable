using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// Which tables of a branch can take a party at a time: bookable now, enough seats, not held by another booking in
/// the 2-hour window. When no single table fits, the best group of pushed-together tables (owner 06/11). One place
/// for booking, the "Hết bàn" suggestions and the waitlist.
/// </summary>
public sealed class TableAvailability(CatalogClient catalog, CustomersClient customers)
{
    /// <summary>Available: single tables that fit. Combination: best group when none does (else null).</summary>
    public sealed record Result(BranchTablesResponse? Layout, IReadOnlyList<BranchTableDto> Available,
        IReadOnlyList<BranchTableDto>? Combination = null, IReadOnlyList<BranchTableDto>? Free = null)
    {
        /// <summary>Ways to seat the party: the single tables, or 1 when only a group fits.</summary>
        public int OptionCount => Available.Count > 0 ? Available.Count : Combination is null ? 0 : 1;
    }

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
        var free = layout.Tables.Where(x => ReservationRules.TableBookableAt(x, at, now) && !occupied.Contains(x.TableId)).ToArray();
        var single = free.Where(x => x.NumberOfSeats >= partySize).ToArray();
        return new Result(layout, single, single.Length == 0 ? TableCombiner.Find(free, partySize) : null, free);
    }

    /// <summary>The smallest table that fits (ties: lowest id), or null.</summary>
    public static BranchTableDto? Best(IEnumerable<BranchTableDto> available) =>
        available.OrderBy(x => x.NumberOfSeats).ThenBy(x => x.TableId).FirstOrDefault();

    /// <summary>Main table and the others of the best seating: a single table, a group, or nothing.</summary>
    public static (int TableId, IReadOnlyList<int> Others)? Seating(Result result)
    {
        var single = Best(result.Available);
        if (single is not null) return (single.TableId, Array.Empty<int>());
        if (result.Combination is { Count: > 1 } group)
        {
            var ordered = group.OrderBy(x => x.DisplayTableNumber).ToArray();
            return (ordered[0].TableId, ordered.Skip(1).Select(x => x.TableId).ToArray());
        }
        return null;
    }
}
