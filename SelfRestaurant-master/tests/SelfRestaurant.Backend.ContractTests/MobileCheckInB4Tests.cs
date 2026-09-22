namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileCheckInB4Tests
{
    [Theory]
    [InlineData("[HttpPost(\"check-ins/qr/validate\")]")]
    [InlineData("[HttpPost(\"check-ins/reservation\")]")]
    [InlineData("[HttpPost(\"check-ins/walk-in\")]")]
    [InlineData("[HttpGet(\"dining-sessions/active\")]")]
    [InlineData("[HttpGet(\"dining-sessions/{sessionId:int}\")]")]
    [InlineData("[HttpPost(\"dining-sessions/{sessionId:int}/cancel-payment-request\")]")]
    public void Gateway_exposes_B4_routes(string route)
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileCheckInsGatewayController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", source, StringComparison.Ordinal);
        Assert.Contains(route, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Qr_is_only_resolved_by_Catalog_and_mismatch_needs_explicit_confirmation()
    {
        var source = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileCheckInsGatewayController.cs");
        Assert.Contains("_catalog.GetTableByQrAsync", source, StringComparison.Ordinal);
        Assert.Contains("table_mismatch_confirmation_required", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmTableMismatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_owns_explicit_sessions_and_enforces_active_table_uniqueness()
    {
        var context = Read("src/Services/SelfRestaurant.Orders.Api/Persistence/Context/OrdersDbContext.cs");
        var bootstrap = Read("src/Services/SelfRestaurant.Orders.Api/Persistence/Bootstrap/OrdersDbBootstrapper.cs");
        Assert.Contains("DbSet<DiningSession>", context, StringComparison.Ordinal);
        Assert.Contains("UX_DiningSessions_ActiveTable", context, StringComparison.Ordinal);
        Assert.Contains("WHERE EndedAtUtc IS NULL", bootstrap, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Submitted_preorder_is_converted_and_linked_exactly_once()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        var entity = Read("src/Services/SelfRestaurant.Orders.Api/Persistence/Entities/ReservationPreOrder.cs");
        Assert.Contains("preOrder.ConvertedOrderId is > 0", source, StringComparison.Ordinal);
        Assert.Contains("preOrder.Status = \"CONVERTED\"", source, StringComparison.Ordinal);
        Assert.Contains("DiningSessionCode = session.SessionCode", source, StringComparison.Ordinal);
        Assert.Contains("ConvertedOrderId", entity, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_reads_are_scoped_to_Jwt_customer()
    {
        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileCheckInsGatewayController.cs");
        var service = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        Assert.Contains("JwtRegisteredClaimNames.Sub", gateway, StringComparison.Ordinal);
        Assert.Contains("entity.CustomerId != customerId", service, StringComparison.Ordinal);
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
