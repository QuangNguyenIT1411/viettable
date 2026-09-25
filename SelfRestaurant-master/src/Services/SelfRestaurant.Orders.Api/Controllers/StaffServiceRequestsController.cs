using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;
using SelfRestaurant.Orders.Api.Domain.ServiceRequests;

namespace SelfRestaurant.Orders.Api.Controllers;

[ApiController]
[Route("api/internal/service-requests")]
public sealed class StaffServiceRequestsController(OrdersDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffServiceRequestDto>>> List([FromQuery] int branchId, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest();
        var rows = await db.ServiceRequests.AsNoTracking()
            .Where(x => x.BranchId == branchId && (x.Status == "OPEN" || x.Status == "ACKNOWLEDGED"))
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return Ok(rows.Select(Map).ToArray());
    }

    [HttpPost("{requestId:int}/acknowledge")]
    public Task<ActionResult<StaffServiceRequestDto>> Acknowledge(int requestId, StaffServiceRequestCommand command, CancellationToken ct)
        => Transition(requestId, command, "OPEN", "ACKNOWLEDGED", ct);

    [HttpPost("{requestId:int}/complete")]
    public Task<ActionResult<StaffServiceRequestDto>> Complete(int requestId, StaffServiceRequestCommand command, CancellationToken ct)
        => Transition(requestId, command, "ACKNOWLEDGED", "COMPLETED", ct);

    private async Task<ActionResult<StaffServiceRequestDto>> Transition(int id, StaffServiceRequestCommand command, string from, string to, CancellationToken ct)
    {
        if (command.BranchId <= 0 || command.EmployeeId <= 0) return BadRequest();
        var row = await db.ServiceRequests.FirstOrDefaultAsync(x => x.ServiceRequestId == id, ct);
        if (row is null) return NotFound();
        if (row.BranchId != command.BranchId) return StatusCode(403, new { code = "forbidden", message = "Yêu cầu thuộc chi nhánh khác." });
        var session = await db.DiningSessions.AsNoTracking().FirstOrDefaultAsync(x => x.DiningSessionId == row.DiningSessionId, ct);
        if (session is null || session.BranchId != row.BranchId || session.TableId != row.TableId || session.CustomerId != row.CustomerId)
            return Conflict(new { code = "service_request_context_invalid", message = "Thông tin phiên của yêu cầu không còn hợp lệ." });
        if (session.EndedAtUtc is not null) return Conflict(new { code = "session_closed", message = "Phiên phục vụ đã kết thúc." });
        if (!ServiceRequestStateMachine.CanTransition(row.Status, to) || !string.Equals(row.Status, from, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { code = "service_request_state_conflict", message = $"Chỉ có thể chuyển {from} sang {to}." });
        row.Status = to;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    private static StaffServiceRequestDto Map(ServiceRequest x) => new(x.ServiceRequestId, x.DiningSessionId,
        x.BranchId, x.TableId, x.CustomerId, x.RequestType, x.Note, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc);

    public sealed record StaffServiceRequestCommand(int BranchId, int EmployeeId);
    public sealed record StaffServiceRequestDto(int ServiceRequestId, int DiningSessionId, int BranchId,
        int TableId, int CustomerId, string RequestType, string? Note, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
}
