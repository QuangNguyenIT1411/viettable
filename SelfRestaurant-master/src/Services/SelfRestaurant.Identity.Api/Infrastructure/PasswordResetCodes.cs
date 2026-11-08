using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SelfRestaurant.Identity.Api.Infrastructure;

/// <summary>
/// "Quên mật khẩu" from the app (owner 08/11): a 6-digit code e-mailed to the customer, typed back with the new
/// password. Only its hash is stored (in <c>PasswordResetTokens.Token</c>, prefix <c>code:</c>); 15 minutes; after
/// <see cref="MaxAttempts"/> wrong tries the code is spent and a new one must be asked for.
/// </summary>
public static class PasswordResetCodes
{
    public const string Prefix = "code:";
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private static readonly ConcurrentDictionary<int, int> Failures = new();

    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Digits(string? input) => new((input ?? "").Where(char.IsAsciiDigit).ToArray());

    /// <summary>What is stored: the hash of the customer and the code, never the code itself.</summary>
    public static string Stored(int customerId, string code) =>
        Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{customerId}:{Digits(code)}"))).ToLowerInvariant();

    public static bool Matches(string stored, int customerId, string? code)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var digits = Digits(code);
        if (digits.Length != 6) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(Stored(customerId, digits)));
    }

    /// <summary>One more wrong try for this customer; returns the count so far.</summary>
    public static int RegisterFailure(int customerId) => Failures.AddOrUpdate(customerId, 1, (_, n) => n + 1);

    public static void Clear(int customerId) => Failures.TryRemove(customerId, out _);
}
