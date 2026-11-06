using System.Globalization;
using System.Security.Cryptography;

namespace JeepneyFareCalculator.Application.Administrators;

/// <summary>
/// Hashes and checks the Administrator's password with PBKDF2 (HMAC-SHA256).
/// A hash is written as <c>PBKDF2-SHA256.&lt;iterations&gt;.&lt;salt&gt;.&lt;key&gt;</c>,
/// with the salt and key in Base64. The iteration count is stored in the hash,
/// so it can be raised later without breaking hashes made earlier.
/// </summary>
public static class PasswordHashing
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const int Iterations = 600_000; // OWASP's recommendation for PBKDF2-HMAC-SHA256.
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('.',
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    /// <summary>
    /// Returns whether <paramref name="password"/> matches <paramref name="hash"/>.
    /// Throws if the hash isn't one that <see cref="Hash"/> made, because that is a
    /// configuration mistake, not a wrong password.
    /// </summary>
    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 4
            || parts[0] != Algorithm
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations < 1
            || !TryFromBase64(parts[2], out var salt)
            || !TryFromBase64(parts[3], out var expectedKey))
        {
            throw new InvalidOperationException(
                "The Administrator password hash is not in the expected format. " +
                "Generate a new one with the hash-password command (see back-end/CLAUDE.md).");
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    private static bool TryFromBase64(string text, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(text);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
