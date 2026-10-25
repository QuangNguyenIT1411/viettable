using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Infrastructure;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence.Entities;

namespace SelfRestaurant.Orders.Api.Controllers;

[ApiController]
[Route("api/internal/reservation-preorders")]
public sealed class ReservationPreOrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly ICatalogReadModel _catalog;

    public ReservationPreOrdersController(OrdersDbContext db, ICatalogReadModel catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet("{reservationId:int}")]
    public async Task<ActionResult<PreOrderResponse>> Get(int reservationId, CancellationToken ct)
    {
        var entity = await Load(reservationId, ct);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPut("{reservationId:int}")]
    public async Task<ActionResult<PreOrderResponse>> Put(int reservationId, UpsertPreOrderRequest request, CancellationToken ct)
    {
        if (reservationId <= 0 || request.CustomerId <= 0 || request.BranchId <= 0 || request.Items is null
            || request.Items.Count > 50 || request.Items.Any(x => x.DishId <= 0 || x.Quantity is < 1 or > 50))
            return BadRequest(new { code = "validation_failed", message = "Giỏ món đặt trước không hợp lệ." });
        var ids = request.Items.Select(x => x.DishId).Distinct().ToArray();
        var catalogDishes = await _catalog.GetDishesAsync(ids, ct) ?? Array.Empty<CatalogApiClient.DishSnapshotResponse>();
        var byId = catalogDishes.ToDictionary(x => x.DishId);
        if (ids.Any(id => !byId.TryGetValue(id, out var dish) || !dish.IsActive || !dish.Available))
            return UnprocessableEntity(new { code = "dish_unavailable", message = "Một hoặc nhiều món không còn bán." });

        var entity = await Load(reservationId, ct);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new ReservationPreOrder { ReservationId = reservationId, CustomerId = request.CustomerId,
                BranchId = request.BranchId, CreatedAtUtc = now, UpdatedAtUtc = now };
            _db.ReservationPreOrders.Add(entity);
        }
        else if (entity.CustomerId != request.CustomerId || entity.BranchId != request.BranchId)
            return Conflict(new { code = "preorder_context_conflict", message = "Pre-order không cùng khách hàng hoặc chi nhánh với reservation." });

        _db.ReservationPreOrderItems.RemoveRange(entity.Items);
        entity.Items.Clear();
        foreach (var item in request.Items)
        {
            var dish = byId[item.DishId];
            entity.Items.Add(new ReservationPreOrderItem { DishId = dish.DishId, DishNameSnapshot = dish.Name,
                UnitPriceSnapshot = dish.Price, Quantity = item.Quantity, Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim() });
        }
        entity.Status = "DRAFT";
        entity.SubmittedAtUtc = null;
        entity.SubmitIdempotencyKey = null;
        entity.SubmitRequestHash = null;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpPost("{reservationId:int}/submit")]
    public async Task<ActionResult<PreOrderResponse>> Submit(int reservationId, SubmitPreOrderRequest request, CancellationToken ct)
    {
        var entity = await Load(reservationId, ct);
        if (entity is null) return NotFound(new { code = "preorder_not_found", message = "Không tìm thấy giỏ món đặt trước." });
        if (entity.CustomerId != request.CustomerId)
            return StatusCode(403, new { code = "forbidden", message = "Khách hàng không sở hữu pre-order này." });
        if (string.Equals(entity.SubmitIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            if (!string.Equals(entity.SubmitRequestHash, request.RequestHash, StringComparison.Ordinal))
                return Conflict(new { code = "idempotency_conflict", message = "Idempotency key đã được dùng với dữ liệu khác." });
            return Ok(Map(entity));
        }
        if (entity.Items.Count == 0) return UnprocessableEntity(new { code = "preorder_empty", message = "Giỏ món đặt trước đang trống." });
        if (!string.Equals(entity.Status, "DRAFT", StringComparison.Ordinal))
            return Conflict(new { code = "preorder_state_conflict", message = "Trạng thái pre-order không cho phép gửi." });
        var now = DateTime.UtcNow;
        entity.Status = "SUBMITTED";
        entity.SubmitIdempotencyKey = request.IdempotencyKey;
        entity.SubmitRequestHash = request.RequestHash;
        entity.SubmittedAtUtc = now;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    /// <summary>The reservation was cancelled: its draft/submitted dishes must never reach the kitchen. Idempotent.</summary>
    [HttpPost("{reservationId:int}/cancel")]
    public async Task<IActionResult> Cancel(int reservationId, CancellationToken ct)
    {
        var entity = await Load(reservationId, ct);
        if (entity is null) return NoContent();
        if (entity.ConvertedOrderId is > 0)
            return Conflict(new { code = "preorder_converted", message = "Món đặt trước đã được chuyển thành đơn." });
        if (!string.Equals(entity.Status, "CANCELLED", StringComparison.Ordinal))
        {
            entity.Status = "CANCELLED";
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    private Task<ReservationPreOrder?> Load(int reservationId, CancellationToken ct) =>
        _db.ReservationPreOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);
    private static PreOrderResponse Map(ReservationPreOrder x) => new(x.ReservationId, x.CustomerId, x.BranchId,
        x.Status, x.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity), x.SubmittedAtUtc,
        x.Items.OrderBy(i => i.PreOrderItemId).Select(i => new PreOrderItemResponse(i.PreOrderItemId, i.DishId,
            i.DishNameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.Note)).ToArray());

    public sealed record PreOrderItemRequest(int DishId, int Quantity, string? Note);
    public sealed record UpsertPreOrderRequest(int CustomerId, int BranchId, IReadOnlyList<PreOrderItemRequest>? Items);
    public sealed record SubmitPreOrderRequest(int CustomerId, string IdempotencyKey, string RequestHash);
    public sealed record PreOrderItemResponse(int PreOrderItemId, int DishId, string DishNameSnapshot,
        decimal UnitPriceSnapshot, int Quantity, string? Note);
    public sealed record PreOrderResponse(int ReservationId, int CustomerId, int BranchId, string Status,
        decimal Subtotal, DateTime? SubmittedAtUtc, IReadOnlyList<PreOrderItemResponse> Items);
}
