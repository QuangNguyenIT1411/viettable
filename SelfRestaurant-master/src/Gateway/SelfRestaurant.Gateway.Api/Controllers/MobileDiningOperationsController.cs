using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/mobile/v1")]
public sealed class MobileDiningOperationsController : ControllerBase
{
    private readonly OrdersClient _orders;
    private readonly IHubContext<MobileNotificationsHub> _hub;

    public MobileDiningOperationsController(OrdersClient orders, IHubContext<MobileNotificationsHub> hub)
    {
        _orders = orders;
        _hub = hub;
    }

    [HttpPost("dining-sessions/{sessionId:int}/orders")]
    public async Task<ActionResult<MobileOrderResponse>> CreateOrder(int sessionId, MobileCreateOrderRequest request, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = Key();
        if (key.Error is not null) return key.Error;
        var items = request.Items ?? Array.Empty<MobileOrderItemRequest>();
        var hash = Hash(new { sessionId, Items = items.Select(x => new { x.DishId, x.Quantity, Note = x.Note?.Trim() }), Note = request.Note?.Trim() });
        try
        {
            var result = await _orders.CreateMobileOrderAsync(sessionId,
                new InternalMobileCreateOrderRequest(actor.Value, $"c{actor}:order:{key.Value}", hash, items, request.Note), ct);
            if (result is null) return Error("order_create_failed", "Không thể tạo đơn gọi món.", 502);
            await _hub.Clients.Group(MobileNotificationsHub.CustomerGroup(actor.Value))
                .SendAsync("orderUpdated", new { result.OrderId, result.DiningSessionId, result.Status }, ct);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "order_create_failed"); }
    }

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<MobileOrderResponse>>> ListOrders([FromQuery] string? scope, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        if (!string.IsNullOrWhiteSpace(scope) && !new[] { "active", "history", "all" }.Contains(scope, StringComparer.OrdinalIgnoreCase))
            return Error("validation_failed", "scope chỉ nhận active, history hoặc all.", 400);
        try { return Ok(await _orders.GetMobileOrdersAsync(actor.Value, scope, ct)); }
        catch (ApiClientException ex) { return Downstream(ex, "get_orders_failed"); }
    }

    [HttpGet("orders/{orderId:int}")]
    public async Task<ActionResult<MobileOrderResponse>> GetOrder(int orderId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var result = await _orders.GetMobileOrderAsync(orderId, actor.Value, ct);
            return result is null ? Error("order_not_found", "Không tìm thấy đơn hàng.", 404) : Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "get_order_failed"); }
    }

    [HttpGet("orders/{orderId:int}/tracking")]
    public async Task<ActionResult<MobileOrderTrackingResponse>> TrackOrder(int orderId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var result = await _orders.GetMobileOrderTrackingAsync(orderId, actor.Value, ct);
            return result is null ? Error("order_not_found", "Không tìm thấy đơn hàng.", 404) : Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "tracking_failed"); }
    }

    [HttpPost("orders/{orderId:int}/items/{itemId:int}/confirm-received")]
    public async Task<IActionResult> ConfirmItemReceived(int orderId, int itemId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var order = await _orders.GetMobileOrderAsync(orderId, actor.Value, ct);
            if (order is null) return Error("order_not_found", "Không tìm thấy đơn hàng.", 404);
            var item = order.Items.FirstOrDefault(x => x.ItemId == itemId);
            if (item is null) return Error("order_item_not_found", "Không tìm thấy món trong đơn hàng này.", 404);
            if (!string.Equals(item.Status, "READY", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "SERVING", StringComparison.OrdinalIgnoreCase))
                return Error("order_item_state_conflict", "Chỉ có thể xác nhận món đang sẵn sàng.", 409);
            await _orders.ConfirmOrderReceivedAsync(orderId, actor.Value, itemId, ct);
            await _hub.Clients.Group(MobileNotificationsHub.CustomerGroup(actor.Value))
                .SendAsync("orderUpdated", new { orderId, order.DiningSessionId, status = "SERVING" }, ct);
            return NoContent();
        }
        catch (ApiClientException ex) { return Downstream(ex, "confirm_received_failed"); }
    }

    [HttpPost("dining-sessions/{sessionId:int}/service-requests")]
    public async Task<ActionResult<ServiceRequestResponse>> CreateServiceRequest(int sessionId, MobileCreateServiceRequest request, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        var key = Key();
        if (key.Error is not null) return key.Error;
        var hash = Hash(new { sessionId, Type = request.RequestType?.Trim().ToUpperInvariant(), Note = request.Note?.Trim() });
        try
        {
            var result = await _orders.CreateServiceRequestAsync(sessionId, new InternalCreateServiceRequest(actor.Value,
                request.RequestType ?? string.Empty, request.Note, $"c{actor}:service:{key.Value}", hash), ct);
            if (result is null) return Error("service_request_failed", "Không thể tạo yêu cầu phục vụ.", 502);
            await _hub.Clients.Group(MobileNotificationsHub.CustomerGroup(actor.Value))
                .SendAsync("serviceRequestUpdated", result, ct);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "service_request_failed"); }
    }

    [HttpGet("dining-sessions/{sessionId:int}/service-requests")]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestResponse>>> ListServiceRequests(int sessionId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try { return Ok(await _orders.GetServiceRequestsAsync(sessionId, actor.Value, ct)); }
        catch (ApiClientException ex) { return Downstream(ex, "get_service_requests_failed"); }
    }

    [HttpPost("service-requests/{requestId:int}/cancel")]
    public async Task<ActionResult<ServiceRequestResponse>> CancelServiceRequest(int requestId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Error("invalid_actor", "Access token không hợp lệ.", 401);
        try
        {
            var result = await _orders.CancelServiceRequestAsync(requestId, actor.Value, ct);
            if (result is null) return Error("service_request_not_found", "Không tìm thấy yêu cầu phục vụ.", 404);
            await _hub.Clients.Group(MobileNotificationsHub.CustomerGroup(actor.Value))
                .SendAsync("serviceRequestUpdated", result, ct);
            return Ok(result);
        }
        catch (ApiClientException ex) { return Downstream(ex, "cancel_service_request_failed"); }
    }

    private int? Actor() => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;
    private (string? Value, ObjectResult? Error) Key()
    {
        var value = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > 64
            ? (null, Error("idempotency_key_required", "Idempotency-Key là bắt buộc và tối đa 64 ký tự.", 400)) : (value, null);
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private ObjectResult Downstream(ApiClientException ex, string fallback) => Error(ex.Code ?? fallback, ex.Message, ex.StatusCode, Details(ex.ResponseBody));
    private static object? Details(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.TryGetProperty("details", out var value) ? value.Clone() : null; }
        catch { return null; }
    }
    private ObjectResult Error(string code, string message, int status, object? details = null) => StatusCode(status,
        MobileApiError.Create(code, message, Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier, details));
}
