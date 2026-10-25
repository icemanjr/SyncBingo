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
        IEnumerable<WinPattern> patterns,
        IEnumerable<PatternPreset>? presets = null)
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
        Presets = [.. presets ?? []];

        ValidateColumns();
        _patternsById = BuildPatternIndex();
        ValidatePresets();
    }

    public string Id { get; }

    public CardLayout Layout { get; }

    /// <summary>Balls are numbered 1 to <see cref="BallMax"/>.</summary>
    public int BallMax { get; }

    /// <summary>One range per card column, left to right; empty for free-form dealing, where any item goes anywhere.</summary>
    public ImmutableArray<ColumnRange> Columns { get; }

    /// <summary>Cards are dealt free-form (any item in any square), and balls have no column label.</summary>
    public bool IsFreeForm => Columns.IsEmpty;

    public ImmutableArray<WinPattern> Patterns { get; }

    /// <summary>Named groups of patterns, e.g. Normal Bingo = Rows + Columns + Diagonals.</summary>
    public ImmutableArray<PatternPreset> Presets { get; }

    /// <summary>
    /// Marks every new card starts with: the free cell, which always counts toward
    /// any pattern that includes it (assumption A8).
    /// </summary>
    public uint InitialMarks => Layout.FreeCellMask;

    /// <summary>
    /// The same rules on a card with no free space (FR-26 <c>None</c>): the free cell becomes an ordinary
    /// square. Patterns are unchanged, since the free cell only ever counted as called.
    /// </summary>
    public BingoVariant WithoutFreeCell() =>
        Layout.FreeCell is null ? this : new(Id, new CardLayout(Layout.Rows, Layout.Columns, freeCell: null), BallMax, Columns, Patterns, Presets);

    /// <summary>
    /// How many different cards these rules can deal (FR-39), at most <see cref="long.MaxValue"/>: free-form,
    /// the ordered picks of an item for each square; by column, the product of each column's ordered picks
    /// from its range. A grouped 3 × 3 set of 3, 2 and 3 pairs makes 3! × 2! × 3! = 72; standard 75-ball Bingo
    /// far more than anyone could play.
    /// </summary>
    public long PossibleCards
    {
        get
        {
            if (IsFreeForm)
            {
                return OrderedPicks(BallMax, Layout.CellCount - (Layout.FreeCell is null ? 0 : 1));
            }

            var total = 1L;
            for (var column = 0; column < Columns.Length; column++)
            {
                total = SaturatingMultiply(total, OrderedPicks(Columns[column].Count, Layout.Rows - FreeCellsIn(column)));
            }

            return total;
        }
    }

    /// <summary>n × (n − 1) × … for <paramref name="picks"/> factors: the ways to fill that many squares in order.</summary>
    private static long OrderedPicks(int items, int picks)
    {
        var total = 1L;
        for (var i = 0; i < picks; i++)
        {
            total = SaturatingMultiply(total, items - i);
        }

        return total;
    }

    private static long SaturatingMultiply(long a, long b) => b != 0 && a > long.MaxValue / b ? long.MaxValue : a * b;

    public WinPattern GetPattern(string id) =>
        _patternsById.TryGetValue(id, out var pattern)
            ? pattern
            : throw new KeyNotFoundException($"Variant '{Id}' has no win pattern '{id}'.");

    public bool TryGetPattern(string id, out WinPattern? pattern) =>
        _patternsById.TryGetValue(id, out pattern);

    /// <summary>
    /// Reads a selection key: pattern ids joined by "+" ("rows+corners"), where a preset's id stands for
    /// its patterns ("normal" = "rows+columns+diagonals", which also keeps sessions saved before
    /// selections existed working). Order and repeats don't matter; the result's key is canonical.
    /// </summary>
    public bool TryGetSelection(string key, out WinSelection? selection)
    {
        selection = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in key.Split(WinSelection.Separator))
        {
            var preset = Presets.FirstOrDefault(p => p.Id == part);
            if (preset is not null)
            {
                ids.UnionWith(preset.PatternIds);
            }
            else if (_patternsById.ContainsKey(part))
            {
                ids.Add(part);
            }
            else
            {
                return false;
            }
        }

        selection = new WinSelection([.. Patterns.Where(pattern => ids.Contains(pattern.Id))]);
        return true;
    }

    public WinSelection GetSelection(string key) =>
        TryGetSelection(key, out var selection)
            ? selection!
            : throw new KeyNotFoundException($"Variant '{Id}' has no win pattern selection '{key}'.");

    /// <summary>The label shown with a ball: its column's, e.g. "N" for 37; empty when dealing is free-form.</summary>
    public string LabelOf(int ball) => IsFreeForm ? string.Empty : Columns[ColumnOf(ball)].Label;

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
        if (IsFreeForm)
        {
            var cellsToFill = Layout.CellCount - (Layout.FreeCell is null ? 0 : 1);
            if (BallMax < cellsToFill)
            {
                throw new ArgumentException($"Free-form dealing needs at least {cellsToFill} balls to fill a card.", "columns");
            }

            return;
        }

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

    private void ValidatePresets()
    {
        foreach (var preset in Presets)
        {
            if (preset.PatternIds.IsEmpty || preset.PatternIds.Any(id => !_patternsById.ContainsKey(id)))
            {
                throw new ArgumentException($"Preset '{preset.Id}' must name patterns of this variant.", "presets");
            }

            if (_patternsById.ContainsKey(preset.Id))
            {
                throw new ArgumentException($"Preset '{preset.Id}' has the same id as a pattern.", "presets");
            }
        }
    }

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
