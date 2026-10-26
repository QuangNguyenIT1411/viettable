using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Route("api/mobile/v1")]
public sealed class MobileCatalogGatewayController : ControllerBase
{
    private readonly CatalogClient _catalog;
    private readonly CustomersClient _customers;

    public MobileCatalogGatewayController(CatalogClient catalog, CustomersClient customers)
    {
        _catalog = catalog;
        _customers = customers;
    }

    [HttpGet("branches")]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches(CancellationToken ct)
        => Ok(await _catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>());

    [HttpGet("branches/{branchId:int}")]
    public async Task<ActionResult<MobileBranchDetailResponse>> GetBranch(int branchId, CancellationToken ct)
    {
        var branch = (await _catalog.GetBranchesAsync(ct))?.FirstOrDefault(x => x.BranchId == branchId);
        var tables = await _catalog.GetBranchTablesAsync(branchId, ct);
        if (branch is null || tables is null) return MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404);
        var active = tables.Tables.Where(x => x.NumberOfSeats > 0).ToArray();
        return Ok(new MobileBranchDetailResponse(branch.BranchId, branch.Name, branch.Location,
            active.Length, active.Length == 0 ? 0 : active.Min(x => x.NumberOfSeats),
            active.Length == 0 ? 0 : active.Max(x => x.NumberOfSeats), branch.Image));
    }

    [HttpGet("branches/nearby")]
    public async Task<ActionResult<IReadOnlyList<MobileNearbyBranchResponse>>> GetNearbyBranches(
        [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken ct)
    {
        var branches = await _catalog.GetBranchesAsync(ct) ?? Array.Empty<BranchDto>();
        // Current source data stores postal addresses but no coordinates. Keep distance nullable
        // instead of fabricating geometry; coordinates can be added without breaking v1.
        return Ok(branches.Select(x => new MobileNearbyBranchResponse(x.BranchId, x.Name, x.Location, null, x.Image)).ToArray());
    }

    [HttpGet("branches/{branchId:int}/areas")]
    public async Task<ActionResult<IReadOnlyList<MobileAreaResponse>>> GetAreas(int branchId, CancellationToken ct)
    {
        var tables = await _catalog.GetBranchTablesAsync(branchId, ct);
        return tables is null
            ? MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404)
            : Ok(new[] { new MobileAreaResponse("GENERAL", "Khu vực chung", tables.Tables.Count) });
    }

    [HttpGet("branches/{branchId:int}/table-layout")]
    public async Task<ActionResult<MobileTableLayoutResponse>> GetTableLayout(
        int branchId, [FromQuery] string? areaCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(areaCode) && !string.Equals(areaCode, "GENERAL", StringComparison.OrdinalIgnoreCase))
            return MobileError("area_not_found", "Khu vực không tồn tại.", 404);
        var tables = await _catalog.GetBranchTablesAsync(branchId, ct);
        if (tables is null) return MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404);
        return Ok(new MobileTableLayoutResponse(branchId, tables.BranchName, "GENERAL",
            tables.Tables.Select(x => MapTable(x, x.IsAvailable)).ToArray()));
    }

    [HttpGet("branches/{branchId:int}/menu")]
    public async Task<ActionResult<MenuResponse>> GetMenu(int branchId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var menu = await _catalog.GetMenuAsync(branchId, date, ct);
        return menu is null
            ? MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404)
            : Ok(menu);
    }

    [HttpGet("branches/{branchId:int}/availability")]
    public async Task<ActionResult<MobileAvailabilityResponse>> GetAvailability(
        int branchId, [FromQuery] DateTimeOffset reservedAt, [FromQuery] int partySize,
        [FromQuery] int durationMinutes = 120, [FromQuery] int? excludeReservationId = null,
        CancellationToken ct = default)
    {
        if (partySize is < 1 or > 30 || durationMinutes is < 30 or > 360 || reservedAt <= DateTimeOffset.UtcNow.AddMinutes(-5))
            return MobileError("validation_failed", "Thời gian, số khách hoặc thời lượng không hợp lệ.", 400);
        var response = await BuildAvailability(branchId, reservedAt, partySize, durationMinutes, excludeReservationId, ct);
        return response is null
            ? MobileError("branch_not_found", "Không tìm thấy chi nhánh.", 404)
            : Ok(response);
    }

    [HttpGet("qr/{code}")]
    public async Task<ActionResult<MobileQrContextResponse>> GetQr(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return MobileError("invalid_qr", "Mã QR không hợp lệ.", 400);
        var table = await _catalog.GetTableByQrAsync(code.Trim(), ct);
        return table is null
            ? MobileError("qr_not_found", "Không tìm thấy bàn từ mã QR.", 404)
            : Ok(new MobileQrContextResponse(table.TableId, table.BranchId, table.DisplayTableNumber));
    }

    private async Task<MobileAvailabilityResponse?> BuildAvailability(int branchId, DateTimeOffset at, int partySize,
        int duration, int? excludeReservationId, CancellationToken ct)
    {
        var layout = await _catalog.GetBranchTablesAsync(branchId, ct);
        if (layout is null) return null;
        var occupied = (await _customers.GetOccupiedTableIdsAsync(branchId, at.UtcDateTime,
            at.AddMinutes(duration).UtcDateTime, excludeReservationId, ct)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        // Seated guests now only matter for a booking that starts soon (ReservationRules.LiveStatusHorizon).
        var suitable = layout.Tables.Where(x => x.NumberOfSeats >= partySize && ReservationRules.TableBookableAt(x, at, now)
            && !occupied.Contains(x.TableId)).ToArray();
        var suggestions = new List<MobileAvailabilitySuggestion>();
        if (suitable.Length == 0)
        {
            foreach (var offset in new[] { -60, -30, 30, 60 })
            {
                var candidate = at.AddMinutes(offset);
                if (candidate <= DateTimeOffset.UtcNow.AddMinutes(-5)) continue;
                var taken = (await _customers.GetOccupiedTableIdsAsync(branchId, candidate.UtcDateTime,
                    candidate.AddMinutes(duration).UtcDateTime, excludeReservationId, ct)).ToHashSet();
                var count = layout.Tables.Count(x => x.NumberOfSeats >= partySize && ReservationRules.TableBookableAt(x, candidate, now)
                    && !taken.Contains(x.TableId));
                if (count > 0) suggestions.Add(new MobileAvailabilitySuggestion(candidate, count));
            }
        }
        // The whole floor for the map; "availableTables" stays for app versions that only list free tables.
        var map = layout.Tables
            .OrderBy(x => x.DisplayTableNumber > 0 ? x.DisplayTableNumber : x.TableId).ThenBy(x => x.TableId)
            .Select(x =>
            {
                var state = ReservationRules.TableMapStatus(x, partySize, occupied.Contains(x.TableId), at, now);
                return new MobileTableMapItem(x.TableId, x.DisplayTableNumber, x.NumberOfSeats, state.Status, state.Label);
            })
            .ToArray();
        return new MobileAvailabilityResponse(branchId, at, partySize, duration,
            suitable.Select(x => MapTable(x, true)).ToArray(), suggestions, map);
    }

    private static MobileTableResponse MapTable(BranchTableDto table, bool reservationAvailable) =>
        new(table.TableId, table.DisplayTableNumber, table.NumberOfSeats, "GENERAL", table.IsAvailable, reservationAvailable);

    private ObjectResult MobileError(string code, string message, int status, object? details = null)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, details));
    }
}
