using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Rules;

/// <summary>
/// Win patterns of standard 75-ball Bingo, checked against hand-written cell lists.
/// Cells are numbered row-major on the 5×5 card:
/// <code>
///  0  1  2  3  4
///  5  6  7  8  9
/// 10 11 12 13 14
/// 15 16 17 18 19
/// 20 21 22 23 24
/// </code>
/// </summary>
public sealed class Classic75PatternTests
{
    private static readonly BingoVariant Variant = StandardVariants.Classic75;
    private static readonly uint Free = Cells.Of(Cells.FreeCenter);

    public static TheoryData<string, int[]> NormalLines => new()
    {
        { "row 1", [0, 1, 2, 3, 4] },
        { "row 2", [5, 6, 7, 8, 9] },
        { "row 3", [10, 11, 12, 13, 14] },
        { "row 4", [15, 16, 17, 18, 19] },
        { "row 5", [20, 21, 22, 23, 24] },
        { "column B", [0, 5, 10, 15, 20] },
        { "column I", [1, 6, 11, 16, 21] },
        { "column N", [2, 7, 12, 17, 22] },
        { "column G", [3, 8, 13, 18, 23] },
        { "column O", [4, 9, 14, 19, 24] },
        { "diagonal top-left to bottom-right", [0, 6, 12, 18, 24] },
        { "diagonal top-right to bottom-left", [4, 8, 12, 16, 20] },
    };

    public static TheoryData<string, int[]> Shapes => new()
    {
        { "corners", [0, 4, 20, 24] },
        { "B", [0, 1, 2, 3, 5, 9, 10, 11, 12, 13, 15, 19, 20, 21, 22, 23] },
        { "I", [0, 1, 2, 3, 4, 7, 12, 17, 20, 21, 22, 23, 24] },
        { "N", [0, 4, 5, 6, 9, 10, 12, 14, 15, 18, 19, 20, 24] },
        { "G", [0, 1, 2, 3, 4, 5, 10, 12, 13, 14, 15, 19, 20, 21, 22, 23, 24] },
        { "O", [0, 1, 2, 3, 4, 5, 9, 10, 14, 15, 19, 20, 21, 22, 23, 24] },
        { "blackout", [.. Enumerable.Range(0, 25)] },
    };

    /// <summary>Normal Bingo is now a preset: Rows, Columns and Diagonals selected together.</summary>
    private static WinSelection Normal => Variant.GetSelection("normal");

    [Fact]
    public void Normal_has_exactly_the_twelve_lines()
    {
        var expected = NormalLines.Select(row => Cells.Of(row.Data.Item2));

        Normal.Patterns.SelectMany(pattern => pattern.Masks).ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(NormalLines))]
    public void Normal_is_won_by_each_line_alone(string line, int[] cells)
    {
        var marks = Cells.Of(cells) | Free;

        Normal.MatchCount(marks).ShouldBeGreaterThan(0, line);
    }

    [Theory]
    [MemberData(nameof(NormalLines))]
    public void Normal_is_not_won_when_a_line_misses_one_cell(string line, int[] cells)
    {
        foreach (var missing in cells.Where(cell => cell != Cells.FreeCenter))
        {
            var marks = (Cells.Of(cells) | Free) & ~Cells.Of(missing);

            Normal.MatchCount(marks).ShouldBe(0, $"{line} without cell {missing}");
        }
    }

    [Fact]
    public void Lines_through_the_center_need_only_four_marks_because_the_free_cell_counts()
    {
        var middleRowWithoutCenter = Cells.Of(10, 11, 13, 14);

        Normal.MatchCount(middleRowWithoutCenter | Variant.InitialMarks).ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Shape_requires_exactly_the_documented_cells(string id, int[] cells)
    {
        Variant.GetPattern(id).Masks.ShouldBe([Cells.Of(cells)]);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Shape_is_won_when_complete_and_not_when_any_cell_is_missing(string id, int[] cells)
    {
        var pattern = Variant.GetPattern(id);
        var complete = Cells.Of(cells) | Free;

        pattern.IsSatisfiedBy(complete).ShouldBeTrue();
        foreach (var missing in cells.Where(cell => cell != Cells.FreeCenter))
        {
            pattern.IsSatisfiedBy(complete & ~Cells.Of(missing)).ShouldBeFalse($"{id} without cell {missing}");
        }
    }

    [Fact]
    public void Extra_marks_never_prevent_a_win()
    {
        var everything = Cells.Of([.. Enumerable.Range(0, 25)]);

        Variant.Patterns.ShouldAllBe(pattern => pattern.IsSatisfiedBy(everything));
    }

    [Fact]
    public void A_fresh_card_has_only_the_free_center_marked_and_wins_nothing()
    {
        Variant.InitialMarks.ShouldBe(Free);
        Variant.Patterns.ShouldAllBe(pattern => !pattern.IsSatisfiedBy(Variant.InitialMarks));
    }

    [Fact]
    public void Offers_the_documented_win_conditions()
    {
        Variant.Patterns.Select(pattern => pattern.Id)
            .ShouldBe(["rows", "columns", "diagonals", "corners", "B", "I", "N", "G", "O", "blackout"]);
    }
}
