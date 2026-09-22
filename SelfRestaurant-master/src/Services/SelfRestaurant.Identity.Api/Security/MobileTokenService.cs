using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Security;

public sealed class MobileTokenService
{
    private readonly MobileJwtOptions _options;

    public MobileTokenService(IOptions<MobileJwtOptions> options)
    {
        _options = options.Value;
        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("MobileJwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }
    }

    public IssuedAccessToken CreateAccessToken(Customers customer)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(_options.AccessTokenMinutes, 5, 120));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.CustomerID.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("name", customer.Name),
            new("role", "customer"),
            new("username", customer.Username),
        };
        if (!string.IsNullOrWhiteSpace(customer.Email)) claims.Add(new(JwtRegisteredClaimNames.Email, customer.Email));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires, credentials);
        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public DateTime GetRefreshExpiryUtc() => DateTime.UtcNow.AddDays(Math.Clamp(_options.RefreshTokenDays, 1, 180));

    public sealed record IssuedAccessToken(string Value, DateTime ExpiresAtUtc);
}
