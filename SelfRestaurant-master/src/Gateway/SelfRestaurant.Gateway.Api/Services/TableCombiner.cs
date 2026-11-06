using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// Pushed-together tables for a large party (owner 06/11). Only when no single free table fits. A group is valid when
/// its tables are free, in the same (non-empty) area, joined by the admin's "ghép được" links (connected), and seat
/// the party. Best = fewest tables, then fewest empty seats, then lowest table numbers. Pure; unit tested.
/// </summary>
public static class TableCombiner
{
    public const int MaxTables = 4;

    public static IReadOnlyList<BranchTableDto>? Find(IReadOnlyList<BranchTableDto> freeTables, int partySize, int maxTables = MaxTables)
    {
        if (partySize <= 0 || freeTables.Any(x => x.NumberOfSeats >= partySize)) return null;
        IReadOnlyList<BranchTableDto>? best = null;
        foreach (var area in freeTables.Where(x => !string.IsNullOrWhiteSpace(x.Area))
                     .GroupBy(x => x.Area!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var tables = area.OrderBy(x => x.DisplayTableNumber).ThenBy(x => x.TableId).ToArray();
            if (tables.Sum(x => x.NumberOfSeats) < partySize) continue;
            for (var size = 2; size <= Math.Min(maxTables, tables.Length); size++)
            {
                foreach (var combo in Combinations(tables, size))
                {
                    if (combo.Sum(x => x.NumberOfSeats) < partySize || !Connected(combo)) continue;
                    if (best is null || Better(combo, best, partySize)) best = combo;
                }
                if (best is not null && best.Count == size) break; // fewer tables always win
            }
        }
        return best;
    }

    /// <summary>"Bàn 5 + 6".</summary>
    public static string Label(IReadOnlyList<BranchTableDto> group) =>
        "Bàn " + string.Join(" + ", group.OrderBy(x => x.DisplayTableNumber).Select(x => x.DisplayTableNumber));

    /// <summary>True when the guest's chosen tables form a valid group for the party right now.</summary>
    public static bool IsValidGroup(IReadOnlyList<BranchTableDto> group, int partySize) =>
        group.Count is >= 2 and <= MaxTables
        && group.Select(x => x.TableId).Distinct().Count() == group.Count
        && group.All(x => !string.IsNullOrWhiteSpace(x.Area))
        && group.Select(x => x.Area!.Trim().ToLowerInvariant()).Distinct().Count() == 1
        && group.Sum(x => x.NumberOfSeats) >= partySize
        && Connected(group);

    private static bool Better(IReadOnlyList<BranchTableDto> a, IReadOnlyList<BranchTableDto> b, int party)
    {
        if (a.Count != b.Count) return a.Count < b.Count;
        var spareA = a.Sum(x => x.NumberOfSeats) - party;
        var spareB = b.Sum(x => x.NumberOfSeats) - party;
        if (spareA != spareB) return spareA < spareB;
        var numbersA = a.Select(x => x.DisplayTableNumber).OrderBy(x => x).ToArray();
        var numbersB = b.Select(x => x.DisplayTableNumber).OrderBy(x => x).ToArray();
        for (var i = 0; i < numbersA.Length; i++)
            if (numbersA[i] != numbersB[i]) return numbersA[i] < numbersB[i];
        return false;
    }

    /// <summary>Every table reaches every other through "ghép được" links inside the group.</summary>
    private static bool Connected(IReadOnlyList<BranchTableDto> group)
    {
        var ids = group.Select(x => x.TableId).ToHashSet();
        var seen = new HashSet<int> { group[0].TableId };
        var queue = new Queue<BranchTableDto>(new[] { group[0] });
        while (queue.Count > 0)
        {
            var table = queue.Dequeue();
            foreach (var next in group)
            {
                if (seen.Contains(next.TableId)) continue;
                var linked = (table.LinkedTableIds?.Contains(next.TableId) ?? false) || (next.LinkedTableIds?.Contains(table.TableId) ?? false);
                if (!linked || !ids.Contains(next.TableId)) continue;
                seen.Add(next.TableId);
                queue.Enqueue(next);
            }
        }
        return seen.Count == group.Count;
    }

    private static IEnumerable<IReadOnlyList<BranchTableDto>> Combinations(IReadOnlyList<BranchTableDto> items, int size)
    {
        var index = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return index.Select(i => items[i]).ToArray();
            var k = size - 1;
            while (k >= 0 && index[k] == items.Count - size + k) k--;
            if (k < 0) yield break;
            index[k]++;
            for (var j = k + 1; j < size; j++) index[j] = index[j - 1] + 1;
        }
    }
}
