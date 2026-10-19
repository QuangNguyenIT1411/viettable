using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Infrastructure;

/// <summary>
/// After a payment is durably SUCCEEDED: an inbox row for the customer plus a realtime "paymentUpdated" nudge.
/// Shared by the cashier's confirmation and the SePay auto-confirmation so the app sees exactly the same thing.
/// Best effort: a failure here never turns a collected payment into an error.
/// </summary>
public sealed class CustomerPaymentNotifier(CustomersClient customers, IHubContext<MobileNotificationsHub> mobileHub,
    ILogger<CustomerPaymentNotifier> logger)
{
    public async Task NotifyAsync(int customerId, int paymentId, int diningSessionId, decimal amount, CancellationToken ct)
    {
        try { await customers.AddPaymentSucceededNotificationAsync(customerId, paymentId, diningSessionId, amount, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Payment {PaymentId} notification deferred.", paymentId); }
        try
        {
            await mobileHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId))
                .SendAsync("paymentUpdated", new { diningSessionId, paymentId, status = "SUCCEEDED" }, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Payment {PaymentId} realtime update deferred.", paymentId); }
    }
}
