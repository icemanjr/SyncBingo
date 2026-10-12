using System.Collections.Immutable;

namespace SyncBingo.Domain.Cards;

/// <summary>
/// The numbers printed on a card, row-major, with <see cref="FreeCellNumber"/> in
/// the free cell. Two cards are equal when every cell holds the same number.
/// </summary>
public sealed class Card : IEquatable<Card>
{
    public const int FreeCellNumber = 0;

    public Card(IEnumerable<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Cells = [.. cells];
        if (Cells.IsEmpty)
        {
            throw new ArgumentException("A card needs at least one cell.", nameof(cells));
        }

        Fingerprint = string.Join('-', Cells);
    }

    public ImmutableArray<int> Cells { get; }

    /// <summary>
    /// Identifies the card's exact layout, e.g. <c>7-22-38-…</c>. Used to make sure
    /// no two cards in play in a session are the same.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>The cell holding <paramref name="ball"/>, or <c>null</c> if the card does not have it.</summary>
    public int? CellOf(int ball)
    {
        var index = Cells.IndexOf(ball);
        return ball != FreeCellNumber && index >= 0 ? index : null;
    }

    public bool Equals(Card? other) => other is not null && Fingerprint == other.Fingerprint;

    public override bool Equals(object? obj) => Equals(obj as Card);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint);

    public override string ToString() => Fingerprint;
}
