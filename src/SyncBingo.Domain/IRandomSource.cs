using System.Security.Cryptography;

namespace SyncBingo.Domain;

/// <summary>
/// Source of randomness for dealing cards and drawing balls. Production uses
/// <see cref="CryptoRandomSource"/>; tests inject a seeded source so results repeat.
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns a uniformly distributed integer in <c>[0, maxExclusive)</c>.</summary>
    int NextInt(int maxExclusive);
}

/// <summary>Cryptographically secure randomness, so players cannot predict cards or balls.</summary>
public sealed class CryptoRandomSource : IRandomSource
{
    public static CryptoRandomSource Instance { get; } = new();

    public int NextInt(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
