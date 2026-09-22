using System.Text.Json;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;
using Microsoft.AspNetCore.Http;

namespace SelfRestaurant.Identity.Api.Infrastructure.Auditing;

public sealed class BusinessAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IdentityDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BusinessAuditLogger(IdentityDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Add(
        string actionType,
        string entityType,
        string entityId,
        object? beforeState = null,
        object? afterState = null,
        string? notes = null,
        int? customerId = null,
        int? employeeId = null,
        string? actorType = null,
        int? actorId = null,
        string? actorCode = null,
        string? actorName = null,
        string? actorRoleCode = null,
        string? correlationId = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
        var requestCorrelationId = correlationId ?? httpContext?.Request?.Headers["X-Correlation-ID"].ToString();

        _db.BusinessAuditLogs.Add(new BusinessAuditLogs
        {
            CreatedAtUtc = DateTime.UtcNow,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            ActorType = actorType,
            ActorId = actorId,
            ActorCode = actorCode,
            ActorName = actorName,
            ActorRoleCode = actorRoleCode,
            CustomerId = customerId,
            EmployeeId = employeeId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = requestCorrelationId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BeforeState = Serialize(beforeState),
            AfterState = Serialize(afterState)
        });
    }

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
