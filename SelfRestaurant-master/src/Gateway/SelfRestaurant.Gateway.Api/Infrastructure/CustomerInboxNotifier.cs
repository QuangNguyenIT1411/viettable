using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

/// <summary>
/// Customer inbox rows for staff actions (kitchen, service requests, cashier cancels) plus a realtime
/// "notificationCreated" nudge so the app refreshes its bell. Best effort: a failure only means a missing
/// notification, never a failed kitchen or cashier action.
/// </summary>
public sealed class CustomerInboxNotifier(CustomersClient customers, IHubContext<MobileNotificationsHub> mobileHub,
    ILogger<CustomerInboxNotifier> logger)
{
    public const string KitchenStarted = "order.kitchen-started.v1";
    public const string OrderCancelled = "order.cancelled.v1";
    public const string OrderItemCancelled = "order.item-cancelled.v1";
    public const string ServiceAcknowledged = "service.request-acknowledged.v1";
    public const string ServiceCompleted = "service.request-completed.v1";
    public const string PaymentCancelled = "billing.payment-cancelled.v1";

    public async Task NotifyAsync(int customerId, string eventName, int refId, string message, int? orderId, CancellationToken ct)
    {
        if (customerId <= 0) return;
        try { await customers.AddMobileNotificationAsync(customerId, eventName, refId, message, orderId, ct); }
        catch (Exception ex) { logger.LogWarning("Inbox {EventName} for customer skipped ({ErrorType}).", eventName, ex.GetType().Name); return; }
        await PushAsync(customerId, ct);
    }

    /// <summary>Only the realtime nudge: the row was written by the owning service (e.g. reservations in Customers).</summary>
    public async Task PushAsync(int? customerId, CancellationToken ct)
    {
        if (customerId is not > 0) return;
        try { await mobileHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId.Value)).SendAsync("notificationCreated", new { }, ct); }
        catch (Exception ex) { logger.LogWarning("Inbox realtime nudge skipped ({ErrorType}).", ex.GetType().Name); }
    }

    /// <summary>
    /// The restaurant changed a booking (confirmed, rejected, no-show): the app reloads it, and the inbox row
    /// Customers wrote in the same step.
    /// </summary>
    public async Task ReservationChangedAsync(int? customerId, int reservationId, string status, CancellationToken ct)
    {
        if (customerId is not > 0) return;
        try
        {
            await mobileHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId.Value))
                .SendAsync("reservationChanged", new { reservationId, status }, ct);
        }
        catch (Exception ex) { logger.LogWarning("Reservation realtime nudge skipped ({ErrorType}).", ex.GetType().Name); }
        await PushAsync(customerId, ct);
    }

    /// <summary>"giục bếp" for KITCHEN_RUSH etc.; same words as the app's request list.</summary>
    public static string RequestLabel(string? requestType) => requestType?.Trim().ToUpperInvariant() switch
    {
        "CALL_STAFF" => "gọi nhân viên",
        "WATER" => "xin nước",
        "UTENSILS" => "xin dụng cụ",
        "KITCHEN_RUSH" => "giục bếp",
        "KITCHEN_CANCEL" => "xin hủy món",
        _ => "phục vụ",
    };
}
