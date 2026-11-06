using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Infrastructure;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api.Controllers;

/// <summary>Admin edits a branch's address and map position; the app shows "Chỉ đường" only when both are set.</summary>
[ApiController]
public sealed class BranchLocationController : ControllerBase
{
    private readonly CatalogDbContext _db;

    public BranchLocationController(CatalogDbContext db) => _db = db;

    public sealed record BranchLocationRequest(string? Address, decimal? Latitude, decimal? Longitude);

    [HttpPut("api/admin/branches/{branchId:int}/location")]
    public async Task<ActionResult<object>> SaveLocation(int branchId, [FromBody] BranchLocationRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Thiếu dữ liệu." });
        var problem = BranchLocationRules.Validate(request.Address, request.Latitude, request.Longitude);
        if (problem is not null) return BadRequest(new { code = "validation_failed", message = problem });
        var branch = await _db.Branches.FirstOrDefaultAsync(x => x.BranchID == branchId, ct);
        if (branch is null) return NotFound(new { code = "branch_not_found", message = "Không tìm thấy chi nhánh." });
        var address = request.Address?.Trim();
        branch.Location = string.IsNullOrEmpty(address) ? null : address;
        branch.Latitude = request.Latitude;
        branch.Longitude = request.Longitude;
        branch.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return Ok(new { branchId = branch.BranchID, name = branch.Name, location = branch.Location,
            image = branch.Image, latitude = branch.Latitude, longitude = branch.Longitude });
    }
}
