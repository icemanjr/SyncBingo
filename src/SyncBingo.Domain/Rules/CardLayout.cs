namespace SyncBingo.Domain.Rules;

/// <summary>
/// The shape of a card: its grid size and optional free cell. Cells are numbered
/// row-major (<c>index = row * Columns + column</c>), and bit <c>index</c> of a
/// <see cref="uint"/> mask stands for that cell, so a card holds at most 32 cells.
/// </summary>
public sealed class CardLayout
{
    public const int MaxCells = 32;

    public CardLayout(int rows, int columns, int? freeCell)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        if (rows * columns > MaxCells)
        {
            throw new ArgumentException($"A card can have at most {MaxCells} cells.", nameof(columns));
        }

        if (freeCell is { } free && (free < 0 || free >= rows * columns))
        {
            throw new ArgumentOutOfRangeException(nameof(freeCell), freeCell, "The free cell must be on the card.");
        }

        Rows = rows;
        Columns = columns;
        FreeCell = freeCell;
    }

    public int Rows { get; }

    public int Columns { get; }

    public int? FreeCell { get; }

    public int CellCount => Rows * Columns;

    /// <summary>Mask with every cell on the card set.</summary>
    public uint AllCells => CellCount == MaxCells ? uint.MaxValue : (1u << CellCount) - 1;

    /// <summary>Mask with only the free cell set, or 0 when the card has none.</summary>
    public uint FreeCellMask => FreeCell is { } free ? 1u << free : 0;

    public int CellIndex(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        return (row * Columns) + column;
    }

    public uint CellMask(int row, int column) => 1u << CellIndex(row, column);

    /// <summary>
    /// Every straight line on the card: each row, each column and, on a square
    /// card, both diagonals. On a 5×5 card that is the 12 lines of Normal Bingo.
    /// </summary>
    public IEnumerable<uint> Lines() => [.. RowLines(), .. ColumnLines(), .. DiagonalLines()];

    /// <summary>Each row, top to bottom.</summary>
    public IEnumerable<uint> RowLines()
    {
        for (var row = 0; row < Rows; row++)
        {
            yield return Line(column => CellMask(row, column), Columns);
        }
    }

    /// <summary>Each column, left to right.</summary>
    public IEnumerable<uint> ColumnLines()
    {
        for (var column = 0; column < Columns; column++)
        {
            yield return Line(row => CellMask(row, column), Rows);
        }
    }

    /// <summary>Both diagonals, on a square card; none otherwise.</summary>
    public IEnumerable<uint> DiagonalLines()
    {
        if (Rows == Columns)
        {
            yield return Line(i => CellMask(i, i), Rows);
            yield return Line(i => CellMask(i, Columns - 1 - i), Rows);
        }
    }

    /// <summary>
    /// Builds a mask from a picture of the card: one string per row, <c>X</c> for a
    /// required cell and <c>.</c> for any other.
    /// </summary>
    public uint ParseShape(params string[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length != Rows)
        {
            throw new ArgumentException($"Expected {Rows} rows but got {rows.Length}.", nameof(rows));
        }

        var mask = 0u;
        for (var row = 0; row < Rows; row++)
        {
            var line = rows[row];
            if (line.Length != Columns)
            {
                throw new ArgumentException($"Row {row} must have {Columns} cells: \"{line}\".", nameof(rows));
            }

            for (var column = 0; column < Columns; column++)
            {
                mask |= line[column] switch
                {
                    'X' => CellMask(row, column),
                    '.' => 0u,
                    var other => throw new ArgumentException($"Unexpected '{other}' in row {row}; use 'X' or '.'.", nameof(rows)),
                };
            }
        }

        return mask;
    }

    private static uint Line(Func<int, uint> cellAt, int length)
    {
        var mask = 0u;
        for (var i = 0; i < length; i++)
        {
            mask |= cellAt(i);
        }

        return mask;
    }
}
