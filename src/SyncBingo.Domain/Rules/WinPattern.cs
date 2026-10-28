using System.Collections.Immutable;

namespace SyncBingo.Domain.Rules;

/// <summary>
/// A win condition expressed as data. A card satisfies the pattern when its marks
/// cover every cell of at least one mask: <c>(marks &amp; mask) == mask</c>.
/// </summary>
public sealed class WinPattern
{
    public WinPattern(string id, string name, IEnumerable<uint> masks, bool isLine = false, string? announcedAs = null)
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
        IsLine = isLine;
        AnnouncedAs = announcedAs ?? name;
    }

    /// <summary>Stable identifier used by the API, e.g. <c>corners</c> or <c>B</c>.</summary>
    public string Id { get; }

    /// <summary>Display name, e.g. <c>Four Corners</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// Straight lines (rows, columns, diagonals): a win on one is announced as "a Bingo", while a
    /// shape such as Four Corners is announced by name ("Ava had Four Corners!").
    /// </summary>
    public bool IsLine { get; }

    /// <summary>How a win on it is announced: "Ava had <c>Four Corners</c>!", "Ava had <c>the letter B</c>!".</summary>
    public string AnnouncedAs { get; }

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
