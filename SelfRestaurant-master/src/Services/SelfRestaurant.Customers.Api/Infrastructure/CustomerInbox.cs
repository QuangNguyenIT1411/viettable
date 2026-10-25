using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Infrastructure;

/// <summary>
/// The customer's notification inbox (table ReadyDishNotifications, named after its first use). Every event has a
/// Vietnamese title and an app tab (category). Rows of non-order events keep OrderId 0 and put the reservation,
/// payment or request id in OrderItemId, so the existing (OrderId, OrderItemId, EventName) index finds duplicates.
/// </summary>
public static class CustomerInbox
{
    public const string ReservationCreated = "reservation.created.v1";
    public const string ReservationUpdated = "reservation.updated.v1";
    public const string ReservationCancelled = "reservation.cancelled.v1";
    public const string ReservationCheckedIn = "reservation.checked-in.v1";
    public const string ReservationNoShow = "reservation.no-show.v1";
    public const string KitchenStarted = "order.kitchen-started.v1";
    public const string OrderReady = "order.status-ready.v1";
    public const string OrderCancelled = "order.cancelled.v1";
    public const string OrderItemCancelled = "order.item-cancelled.v1";
    public const string ServiceAcknowledged = "service.request-acknowledged.v1";
    public const string ServiceCompleted = "service.request-completed.v1";
    public const string PaymentSucceeded = "billing.payment-succeeded.v1";
    public const string PaymentCancelled = "billing.payment-cancelled.v1";
    public const string PointsEarned = "loyalty.points-earned.v1";
    public const string TierUp = "loyalty.tier-up.v1";

    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        [ReservationCreated] = "Đặt bàn thành công",
        [ReservationUpdated] = "Đặt bàn đã thay đổi",
        [ReservationCancelled] = "Đặt bàn đã hủy",
        [ReservationCheckedIn] = "Đã nhận bàn",
        [ReservationNoShow] = "Đặt bàn đã quá giờ",
        [KitchenStarted] = "Bếp đã nhận đơn",
        [OrderReady] = "Món ăn đã sẵn sàng",
        [OrderCancelled] = "Đơn hàng bị hủy",
        [OrderItemCancelled] = "Món bị hủy",
        [ServiceAcknowledged] = "Nhân viên đã nhận yêu cầu",
        [ServiceCompleted] = "Yêu cầu đã xong",
        [PaymentSucceeded] = "Thanh toán thành công",
        [PaymentCancelled] = "Giao dịch đã hủy",
        [PointsEarned] = "Cộng điểm thưởng",
        [TierUp] = "Lên hạng thành viên",
    };

    /// <summary>Events that may happen more than once for the same item (a reservation can be changed again).</summary>
    private static readonly HashSet<string> Repeatable = new(StringComparer.Ordinal) { ReservationUpdated };

    public static bool IsKnown(string? eventName) => eventName is not null && Titles.ContainsKey(eventName);

    public static string Title(string eventName) => Titles.TryGetValue(eventName, out var title) ? title : "Thông báo";

    /// <summary>App tab: reservation, order (kitchen and service requests), payment, offer (points, tiers, promotions).</summary>
    public static string Category(string eventName) => eventName.Split('.')[0] switch
    {
        "reservation" => "reservation",
        "order" or "service" => "order",
        "billing" => "payment",
        "loyalty" or "promotion" => "offer",
        _ => "order",
    };

    /// <summary>
    /// Adds a row to the context (caller saves) unless it already exists: once per (item, event), or for a repeatable
    /// event once per identical message within two minutes (a replayed request). Returns the existing row if any.
    /// </summary>
    public static async Task<(ReadyDishNotifications Row, bool Created)> AddAsync(CustomersDbContext db, int customerId,
        string eventName, int refId, string message, int orderId = 0, CancellationToken ct = default)
    {
        var duplicates = db.ReadyDishNotifications.Where(x => x.CustomerId == customerId && x.OrderId == orderId
            && x.OrderItemId == refId && x.EventName == eventName);
        if (Repeatable.Contains(eventName))
        {
            var since = DateTime.UtcNow.AddMinutes(-2);
            duplicates = duplicates.Where(x => x.Message == message && x.CreatedAtUtc >= since);
        }
        var existing = await duplicates.OrderByDescending(x => x.ReadyDishNotificationId).FirstOrDefaultAsync(ct)
            ?? db.ReadyDishNotifications.Local.FirstOrDefault(x => x.CustomerId == customerId && x.OrderId == orderId
                && x.OrderItemId == refId && x.EventName == eventName && x.Message == message);
        if (existing is not null) return (existing, false);
        var row = new ReadyDishNotifications
        {
            OrderId = orderId, OrderItemId = refId, CustomerId = customerId, EventName = eventName,
            Message = message.Length > 500 ? message[..500] : message, Status = "OPEN", CreatedAtUtc = DateTime.UtcNow
        };
        db.ReadyDishNotifications.Add(row);
        return (row, true);
    }

    // ---- Reservation messages (restaurant time, UTC+7) ----

    public static string ReservationMessage(string eventName, Reservation r) => eventName switch
    {
        ReservationCreated => $"Đặt bàn {r.ReservationCode} lúc {When(r.ReservedAt)} cho {r.PartySize} khách đã được ghi nhận.",
        ReservationUpdated => $"Đặt bàn {r.ReservationCode} đã đổi sang {When(r.ReservedAt)}, {r.PartySize} khách.",
        ReservationCancelled => $"Đặt bàn {r.ReservationCode} lúc {When(r.ReservedAt)} đã được hủy.",
        ReservationCheckedIn => $"Bạn đã nhận bàn theo đặt bàn {r.ReservationCode}. Chúc quý khách ngon miệng!",
        ReservationNoShow => $"Đặt bàn {r.ReservationCode} lúc {When(r.ReservedAt)} đã quá giờ đến nên được ghi nhận là vắng mặt.",
        _ => $"Đặt bàn {r.ReservationCode} có cập nhật mới.",
    };

    /// <summary>Queues the reservation event for its customer (walk-in bookings without an account get none).</summary>
    public static Task AddReservationAsync(CustomersDbContext db, string eventName, Reservation r, CancellationToken ct) =>
        r.CustomerId is > 0 ? AddAsync(db, r.CustomerId.Value, eventName, r.ReservationId, ReservationMessage(eventName, r), 0, ct) : Task.CompletedTask;

    public static string When(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), RestaurantTimeZone)
            .ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    // ---- Loyalty (same thresholds as the profile's membership tiers) ----

    public static (string Code, string Name) Tier(int points) => points switch
    {
        >= 20000 => ("DIAMOND", "Kim Cương"),
        >= 5000 => ("GOLD", "Vàng"),
        >= 1000 => ("SILVER", "Bạc"),
        _ => ("MEMBER", "Thành viên"),
    };

    private static TimeZoneInfo RestaurantTimeZone { get; } = FindZone();

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
