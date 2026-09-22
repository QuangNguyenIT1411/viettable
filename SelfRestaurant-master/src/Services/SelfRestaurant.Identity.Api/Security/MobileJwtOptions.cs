namespace SelfRestaurant.Identity.Api.Security;

public sealed class MobileJwtOptions
{
    public const string SectionName = "MobileJwt";
    public string Issuer { get; set; } = "SelfRestaurant.Identity";
    public string Audience { get; set; } = "SelfRestaurant.Mobile";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
