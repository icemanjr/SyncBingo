using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Rules;

/// <summary>Several patterns at once (FR-21): selection keys, how a card wins, and what a win is announced as.</summary>
public sealed class WinSelectionTests
{
    private static readonly BingoVariant Variant = StandardVariants.Classic75;
    private static readonly uint TopRow = Cells.Of(0, 1, 2, 3, 4);
    private static readonly uint ColumnB = Cells.Of(0, 5, 10, 15, 20);
    private static readonly uint Corners = Cells.Of(0, 4, 20, 24);

    [Fact]
    public void Normal_Bingo_is_rows_columns_and_diagonals()
    {
        Variant.GetSelection("normal").Key.ShouldBe("rows+columns+diagonals");
        Variant.GetPattern("rows").Masks.Length.ShouldBe(5);
        Variant.GetPattern("columns").Masks.Length.ShouldBe(5);
        Variant.GetPattern("diagonals").Masks.Length.ShouldBe(2);
    }

    [Theory]
    [InlineData("corners+rows", "rows+corners")]
    [InlineData("rows+rows+corners", "rows+corners")]
    [InlineData("normal+corners", "rows+columns+diagonals+corners")]
    [InlineData("blackout", "blackout")]
    public void Keys_are_canonical_whatever_the_order(string given, string canonical)
    {
        Variant.GetSelection(given).Key.ShouldBe(canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rows+")]
    [InlineData("rows+stars")]
    [InlineData("Rows")]
    public void Unknown_or_empty_keys_are_refused(string key)
    {
        Variant.TryGetSelection(key, out _).ShouldBeFalse();
    }

    [Fact]
    public void Any_selected_pattern_wins_and_others_do_not()
    {
        var rowsOrCorners = Variant.GetSelection("rows+corners");

        rowsOrCorners.MatchCount(TopRow).ShouldBe(1);
        rowsOrCorners.MatchCount(Corners).ShouldBe(1);
        rowsOrCorners.MatchCount(ColumnB).ShouldBe(0, "columns aren't selected");
        rowsOrCorners.MatchCount(TopRow | Corners).ShouldBe(2);
    }

    [Fact]
    public void A_line_is_announced_as_a_Bingo()
    {
        Variant.GetSelection("normal+corners").Announced(TopRow).ShouldBeNull();
        Variant.GetSelection("normal").Announced(TopRow | ColumnB).ShouldBeNull("a double Bingo is still a Bingo");
    }

    [Fact]
    public void A_shape_alone_is_announced_by_name()
    {
        Variant.GetSelection("normal+corners").Announced(Corners).ShouldBe("corners");
        Variant.GetSelection("blackout").Announced(Variant.Layout.AllCells).ShouldBe("blackout");
    }

    [Fact]
    public void A_line_and_a_shape_together_stay_a_Bingo_so_nothing_undeclared_is_named()
    {
        // The top row and the corners share cells 0 and 4: Ava might have declared either.
        Variant.GetSelection("rows+corners").Announced(TopRow | Corners).ShouldBeNull();
        Variant.GetSelection("rows+corners").Completed(TopRow | Corners).Select(p => p.Id).ShouldBe(["rows", "corners"]);
    }

    [Fact]
    public void Shapes_say_how_they_are_announced()
    {
        Variant.GetPattern("corners").AnnouncedAs.ShouldBe("Four Corners");
        Variant.GetPattern("B").AnnouncedAs.ShouldBe("the letter B");
        Variant.GetPattern("B").Name.ShouldBe("Letter B");
    }

    [Fact]
    public void Nothing_complete_is_announced_as_a_Bingo_the_host_awarded_anyway()
    {
        Variant.GetSelection("corners").Announced(0).ShouldBeNull();
    }
}
