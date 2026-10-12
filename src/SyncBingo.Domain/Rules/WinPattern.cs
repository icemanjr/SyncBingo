using System.Collections.Immutable;

namespace SyncBingo.Domain.Rules;

/// <summary>
/// A win condition expressed as data. A card satisfies the pattern when its marks
/// cover every cell of at least one mask: <c>(marks &amp; mask) == mask</c>.
/// </summary>
public sealed class WinPattern
{
    public WinPattern(string id, string name, IEnumerable<uint> masks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(masks);

        var all = masks.ToImmutableArray();
        if (all.IsEmpty)
        {
            throw new ArgumentException("A pattern needs at least one mask.", nameof(masks));
        }

        if (all.Contains(0u))
        {
            throw new ArgumentException("Every mask must require at least one cell.", nameof(masks));
        }

        Id = id;
        Name = name;
        Masks = all;
    }

    /// <summary>Stable identifier used by the API, e.g. <c>normal</c> or <c>B</c>.</summary>
    public string Id { get; }

    /// <summary>Display name, e.g. <c>Normal Bingo</c>.</summary>
    public string Name { get; }

    /// <summary>Alternative ways to win; satisfying any one of them is enough.</summary>
    public ImmutableArray<uint> Masks { get; }

    public bool IsSatisfiedBy(uint marks)
    {
        foreach (var mask in Masks)
        {
            if ((marks & mask) == mask)
            {
                return true;
            }
        }

        return false;
    }
}
