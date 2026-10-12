using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class CustomerPaymentNotificationTests
{
    [Fact]
    public async Task Payment_notification_is_created_once_per_payment_and_reaches_only_the_payer_inbox()
    {
        await using var db = Database();
        var controller = new MobileEngagementController(db);
        var request = new MobileEngagementController.PaymentSucceededRequest(PaymentId: 91, DiningSessionId: 25, Amount: 250000m);

        await controller.PaymentSucceeded(7, request, CancellationToken.None);
        await controller.PaymentSucceeded(7, request, CancellationToken.None);

        var row = Assert.Single(db.ReadyDishNotifications);
        Assert.Equal(MobileEngagementController.PaymentSucceededEvent, row.EventName);
        Assert.Equal(7, row.CustomerId);
        Assert.Null(row.TableId);
        Assert.Equal("Thanh toán thành công 250.000đ cho phiên #25. Cảm ơn quý khách!", row.Message);

        var payerInbox = Assert.IsType<OkObjectResult>((await controller.Notifications(7, null, 20, CancellationToken.None)).Result);
        Assert.Contains(MobileEngagementController.PaymentSucceededEvent, System.Text.Json.JsonSerializer.Serialize(payerInbox.Value));
        var otherInbox = Assert.IsType<OkObjectResult>((await controller.Notifications(8, null, 20, CancellationToken.None)).Result);
        Assert.DoesNotContain(MobileEngagementController.PaymentSucceededEvent, System.Text.Json.JsonSerializer.Serialize(otherInbox.Value));
    }

    [Fact]
    public async Task Payment_notification_rejects_invalid_input()
    {
        await using var db = Database();
        var controller = new MobileEngagementController(db);
        var result = await controller.PaymentSucceeded(7, new(PaymentId: 0, DiningSessionId: 25, Amount: 1m), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.ReadyDishNotifications);
    }

    [Fact]
    public void Ready_dish_message_uses_short_order_number()
    {
        Assert.Equal("Bò né đã sẵn sàng. Vui lòng đến nhận món cho đơn #44.", OrderReadyConsumerService.ReadyMessage("Bò né", 44));
    }

    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
