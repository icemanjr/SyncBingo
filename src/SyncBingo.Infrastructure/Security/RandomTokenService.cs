using System.Security.Cryptography;
using System.Text;
using SyncBingo.Application.Ports;

namespace SyncBingo.Infrastructure.Security;

/// <summary>
/// Session tokens (ADR 0005): 256 random bits, URL-safe base64 for the client; only the
/// SHA-256 is stored. A fast hash is right here, unlike for passwords: the token is already
/// unguessable, so there is nothing to slow down.
/// </summary>
public sealed class RandomTokenService : ITokenService
{
    private const int TokenBytes = 32;

    public IssuedToken Issue()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(TokenBytes));
        return new IssuedToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
