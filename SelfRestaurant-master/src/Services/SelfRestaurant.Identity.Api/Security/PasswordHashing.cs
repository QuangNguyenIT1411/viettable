using System.Security.Cryptography;

namespace SelfRestaurant.Identity.Api.Security;

internal static class PasswordHashing
{
    private const string Scheme = "pbkdf2";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string storedValue, string providedPassword, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return false;
        }

        if (!storedValue.StartsWith($"{Scheme}$", StringComparison.Ordinal))
        {
            needsUpgrade = string.Equals(storedValue, providedPassword, StringComparison.Ordinal);
            return needsUpgrade;
        }

        var parts = storedValue.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                providedPassword,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
