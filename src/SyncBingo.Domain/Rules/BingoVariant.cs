using System.Collections.Immutable;

namespace SyncBingo.Domain.Rules;

/// <summary>The balls a card column draws from, e.g. B = 1–15.</summary>
public readonly record struct ColumnRange(string Label, int Min, int Max)
{
    public int Count => Max - Min + 1;

    public bool Contains(int ball) => ball >= Min && ball <= Max;
}

/// <summary>
/// A complete rule set: card layout, ball range, column ranges and the win
/// patterns a host can choose from. Standard 75-ball Bingo is one instance
/// (<see cref="StandardVariants.Classic75"/>); other games are other data.
/// </summary>
public sealed class BingoVariant
{
    private readonly ImmutableDictionary<string, WinPattern> _patternsById;

    public BingoVariant(
        string id,
        CardLayout layout,
        int ballMax,
        IEnumerable<ColumnRange> columns,
        IEnumerable<WinPattern> patterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfLessThan(ballMax, 1);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(patterns);

        Id = id;
        Layout = layout;
        BallMax = ballMax;
        Columns = [.. columns];
        Patterns = [.. patterns];

        ValidateColumns();
        _patternsById = BuildPatternIndex();
    }

    public string Id { get; }

    public CardLayout Layout { get; }

    /// <summary>Balls are numbered 1 to <see cref="BallMax"/>.</summary>
    public int BallMax { get; }

    /// <summary>One range per card column, left to right.</summary>
    public ImmutableArray<ColumnRange> Columns { get; }

    public ImmutableArray<WinPattern> Patterns { get; }

    /// <summary>
    /// Marks every new card starts with: the free cell, which always counts toward
    /// any pattern that includes it (assumption A8).
    /// </summary>
    public uint InitialMarks => Layout.FreeCellMask;

    public WinPattern GetPattern(string id) =>
        _patternsById.TryGetValue(id, out var pattern)
            ? pattern
            : throw new KeyNotFoundException($"Variant '{Id}' has no win pattern '{id}'.");

    public bool TryGetPattern(string id, out WinPattern? pattern) =>
        _patternsById.TryGetValue(id, out pattern);

    /// <summary>The column a ball belongs to, e.g. 37 is column 2 (N) in 75-ball Bingo.</summary>
    public int ColumnOf(int ball)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ball, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ball, BallMax);

        for (var column = 0; column < Columns.Length; column++)
        {
            if (Columns[column].Contains(ball))
            {
                return column;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(ball), ball, $"Ball {ball} is not in any column of variant '{Id}'.");
    }

    private void ValidateColumns()
    {
        if (Columns.Length != Layout.Columns)
        {
            throw new ArgumentException($"Expected {Layout.Columns} column ranges but got {Columns.Length}.", "columns");
        }

        for (var column = 0; column < Columns.Length; column++)
        {
            var range = Columns[column];
            if (range.Min < 1 || range.Max > BallMax || range.Min > range.Max)
            {
                throw new ArgumentException($"Column {range.Label} must be a range within 1–{BallMax}.", "columns");
            }

            if (column > 0 && range.Min <= Columns[column - 1].Max)
            {
                throw new ArgumentException("Column ranges must be in ascending order and must not overlap.", "columns");
            }

            var cellsToFill = Layout.Rows - FreeCellsIn(column);
            if (range.Count < cellsToFill)
            {
                throw new ArgumentException($"Column {range.Label} needs at least {cellsToFill} balls to fill a card.", "columns");
            }
        }
    }

    private int FreeCellsIn(int column) =>
        Layout.FreeCell is { } free && free % Layout.Columns == column ? 1 : 0;

    private ImmutableDictionary<string, WinPattern> BuildPatternIndex()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, WinPattern>(StringComparer.Ordinal);
        foreach (var pattern in Patterns)
        {
            if (pattern.Masks.Any(mask => (mask & ~Layout.AllCells) != 0))
            {
                throw new ArgumentException($"Pattern '{pattern.Id}' uses cells outside the card.", "patterns");
            }

            if (!builder.TryAdd(pattern.Id, pattern))
            {
                throw new ArgumentException($"Duplicate pattern id '{pattern.Id}'.", "patterns");
            }
        }

        return builder.ToImmutable();
    }
}
