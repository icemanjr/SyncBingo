using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Cards;

/// <summary>
/// Deals any item into any square (ADR 0009): distinct items 1…<see cref="BingoVariant.BallMax"/> in random
/// cells, for content without columns, such as a word list. The free cell holds <see cref="Card.FreeCellNumber"/>.
/// </summary>
public sealed class FreeFormCardGenerator : ICardGenerator
{
    private readonly BingoVariant _variant;

    public FreeFormCardGenerator(BingoVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        _variant = variant;
    }

    public Card Generate(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var layout = _variant.Layout;
        var pool = Enumerable.Range(1, _variant.BallMax).ToArray();
        var cells = new int[layout.CellCount];
        var drawn = 0;
        for (var cell = 0; cell < cells.Length; cell++)
        {
            if (cell == layout.FreeCell)
            {
                cells[cell] = Card.FreeCellNumber;
                continue;
            }

            // Partial Fisher–Yates over all items: each cell gets a uniformly chosen item not used yet.
            var pick = drawn + random.NextInt(pool.Length - drawn);
            (pool[drawn], pool[pick]) = (pool[pick], pool[drawn]);
            cells[cell] = pool[drawn];
            drawn++;
        }

        return new Card(cells);
    }
}

/// <summary>The dealer a variant needs: by column, or free-form.</summary>
public static class CardGenerators
{
    public static ICardGenerator For(BingoVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.IsFreeForm ? new FreeFormCardGenerator(variant) : new ColumnCardGenerator(variant);
    }
}
