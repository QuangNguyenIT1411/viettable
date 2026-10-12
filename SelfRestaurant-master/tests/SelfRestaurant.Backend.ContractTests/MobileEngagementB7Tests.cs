namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileEngagementB7Tests
{
    [Theory]
    [InlineData("[HttpGet(\"me/favorites\")]")]
    [InlineData("[HttpPost(\"me/favorites/dishes/{dishId:int}\")]")]
    [InlineData("[HttpDelete(\"me/favorites/dishes/{dishId:int}\")]")]
    [InlineData("[HttpGet(\"notifications\")]")]
    [InlineData("[HttpPost(\"notifications/{notificationId:long}/read\")]")]
    [InlineData("[HttpPost(\"reviews\")]")]
    [InlineData("[HttpGet(\"me/reviews\")]")]
    [InlineData("[HttpPut(\"reviews/{reviewId:int}\")]")]
    [InlineData("[HttpDelete(\"reviews/{reviewId:int}\")]")]
    [InlineData("[HttpPost(\"support/requests\")]")]
    public void Gateway_exposes_customer_scoped_B7_routes(string route)
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileEngagementGatewayController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", source, StringComparison.Ordinal);
        Assert.Contains(route, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_FAQ_is_the_only_anonymous_B7_route()
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileEngagementGatewayController.cs");
        Assert.Contains("[AllowAnonymous]\n    [HttpGet(\"support/faqs\")]", source.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void Customers_owns_engagement_records_and_unique_business_keys()
    {
        var source = Read("src/Services/SelfRestaurant.Customers.Api/Persistence/Context/CustomersDbContext.cs");
        Assert.Contains("DbSet<CustomerFavorite>", source, StringComparison.Ordinal);
        Assert.Contains("DbSet<CustomerReview>", source, StringComparison.Ordinal);
        Assert.Contains("DbSet<SupportRequest>", source, StringComparison.Ordinal);
        Assert.Contains("new { x.CustomerId, x.DishId }).IsUnique()", source, StringComparison.Ordinal);
        Assert.Contains("HasIndex(x => x.InvoiceId).IsUnique()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Gateway_validates_favorites_and_completed_review_eligibility_with_domain_owners()
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileEngagementGatewayController.cs");
        Assert.Contains("_catalog.GetAdminDishByIdAsync", source, StringComparison.Ordinal);
        Assert.Contains("_billing.GetInvoiceAsync(request.InvoiceId, actor.Value", source, StringComparison.Ordinal);
        Assert.Contains("review_not_eligible", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_and_support_commands_store_hashes_for_stable_retry()
    {
        var source = Read("src/Services/SelfRestaurant.Customers.Api/Controllers/MobileEngagementController.cs");
        Assert.Contains("CreateIdempotencyKey", source, StringComparison.Ordinal);
        Assert.Contains("IdempotencyConflict()", source, StringComparison.Ordinal);
        Assert.Contains("entity.CustomerId != customerId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Cashier_onsite_confirmation_notifies_the_paying_customer_without_failing_the_payment()
    {
        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/StaffCashierGatewayController.cs");
        Assert.Contains("if (paymentId is not null) await NotifyCustomerPaymentAsync(result, ct);", gateway, StringComparison.Ordinal);
        Assert.Contains("_customersClient.AddPaymentSucceededNotificationAsync(customerId, paymentId, diningSessionId, amount, ct)", gateway, StringComparison.Ordinal);
        Assert.Contains("MobileNotificationsHub.CustomerGroup(customerId))\n                .SendAsync(\"paymentUpdated\"", gateway.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("status.GetString() != \"SUCCEEDED\"", gateway, StringComparison.Ordinal);
        var billing = Read("src/Services/SelfRestaurant.Billing.Api/Controllers/CashierMobilePaymentsController.cs");
        Assert.Contains("payment.CustomerId, payment.DiningSessionId", billing, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_checkout_settles_app_sessions_through_the_mobile_invoice_path()
    {
        var cashier = Read("src/Services/SelfRestaurant.Billing.Api/Controllers/CashierController.cs");
        var mobileBranch = cashier.IndexOf("return await CheckoutMobileSessionAsync(command, request, initialOrder, cancellationToken);", StringComparison.Ordinal);
        Assert.True(mobileBranch > 0);
        // The app-session branch runs before any legacy bill can be created.
        Assert.True(mobileBranch < cashier.IndexOf("_db.Bills.Add(bill);", StringComparison.Ordinal));
        Assert.Contains("_mobileSettlement.SettleAsync(", cashier, StringComparison.Ordinal);
        Assert.Contains("MobilePaymentId: result.PaymentId", cashier, StringComparison.Ordinal);

        var settlement = Read("src/Services/SelfRestaurant.Billing.Api/Infrastructure/Payments/CashierMobileSettlementService.cs");
        Assert.Contains("PaymentFinalizationService.LockQuoteAsync(db, quote.CheckoutQuoteId, ct)", settlement, StringComparison.Ordinal);
        Assert.Contains("finalization.ConfirmAsync(paymentId, employeeId, branchId, null, ct)", settlement, StringComparison.Ordinal);
        Assert.Contains("$\"cashier:{commandKey}:pay\"", settlement, StringComparison.Ordinal);
        Assert.Contains("$\"cashier:{commandKey}:quote\"", settlement, StringComparison.Ordinal);

        var orders = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/OrdersController.cs");
        Assert.Contains("diningSessionCustomerId = mobileSession?.CustomerId", orders, StringComparison.Ordinal);

        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/StaffCashierGatewayController.cs");
        Assert.Contains("BranchId: staff.BranchId", gateway, StringComparison.Ordinal);
        Assert.Contains("await NotifyCustomerPaymentAsync(payerId, mobilePaymentId, paidSessionId, response.TotalAmount, cancellationToken);", gateway, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SelfRestaurant.Microservices.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
