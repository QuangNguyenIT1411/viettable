namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileDiningB5Tests
{
    [Theory]
    [InlineData("[HttpPost(\"dining-sessions/{sessionId:int}/orders\")]")]
    [InlineData("[HttpGet(\"orders\")]")]
    [InlineData("[HttpGet(\"orders/{orderId:int}\")]")]
    [InlineData("[HttpGet(\"orders/{orderId:int}/tracking\")]")]
    [InlineData("[HttpPost(\"dining-sessions/{sessionId:int}/service-requests\")]")]
    [InlineData("[HttpGet(\"dining-sessions/{sessionId:int}/service-requests\")]")]
    [InlineData("[HttpPost(\"service-requests/{requestId:int}/cancel\")]")]
    [InlineData("[HttpPost(\"orders/{orderId:int}/items/{itemId:int}/confirm-received\")]")]
    public void Gateway_exposes_B5_mobile_routes(string route)
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileDiningOperationsController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", source, StringComparison.Ordinal);
        Assert.Contains(route, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Staff_service_requests_are_branch_scoped_and_transition_guarded()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/StaffServiceRequestsController.cs");
        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/StaffCashierGatewayController.cs");
        Assert.Contains("x.BranchId == branchId", source, StringComparison.Ordinal);
        Assert.Contains("row.BranchId != command.BranchId", source, StringComparison.Ordinal);
        Assert.Contains("ServiceRequestStateMachine.CanTransition", source, StringComparison.Ordinal);
        Assert.Contains("RequireCashier()", gateway, StringComparison.Ordinal);
        Assert.Contains("serviceRequestUpdated", gateway, StringComparison.Ordinal);
        Assert.Contains("session.EndedAtUtc is not null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_close_cleanup_cancels_only_active_service_requests()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        var start = source.IndexOf("[HttpPost(\"{sessionId:int}/payment-succeeded\")]", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected to find the session payment-success endpoint.");
        var end = source.IndexOf("[HttpPost(\"{sessionId:int}/orders\")]", start, StringComparison.Ordinal);
        Assert.True(end > start, "Expected to find the next order endpoint boundary.");

        var closeEndpoint = source[start..end];
        Assert.Contains("x.Status == \"OPEN\" || x.Status == \"ACKNOWLEDGED\"", closeEndpoint, StringComparison.Ordinal);
        Assert.Contains("serviceRequest.Status = \"CANCELLED\"", closeEndpoint, StringComparison.Ordinal);
        Assert.Contains("serviceRequest.CancelledAtUtc = now", closeEndpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("x.Status == \"COMPLETED\"", closeEndpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_item_received_endpoint_checks_owner_and_item_state()
    {
        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileDiningOperationsController.cs");
        var orders = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/OrdersController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", gateway, StringComparison.Ordinal);
        Assert.Contains("_orders.GetMobileOrderAsync(orderId, actor.Value", gateway, StringComparison.Ordinal);
        Assert.Contains("order.CustomerID != customerId", orders, StringComparison.Ordinal);
        Assert.Contains("x.OrderID == orderId && x.ItemID == itemId.Value", orders, StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_owns_service_requests_and_order_command_hashes()
    {
        var context = Read("src/Services/SelfRestaurant.Orders.Api/Persistence/Context/OrdersDbContext.cs");
        var controller = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        Assert.Contains("DbSet<ServiceRequest>", context, StringComparison.Ordinal);
        Assert.Contains("SubmitRequestHash", context, StringComparison.Ordinal);
        Assert.Contains("idempotency_conflict", controller, StringComparison.Ordinal);
        Assert.Contains("session_payment_locked", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_mobile_order_is_bound_to_owned_explicit_session()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        Assert.Contains("ValidateWritableSession(session, request.CustomerId)", source, StringComparison.Ordinal);
        Assert.Contains("DiningSessionCode = session!.SessionCode", source, StringComparison.Ordinal);
        Assert.Contains("CustomerID = session.CustomerId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_realtime_hub_is_authenticated_and_customer_scoped()
    {
        var hub = Read("src/Gateway/SelfRestaurant.Gateway.Api/Hubs/MobileNotificationsHub.cs");
        var program = Read("src/Gateway/SelfRestaurant.Gateway.Api/Program.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", hub, StringComparison.Ordinal);
        Assert.Contains("JwtRegisteredClaimNames.Sub", hub, StringComparison.Ordinal);
        Assert.Contains("/hubs/mobile-notifications", program, StringComparison.Ordinal);
        Assert.Contains("OnMessageReceived", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Chef_status_changes_publish_customer_order_updates()
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/StaffChefGatewayController.cs");
        Assert.Contains("BroadcastMobileOrderUpdatedAsync", source, StringComparison.Ordinal);
        Assert.Contains("MobileNotificationsHub.CustomerGroup(customerId)", source, StringComparison.Ordinal);
        Assert.Contains("orderUpdated", source, StringComparison.Ordinal);
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
