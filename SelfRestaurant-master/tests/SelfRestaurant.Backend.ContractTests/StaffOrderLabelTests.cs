namespace SelfRestaurant.Backend.ContractTests;

// Staff web shows "Đơn #<id>" instead of the long ORD-<timestamp>-<guid> code, so bill rows must carry the order id.
public sealed class StaffOrderLabelTests
{
    [Fact]
    public void Bill_history_rows_carry_order_id_through_identity_and_gateway()
    {
        var identityContracts = Read("src/Services/SelfRestaurant.Identity.Api/Infrastructure/InternalHistoryContracts.cs");
        var identity = Read("src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs");
        var gatewayContracts = Read("src/Gateway/SelfRestaurant.Gateway.Api/Models/ApiContracts.cs");
        var cashierContracts = Read("src/Gateway/SelfRestaurant.Gateway.Api/Models/GatewayStaffCashierContracts.cs");
        var cashier = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/StaffCashierGatewayController.cs");

        Assert.Contains("decimal? ChangeAmount,\n    int? OrderId = null);", Normalize(identityContracts), StringComparison.Ordinal);
        Assert.Contains("b.ChangeAmount,\n                        b.OrderId)).ToList());", Normalize(identity), StringComparison.Ordinal);
        Assert.Contains("decimal? ChangeAmount,\n    int? OrderId = null);", Normalize(gatewayContracts), StringComparison.Ordinal);
        Assert.Contains("string CustomerName,\n    int? OrderId = null);", Normalize(cashierContracts), StringComparison.Ordinal);
        Assert.Contains("bill.CustomerName ?? string.Empty,\n        bill.OrderId);", Normalize(cashier), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Frontend/selfrestaurant-chef-web/src/pages/DashboardPage.tsx")]
    [InlineData("src/Frontend/selfrestaurant-cashier-web/src/pages/DashboardPage.tsx")]
    [InlineData("src/Frontend/selfrestaurant-cashier-web/src/pages/HistoryPage.tsx")]
    [InlineData("src/Frontend/selfrestaurant-cashier-web/src/pages/ReportPage.tsx")]
    [InlineData("src/Frontend/selfrestaurant-admin-web/src/pages/customers/CustomerActivityLogsPage.tsx")]
    [InlineData("src/Frontend/selfrestaurant-admin-web/src/pages/employees/EmployeesModulePage.tsx")]
    public void Staff_pages_render_orders_with_short_label(string page)
    {
        var source = Read(page);
        Assert.Contains("orderLabel(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("`ORD${", source, StringComparison.Ordinal);
        Assert.DoesNotContain("`ORDER-${", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>{bill.orderCode}</td>", source, StringComparison.Ordinal);
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SelfRestaurant.Microservices.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
