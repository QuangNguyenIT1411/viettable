namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileProfileB2Tests
{
    [Theory]
    [InlineData("[HttpGet]")]
    [InlineData("[HttpPut]")]
    [InlineData("[HttpGet(\"loyalty\")]")]
    [InlineData("[HttpGet(\"settings\")]")]
    [InlineData("[HttpPut(\"settings\")]")]
    public void Gateway_exposes_B2_mobile_profile_routes(string action)
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileProfileGatewayController.cs");
        Assert.Contains("[Route(\"api/mobile/v1/me\")]", source, StringComparison.Ordinal);
        Assert.Contains(action, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_B2_route_requires_customer_bearer_role()
    {
        var gateway = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileProfileGatewayController.cs");
        var identity = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileProfileController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", gateway, StringComparison.Ordinal);
        Assert.Contains("[Authorize(Roles = \"customer\")]", identity, StringComparison.Ordinal);
        Assert.Contains("JwtRegisteredClaimNames.Sub", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("request.CustomerId", identity, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settings_are_owned_by_Identity_and_branch_is_validated_through_Catalog()
    {
        var context = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Persistence/Context/IdentityDbContext.cs");
        var controller = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileProfileController.cs");
        Assert.Contains("DbSet<CustomerSettings>", context, StringComparison.Ordinal);
        Assert.Contains("_catalog.GetBranchAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Branches", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Loyalty_uses_the_customer_point_ledger_and_declares_all_tier_thresholds()
    {
        var source = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileProfileController.cs");
        Assert.Contains("customer.LoyaltyPoints", source, StringComparison.Ordinal);
        Assert.Contains(">= 20000", source, StringComparison.Ordinal);
        Assert.Contains(">= 5000", source, StringComparison.Ordinal);
        Assert.Contains(">= 1000", source, StringComparison.Ordinal);
        Assert.Contains("\"MEMBER\"", source, StringComparison.Ordinal);
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
