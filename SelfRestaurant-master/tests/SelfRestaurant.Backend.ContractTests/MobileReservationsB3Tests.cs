namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileReservationsB3Tests
{
    [Theory]
    [InlineData("branches")]
    [InlineData("branches/{branchId:int}")]
    [InlineData("branches/nearby")]
    [InlineData("branches/{branchId:int}/areas")]
    [InlineData("branches/{branchId:int}/table-layout")]
    [InlineData("branches/{branchId:int}/availability")]
    [InlineData("branches/{branchId:int}/menu")]
    [InlineData("qr/{code}")]
    public void Gateway_exposes_B3_discovery_routes(string route)
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileCatalogGatewayController.cs");
        Assert.Contains($"[HttpGet(\"{route}\")]", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[HttpPost]")]
    [InlineData("[HttpGet]")]
    [InlineData("[HttpGet(\"{reservationId:int}\")]")]
    [InlineData("[HttpPut(\"{reservationId:int}\")]")]
    [InlineData("[HttpPost(\"{reservationId:int}/cancel\")]")]
    [InlineData("[HttpGet(\"{reservationId:int}/deposit\")]")]
    [InlineData("[HttpGet(\"{reservationId:int}/preorder\")]")]
    [InlineData("[HttpPut(\"{reservationId:int}/preorder\")]")]
    [InlineData("[HttpPost(\"{reservationId:int}/preorder/submit\")]")]
    public void Gateway_exposes_B3_reservation_routes(string action)
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileReservationsGatewayController.cs");
        Assert.Contains("[Route(\"api/mobile/v1/reservations\")]", source, StringComparison.Ordinal);
        Assert.Contains(action, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Reservation_actor_comes_from_JWT_and_branch_is_not_mutable()
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileReservationsGatewayController.cs");
        var contracts = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Models/MobileReservationContracts.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", source, StringComparison.Ordinal);
        Assert.Contains("JwtRegisteredClaimNames.Sub", source, StringComparison.Ordinal);
        Assert.Contains("record UpdateMobileReservationRequest(int? TableId", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateMobileReservationRequest(int BranchId", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void Reservation_commands_require_idempotency_and_detect_payload_conflicts()
    {
        var gateway = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileReservationsGatewayController.cs");
        var customers = ReadRepoFile("src/Services/SelfRestaurant.Customers.Api/Controllers/CustomersController.cs");
        Assert.Contains("Idempotency-Key", gateway, StringComparison.Ordinal);
        Assert.Contains("idempotency_conflict", gateway, StringComparison.Ordinal);
        Assert.Contains("idempotency_conflict", customers, StringComparison.Ordinal);
        Assert.Contains("RequestHash", customers, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", customers, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_selected_table_replay_preserves_original_selection_but_explicit_table_is_checked()
    {
        var gateway = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileReservationsGatewayController.cs");
        Assert.Contains("(request.TableId is > 0 && retry.TableId != request.TableId)", gateway, StringComparison.Ordinal);
        Assert.Contains("return Ok(retry)", gateway, StringComparison.Ordinal);
    }

    [Fact]
    public void Availability_is_composed_without_cross_database_reads()
    {
        var gateway = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileCatalogGatewayController.cs");
        var customers = ReadRepoFile("src/Services/SelfRestaurant.Customers.Api/Controllers/CustomersController.cs");
        Assert.Contains("_catalog.GetBranchTablesAsync", gateway, StringComparison.Ordinal);
        Assert.Contains("_customers.GetOccupiedTableIdsAsync", gateway, StringComparison.Ordinal);
        Assert.Contains("api/internal/reservations/occupied-tables", customers, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_preorders_are_owned_by_Orders_and_prices_come_from_Catalog()
    {
        var gateway = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileReservationsGatewayController.cs");
        var orders = ReadRepoFile("src/Services/SelfRestaurant.Orders.Api/Controllers/ReservationPreOrdersController.cs");
        var context = ReadRepoFile("src/Services/SelfRestaurant.Orders.Api/Persistence/Context/OrdersDbContext.cs");
        Assert.Contains("_orders.UpsertReservationPreOrderAsync", gateway, StringComparison.Ordinal);
        Assert.Contains("_catalog.GetDishesAsync", orders, StringComparison.Ordinal);
        Assert.Contains("DbSet<ReservationPreOrder>", context, StringComparison.Ordinal);
        Assert.Contains("SubmitIdempotencyKey", orders, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SelfRestaurant.Microservices.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
