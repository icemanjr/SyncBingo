using System.Collections.Immutable;

namespace SyncBingo.Domain.Rules;

/// <summary>
/// A named group of patterns the host can pick in one go, e.g. Normal Bingo = Rows + Columns + Diagonals.
/// Screens use it to name a selection: all three line patterns read as "Normal Bingo".
/// </summary>
public sealed record PatternPreset(string Id, string Name, ImmutableArray<string> PatternIds);

/// <summary>
/// The patterns a round is played for: one or more of the variant's patterns, any of which wins (FR-21).
/// Its <see cref="Key"/> is the selection's id everywhere a single pattern id used to be ("rows+corners"),
/// in the variant's pattern order, so the same selection always has the same key. A card can win once
/// per selection in the "once" modes, and a new selection allows another win, as a new pattern did.
/// </summary>
public sealed class WinSelection
{
    internal WinSelection(ImmutableArray<WinPattern> patterns)
    {
        Patterns = patterns;
        Key = string.Join(Separator, patterns.Select(pattern => pattern.Id));
    }

    public const char Separator = '+';

    public string Key { get; }

    /// <summary>The selected patterns, in the variant's order.</summary>
    public ImmutableArray<WinPattern> Patterns { get; }

    /// <summary>How many masks of all the selected patterns <paramref name="covered"/> completes (the "lines").</summary>
    public int MatchCount(uint covered) =>
        Patterns.Sum(pattern => pattern.Masks.Count(mask => (covered & mask) == mask));

    /// <summary>The selected patterns <paramref name="covered"/> completes at least once.</summary>
    public IReadOnlyList<WinPattern> Completed(uint covered) =>
        [.. Patterns.Where(pattern => pattern.IsSatisfiedBy(covered))];

    /// <summary>
    /// What a win is announced as: a shape's id when it is the only thing complete ("Ava had Four
    /// Corners!"), otherwise <c>null</c>, "a Bingo". A line, or two patterns at once, stays "a Bingo",
    /// so the announcement never names a pattern the player didn't declare.
    /// </summary>
    public string? Announced(uint covered) =>
        Completed(covered) is [{ IsLine: false } only] ? only.Id : null;
}
