using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>
/// Which bookings hold which tables (owner 06/11: a large party can hold several pushed-together tables). A booking
/// holds its main table and every table in ReservationTables, for 2 hours from its time, unless it is closed, an
/// unpaid deposit past its deadline, or a waitlist hold nobody accepted in time.
/// </summary>
public static class ReservationOccupancy
{
    public const int HoldMinutes = 120;

    private static readonly string[] Closed = { "Cancelled", "NoShow", "CheckedIn", "Completed", "Rejected" };

    /// <summary>Open bookings of the branch overlapping [fromUtc, toUtc).</summary>
    public static IQueryable<Reservation> Overlapping(IQueryable<Reservation> reservations, int branchId, DateTime fromUtc, DateTime toUtc, DateTime nowUtc) =>
        reservations.Where(x => x.BranchId == branchId
            && x.ReservedAt < toUtc && x.ReservedAt.AddMinutes(HoldMinutes) > fromUtc
            && !Closed.Contains(x.Status)
            && !(x.Status == "PendingDeposit" && x.DepositDueAtUtc < nowUtc)
            && !(x.Status == WaitlistRules.HoldStatus && x.HoldExpiresAtUtc < nowUtc));

    /// <summary>Tables held by open bookings overlapping the window (main tables and pushed-together ones).</summary>
    public static async Task<IReadOnlyList<int>> HeldTableIdsAsync(CustomersDbContext db, int branchId, DateTime fromUtc, DateTime toUtc,
        int? excludeReservationId, DateTime nowUtc, CancellationToken ct)
    {
        var query = Overlapping(db.Reservations.AsNoTracking(), branchId, fromUtc, toUtc, nowUtc);
        if (excludeReservationId is > 0) query = query.Where(x => x.ReservationId != excludeReservationId.Value);
        var main = await query.Where(x => x.TableId != null).Select(x => x.TableId!.Value).ToListAsync(ct);
        var grouped = await query.SelectMany(x => x.ReservationTables.Select(t => t.TableId)).ToListAsync(ct);
        return main.Concat(grouped).Distinct().ToArray();
    }

    /// <summary>True when any of the tables is held by another open booking for a booking at <paramref name="reservedAtUtc"/>.</summary>
    public static async Task<bool> AnyTakenAsync(CustomersDbContext db, int branchId, IReadOnlyCollection<int> tableIds, DateTime reservedAtUtc,
        int? excludeReservationId, DateTime nowUtc, CancellationToken ct)
    {
        if (tableIds.Count == 0) return false;
        var held = await HeldTableIdsAsync(db, branchId, reservedAtUtc, reservedAtUtc.AddMinutes(HoldMinutes), excludeReservationId, nowUtc, ct);
        return tableIds.Any(held.Contains);
    }

    /// <summary>The main table first, then the others, no duplicates, no blanks.</summary>
    public static IReadOnlyList<int> Group(int? mainTableId, IEnumerable<int>? additional)
    {
        var result = new List<int>();
        if (mainTableId is > 0) result.Add(mainTableId.Value);
        foreach (var id in additional ?? Array.Empty<int>())
            if (id > 0 && !result.Contains(id)) result.Add(id);
        return result;
    }

    /// <summary>Rows of ReservationTables for a group of two or more tables (a single table needs none).</summary>
    public static void SetGroup(Reservation reservation, IReadOnlyList<int> group, DateTime nowUtc)
    {
        reservation.ReservationTables.Clear();
        if (group.Count == 0) return;
        for (var i = 0; i < group.Count; i++)
            reservation.ReservationTables.Add(new ReservationTable { TableId = group[i], IsPrimary = i == 0, CreatedAtUtc = nowUtc });
    }
}
