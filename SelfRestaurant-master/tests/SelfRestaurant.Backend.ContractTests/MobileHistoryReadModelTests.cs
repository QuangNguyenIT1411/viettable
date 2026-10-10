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

    [Fact]
    public void Order_page_batches_items_statuses_and_remote_catalog_before_mapping()
    {
        var source = Read("src/Services/SelfRestaurant.Orders.Api/Controllers/DiningSessionsController.cs");
        var list = source.Split("[HttpGet(\"mobile-orders\")]")[1].Split("[HttpGet(\"mobile-orders/{orderId:int}\")]")[0];
        Assert.Contains("orderIds.Contains(x.OrderID)", list, StringComparison.Ordinal);
        Assert.Contains("statusIds.Contains(x.StatusID)", list, StringComparison.Ordinal);
        Assert.Equal(1, list.Split("_catalog.GetDishesAsync").Length - 1);
        Assert.DoesNotContain("_catalog.GetDishesAsync", list.Split("foreach (var order in orders)")[1], StringComparison.Ordinal);
        Assert.Contains("itemsByOrder[order.OrderID].ToArray(), true", list, StringComparison.Ordinal);
        Assert.Contains("if (!sessionResolved)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_menu_fetches_dishes_once_not_per_category()
    {
        var source = Read("src/Services/SelfRestaurant.Catalog.Api/Controllers/CatalogController.cs");
        var menu = source.Split("[HttpGet(\"api/branches/{branchId:int}/menu\")]")[1].Split("[HttpGet(\"api/tables/qr/{code}\")]")[0];
        Assert.Contains("categoryIds.Contains(x.CategoryID)", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("await _db", menu.Split("foreach (var category in allCategories)")[1], StringComparison.Ordinal);
        Assert.Contains("x.IsActive ?? true", menu, StringComparison.Ordinal);
        Assert.Contains("x.ingredients", menu, StringComparison.Ordinal);
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
