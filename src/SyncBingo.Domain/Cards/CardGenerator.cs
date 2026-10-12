using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Cards;

/// <summary>Deals random cards for a <see cref="BingoVariant"/>.</summary>
public static class CardGenerator
{
    /// <summary>
    /// How many times <see cref="GenerateUnique"/> redraws before giving up. A clash
    /// is astronomically rare for real variants, so hitting this means the variant
    /// has (almost) run out of distinct cards.
    /// </summary>
    public const int MaxAttempts = 1000;

    /// <summary>
    /// Deals one card. Each column gets distinct numbers from its range, placed in
    /// random order (not sorted), and the free cell holds <see cref="Card.FreeCellNumber"/>.
    /// </summary>
    public static Card Generate(BingoVariant variant, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(random);

        var layout = variant.Layout;
        var cells = new int[layout.CellCount];

        for (var column = 0; column < layout.Columns; column++)
        {
            var range = variant.Columns[column];
            var pool = Enumerable.Range(range.Min, range.Count).ToArray();
            var drawn = 0;

            for (var row = 0; row < layout.Rows; row++)
            {
                var cell = layout.CellIndex(row, column);
                if (cell == layout.FreeCell)
                {
                    cells[cell] = Card.FreeCellNumber;
                    continue;
                }

                // Partial Fisher–Yates: pick uniformly from the numbers not yet used.
                var pick = drawn + random.NextInt(pool.Length - drawn);
                (pool[drawn], pool[pick]) = (pool[pick], pool[drawn]);
                cells[cell] = pool[drawn];
                drawn++;
            }
        }

        return new Card(cells);
    }

    /// <summary>
    /// Deals a card that is not already in play. <paramref name="tryClaim"/> is asked
    /// about each candidate and must return <c>true</c> only if the card is free and
    /// it has now reserved it (for example an atomic add to a set of fingerprints),
    /// so two concurrent deals can never both receive the same card.
    /// </summary>
    /// <exception cref="InvalidOperationException">No unclaimed card was found in <see cref="MaxAttempts"/> tries.</exception>
    public static Card GenerateUnique(BingoVariant variant, IRandomSource random, Func<Card, bool> tryClaim)
    {
        ArgumentNullException.ThrowIfNull(tryClaim);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var card = Generate(variant, random);
            if (tryClaim(card))
            {
                return card;
            }
        }

        throw new InvalidOperationException(
            $"Could not deal a card that is not already in play after {MaxAttempts} attempts (variant '{variant.Id}').");
    }
}
