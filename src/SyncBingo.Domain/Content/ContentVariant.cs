using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Content;

/// <summary>
/// The rules a game plays with a custom set (ADR 0009): the standard card, patterns and presets, with the
/// set's items as the balls. A set with groups is dealt by group, each card column drawing from one group as
/// B-I-N-G-O columns draw from their ranges; a set without groups is dealt free-form.
/// </summary>
public static class ContentVariant
{
    public const string Id = "custom";

    /// <summary>
    /// Builds the variant, or says why the set doesn't fit the card: never altering the set to make it fit.
    /// </summary>
    public static Result<BingoVariant> For(BingoVariant standard, ContentSet set)
    {
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(set);

        var layout = standard.Layout;
        var cellsToFill = layout.CellCount - (layout.FreeCell is null ? 0 : 1);
        if (set.Pairs.Length < cellsToFill)
        {
            return Refuse($"This card needs at least {cellsToFill} entries; the set has {set.Pairs.Length}.");
        }

        List<ColumnRange> columns = [];
        if (!set.Groups.IsEmpty)
        {
            if (!set.IsFullyGrouped)
            {
                return Refuse("A set with groups needs every entry in a group.");
            }

            if (set.Groups.Length != layout.Columns)
            {
                return Refuse($"A set with groups needs {layout.Columns} groups, one for each card column; this set has {set.Groups.Length}.");
            }

            // Pairs are kept in group order (ContentSet), so each group is a run of item ids.
            var first = 1;
            for (var column = 0; column < layout.Columns; column++)
            {
                var name = set.Groups[column];
                var count = set.Pairs.Count(pair => pair.Group == name);
                var needed = layout.Rows - (layout.FreeCell is { } free && free % layout.Columns == column ? 1 : 0);
                if (count < needed)
                {
                    return Refuse($"Group '{name}' has {count} entries; its column needs at least {needed}.");
                }

                columns.Add(new ColumnRange(name, first, first + count - 1));
                first += count;
            }
        }

        return Result.Success(new BingoVariant(Id, layout, set.Pairs.Length, columns, standard.Patterns, standard.Presets));
    }

    private static Result<BingoVariant> Refuse(string message) => Result.Failure<BingoVariant>(DomainErrorCode.Invalid, message);
}
