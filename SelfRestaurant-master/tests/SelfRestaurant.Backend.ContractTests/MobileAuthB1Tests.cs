namespace SelfRestaurant.Backend.ContractTests;

public sealed class MobileAuthB1Tests
{
    [Fact]
    public void Google_mobile_and_legacy_web_have_separate_audience_configuration()
    {
        var mobile = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileAuthController.cs");
        var web = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs");
        Assert.Contains("\"Authentication:Google:ClientId\"", mobile, StringComparison.Ordinal);
        Assert.DoesNotContain("Authentication:Google:WebClientId", mobile, StringComparison.Ordinal);
        Assert.Contains("\"Authentication:Google:WebClientId\"", web, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Authentication:Google:ClientId\"", web, StringComparison.Ordinal);
    }

    [Fact]
    public void Google_keeps_verified_identity_and_password_provider_conflict_guards()
    {
        var source = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileAuthController.cs");
        Assert.Contains("GoogleJsonWebSignature.ValidateAsync", source, StringComparison.Ordinal);
        Assert.Contains("Audience = [clientId.Trim()]", source, StringComparison.Ordinal);
        Assert.Contains("payload.EmailVerified != true", source, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(payload.Subject)", source, StringComparison.Ordinal);
        Assert.Contains("\"email_provider_conflict\"", source, StringComparison.Ordinal);
        // google_ + a GUID in N format is 39 chars; slicing it to 50 throws for every new user.
        Assert.Contains("var baseUsername = $\"google_{Guid.NewGuid():N}\";", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("register")]
    [InlineData("login")]
    [InlineData("google")]
    [InlineData("refresh")]
    [InlineData("logout")]
    [InlineData("forgot-password")]
    [InlineData("reset-password")]
    [InlineData("change-password")]
    public void Gateway_exposes_frozen_mobile_auth_route(string route)
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileAuthGatewayController.cs");
        Assert.Contains("[Route(\"api/mobile/v1/auth\")]", source, StringComparison.Ordinal);
        Assert.Contains($"[HttpPost(\"{route}\")]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_password_requires_customer_bearer_role()
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/MobileAuthGatewayController.cs");
        Assert.Contains("[Authorize(Roles = \"customer\")]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_tokens_are_hashed_rotated_and_family_reuse_is_revoked()
    {
        var entity = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Persistence/Entities/MobileRefreshToken.cs");
        var controller = ReadRepoFile("src/Services/SelfRestaurant.Identity.Api/Controllers/MobileAuthController.cs");
        Assert.Contains("TokenHash", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("public string RefreshToken", entity, StringComparison.Ordinal);
        Assert.Contains("ReplacedByTokenHash", entity, StringComparison.Ordinal);
        Assert.Contains("reuse_detected", controller, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_cookie_web_auth_routes_remain_available()
    {
        var source = ReadRepoFile("src/Gateway/SelfRestaurant.Gateway.Api/Controllers/CustomerGatewayController.cs");
        Assert.Contains("[HttpPost(\"auth/login\")]", source, StringComparison.Ordinal);
        Assert.Contains("ApplyLoginSession(login);", source, StringComparison.Ordinal);
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
