namespace SelfRestaurant.Backend.ContractTests;

public sealed class BaselineRouteTests
{
    [Theory]
    [InlineData("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/CustomerGatewayController.cs", "GetSession", "HttpGet", "session")]
    [InlineData("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/CustomerGatewayController.cs", "CreateReservation", "HttpPost", "/api/customer/reservations")]
    [InlineData("src/Services/SelfRestaurant.Catalog.Api/Controllers/CatalogController.cs", "GetBranches", "HttpGet", "api/branches")]
    [InlineData("src/Services/SelfRestaurant.Catalog.Api/Controllers/CatalogController.cs", "GetMenu", "HttpGet", "api/branches/{branchId:int}/menu")]
    [InlineData("src/Services/SelfRestaurant.Customers.Api/Controllers/CustomersController.cs", "CreateReservation", "HttpPost", "api/customers/reservations")]
    [InlineData("src/Services/SelfRestaurant.Customers.Api/Controllers/CustomersController.cs", "ReplacePreOrderItems", "HttpPost", "api/customers/reservations/{reservationId:int}/preorder-items")]
    [InlineData("src/Services/SelfRestaurant.Orders.Api/Controllers/OrdersController.cs", "AddItem", "HttpPost", "api/tables/{tableId:int}/order/items")]
    [InlineData("src/Services/SelfRestaurant.Orders.Api/Controllers/OrdersController.cs", "SubmitOrder", "HttpPost", "api/tables/{tableId:int}/order/submit")]
    [InlineData("src/Services/SelfRestaurant.Billing.Api/Controllers/CashierController.cs", "Checkout", "HttpPost", "api/orders/{orderId:int}/checkout")]
    [InlineData("src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs", "Login", "HttpPost", "login")]
    public void Existing_route_used_as_migration_baseline_is_present(string relativePath, string methodName, string attributeName, string template)
    {
        var source = ReadRepoFile(relativePath);

        Assert.Contains($"[{attributeName}(\"{template}\")]", source, StringComparison.Ordinal);
        Assert.Contains($" {methodName}(", source, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SelfRestaurant.Microservices.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
