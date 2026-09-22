using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;

namespace SelfRestaurant.Billing.Api.Controllers;

[ApiController]
public sealed class InternalOutboxController : ControllerBase
{
    private readonly BillingDbContext _db;

    public InternalOutboxController(BillingDbContext db)
    {
        _db = db;
    }

    [HttpGet("api/internal/outbox/pending")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetPending(
        [FromQuery] string? eventName,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _db.OutboxEvents
            .AsNoTracking()
            .Where(x => x.Status == "PENDING");

        if (!string.IsNullOrWhiteSpace(eventName))
        {
            query = query.Where(x => x.EventName == eventName);
        }

        var rows = await query
            .OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new
            {
                x.OutboxEventId,
                x.EventName,
                x.OccurredAtUtc,
                x.Source,
                x.CorrelationId,
                x.PayloadJson
            })
            .ToListAsync(cancellationToken);

        var payload = rows.Select(x => new
        {
            x.OutboxEventId,
            x.EventName,
            x.OccurredAtUtc,
            x.Source,
            x.CorrelationId,
            Payload = JsonSerializer.Deserialize<JsonElement>(x.PayloadJson)
        }).ToList();

        return Ok(payload);
    }

    [HttpPost("api/internal/outbox/{outboxEventId:long}/ack")]
    public async Task<ActionResult> Ack(
        long outboxEventId,
        [FromBody] AckOutboxRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.OutboxEvents.FirstOrDefaultAsync(x => x.OutboxEventId == outboxEventId, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        row.Status = "PROCESSED";
        row.ProcessedAtUtc = DateTime.UtcNow;
        row.Error = string.IsNullOrWhiteSpace(request.Consumer) ? null : $"ACK:{request.Consumer}";

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public sealed record AckOutboxRequest(string? Consumer);
}
