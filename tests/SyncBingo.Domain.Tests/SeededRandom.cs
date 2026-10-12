namespace SyncBingo.Domain.Tests;

/// <summary>Repeatable randomness for tests.</summary>
internal sealed class SeededRandom(int seed) : IRandomSource
{
#pragma warning disable CA5394 // Predictable on purpose: tests must be repeatable.
    private readonly Random _random = new(seed);

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
#pragma warning restore CA5394
}
