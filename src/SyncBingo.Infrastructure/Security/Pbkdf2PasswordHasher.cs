using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SyncBingo.Application.Ports;

namespace SyncBingo.Infrastructure.Security;

/// <summary>
/// The game password, salted and stretched with PBKDF2-HMAC-SHA256 (docs/04 §5). The stored
/// form records the algorithm and iteration count, <c>pbkdf2-sha256$100000$salt$hash</c>, so the
/// cost can rise later without breaking games already running.
/// </summary>
public sealed class Pbkdf2PasswordHasher(int iterations = Pbkdf2PasswordHasher.DefaultIterations) : IPasswordHasher
{
    /// <summary>
    /// Below OWASP's 600,000 for account passwords on purpose: this guards a short-lived game
    /// password, every join verifies it, and a room of 100 joining at once must not stall the
    /// API (docs/04 §5). Joins are rate-limited as well.
    /// </summary>
    public const int DefaultIterations = 100_000;

    private const string Algorithm = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, iterations);
        return string.Join('$', Algorithm, iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);

        var parts = hash.Split('$');
        if (parts.Length != 4
            || parts[0] != Algorithm
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var storedIterations)
            || storedIterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derive(password, salt, storedIterations, expected.Length), expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, length);
}
