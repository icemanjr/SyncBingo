using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Cards;

/// <summary>
/// Deals cards the standard way: each column draws distinct numbers from its own
/// range (B = 1–15, I = 16–30, …), placed in random order (not sorted), and the
/// free cell holds <see cref="Card.FreeCellNumber"/>.
/// </summary>
public sealed class ColumnCardGenerator : ICardGenerator
{
    private readonly BingoVariant _variant;

    public ColumnCardGenerator(BingoVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        _variant = variant;
    }

    public Card Generate(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var layout = _variant.Layout;
        var cells = new int[layout.CellCount];

        for (var column = 0; column < layout.Columns; column++)
        {
            var range = _variant.Columns[column];
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
}
