using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence.Entities;

namespace SelfRestaurant.Catalog.Api.Controllers;

/// <summary>
/// "Khu vực & ghép bàn" (owner 06/11): the admin names each table's area and which tables can be pushed together.
/// A link is kept both ways and only between active tables of the same branch and the same area.
/// </summary>
[ApiController]
public sealed class TableGroupingController : ControllerBase
{
    private readonly CatalogDbContext _db;

    public TableGroupingController(CatalogDbContext db) => _db = db;

    public sealed record TableGroupingRequest(string? Area, IReadOnlyList<int>? LinkedTableIds);

    [HttpGet("api/admin/branches/{branchId:int}/table-grouping")]
    public async Task<ActionResult<IReadOnlyList<object>>> Get(int branchId, CancellationToken ct)
    {
        var tables = await _db.DiningTables.AsNoTracking()
            .Where(x => x.BranchID == branchId && (x.IsActive ?? true))
            .OrderBy(x => x.TableNumber).ThenBy(x => x.TableID)
            .Select(x => new { x.TableID, x.TableNumber, x.NumberOfSeats, x.Area }).ToListAsync(ct);
        var ids = tables.Select(x => x.TableID).ToArray();
        var links = await _db.DiningTableLinks.AsNoTracking().Where(x => ids.Contains(x.TableId)).ToListAsync(ct);
        return Ok(tables.Select(x => (object)new
        {
            tableId = x.TableID, tableNumber = x.TableNumber, numberOfSeats = x.NumberOfSeats, area = x.Area,
            linkedTableIds = links.Where(l => l.TableId == x.TableID && ids.Contains(l.LinkedTableId)).Select(l => l.LinkedTableId).OrderBy(i => i).ToArray()
        }).ToArray());
    }

    [HttpPut("api/admin/tables/{tableId:int}/grouping")]
    public async Task<ActionResult<object>> Save(int tableId, [FromBody] TableGroupingRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Thiếu dữ liệu." });
        var table = await _db.DiningTables.FirstOrDefaultAsync(x => x.TableID == tableId, ct);
        if (table is null) return NotFound(new { code = "table_not_found", message = "Không tìm thấy bàn." });
        var area = string.IsNullOrWhiteSpace(request.Area) ? null : request.Area.Trim();
        if (area is { Length: > 50 }) return BadRequest(new { code = "validation_failed", message = "Tên khu vực tối đa 50 ký tự." });
        var wanted = (request.LinkedTableIds ?? Array.Empty<int>()).Where(x => x > 0 && x != tableId).Distinct().ToArray();
        if (wanted.Length > 0 && area is null)
            return BadRequest(new { code = "validation_failed", message = "Cần đặt khu vực trước khi chọn bàn ghép." });
        var others = await _db.DiningTables.Where(x => wanted.Contains(x.TableID)).ToListAsync(ct);
        var problem = TableGroupingRules.Validate(table.BranchID, area, others.Select(x => (x.TableID, x.BranchID, x.Area, x.IsActive ?? true)).ToArray(), wanted);
        if (problem is not null) return BadRequest(new { code = "validation_failed", message = problem });

        table.Area = area;
        table.UpdatedAt = DateTime.Now;
        var old = await _db.DiningTableLinks.Where(x => x.TableId == tableId || x.LinkedTableId == tableId).ToListAsync(ct);
        _db.DiningTableLinks.RemoveRange(old);
        foreach (var other in wanted)
        {
            _db.DiningTableLinks.Add(new DiningTableLink { TableId = tableId, LinkedTableId = other });
            _db.DiningTableLinks.Add(new DiningTableLink { TableId = other, LinkedTableId = tableId });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { tableId, area, linkedTableIds = wanted.OrderBy(x => x).ToArray() });
    }
}

/// <summary>Checks on a table's links (pure; unit tested).</summary>
public static class TableGroupingRules
{
    public static string? Validate(int branchId, string? area,
        IReadOnlyList<(int TableId, int BranchId, string? Area, bool IsActive)> others, IReadOnlyList<int> wanted)
    {
        if (others.Count != wanted.Count) return "Có bàn ghép không tồn tại.";
        foreach (var other in others)
        {
            if (other.BranchId != branchId) return "Chỉ ghép được bàn cùng chi nhánh.";
            if (!other.IsActive) return "Không ghép với bàn đã ngừng dùng.";
            if (!string.Equals(other.Area?.Trim(), area, StringComparison.OrdinalIgnoreCase))
                return "Chỉ ghép được bàn cùng khu vực. Đặt khu vực cho bàn kia trước.";
        }
        return null;
    }
}
