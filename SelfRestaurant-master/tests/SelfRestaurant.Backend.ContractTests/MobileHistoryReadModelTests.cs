namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileHistoryReadModelTests
{
    [Fact]
    public void Customer_invoice_history_is_customer_scoped_by_gateway_actor_and_billing_owner()
    {
        var gateway = Read("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileBillingGatewayController.cs");
        var billing = Read("src/Services/SelfRestaurant.Billing.Api/Controllers/MobileCheckoutController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", gateway, StringComparison.Ordinal);
        Assert.Contains("_billing.GetCustomerInvoicesAsync(actor.Value, limit, ct)", gateway, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"invoices\")]", billing, StringComparison.Ordinal);
        Assert.Contains("Where(x => x.CustomerId == customerId)", billing, StringComparison.Ordinal);
        Assert.Contains("branchId = quote?.BranchId, tableId = quote?.TableId", billing, StringComparison.Ordinal);
        Assert.Contains("paymentStatus = payment?.Status", billing, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_history_enriches_customer_owned_orders_with_orders_owned_session_metadata()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        Assert.Contains("_db.Orders.AsNoTracking().Where(x => x.CustomerID == customerId)", source, StringComparison.Ordinal);
        Assert.Contains("sessionCodes.Contains(x.SessionCode)", source, StringComparison.Ordinal);
        Assert.Contains("session.StartedAtUtc, session.EndedAtUtc", source, StringComparison.Ordinal);
        Assert.Contains("DiningSessionSummaryResponse? DiningSession = null", source, StringComparison.Ordinal);
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
