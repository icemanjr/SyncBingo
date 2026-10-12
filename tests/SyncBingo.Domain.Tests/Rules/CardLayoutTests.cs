using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Rules;

public sealed class CardLayoutTests
{
    private static readonly CardLayout FiveByFive = new(5, 5, freeCell: 12);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 4, 4)]
    [InlineData(2, 2, 12)]
    [InlineData(4, 0, 20)]
    [InlineData(4, 4, 24)]
    public void Cells_are_numbered_row_major(int row, int column, int index)
    {
        FiveByFive.CellIndex(row, column).ShouldBe(index);
        FiveByFive.CellMask(row, column).ShouldBe(Cells.Of(index));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    public void CellIndex_rejects_positions_off_the_card(int row, int column)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => FiveByFive.CellIndex(row, column));
    }

    [Fact]
    public void AllCells_covers_exactly_the_card()
    {
        FiveByFive.AllCells.ShouldBe(Cells.Of([.. Enumerable.Range(0, 25)]));
        new CardLayout(4, 8, freeCell: null).AllCells.ShouldBe(uint.MaxValue);
    }

    [Fact]
    public void FreeCellMask_is_empty_without_a_free_cell()
    {
        FiveByFive.FreeCellMask.ShouldBe(Cells.Of(12));
        new CardLayout(5, 5, freeCell: null).FreeCellMask.ShouldBe(0u);
    }

    [Fact]
    public void A_rectangular_card_has_no_diagonals()
    {
        var lines = new CardLayout(2, 3, freeCell: null).Lines().ToList();

        lines.ShouldBe(
        [
            Cells.Of(0, 1, 2),
            Cells.Of(3, 4, 5),
            Cells.Of(0, 3),
            Cells.Of(1, 4),
            Cells.Of(2, 5),
        ]);
    }

    [Fact]
    public void ParseShape_reads_X_as_a_required_cell()
    {
        var mask = FiveByFive.ParseShape(
            "X....",
            ".X...",
            ".....",
            ".....",
            "....X");

        mask.ShouldBe(Cells.Of(0, 6, 24));
    }

    [Fact]
    public void ParseShape_rejects_the_wrong_number_of_rows()
    {
        Should.Throw<ArgumentException>(() => FiveByFive.ParseShape("XXXXX"));
    }

    [Fact]
    public void ParseShape_rejects_a_row_of_the_wrong_length()
    {
        Should.Throw<ArgumentException>(() => FiveByFive.ParseShape("XXXX", ".....", ".....", ".....", "....."));
    }

    [Fact]
    public void ParseShape_rejects_unknown_characters()
    {
        Should.Throw<ArgumentException>(() => FiveByFive.ParseShape("XXXXO", ".....", ".....", ".....", "....."));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(6, 6)]
    public void Rejects_impossible_sizes(int rows, int columns)
    {
        Should.Throw<ArgumentException>(() => new CardLayout(rows, columns, freeCell: null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void Rejects_a_free_cell_off_the_card(int freeCell)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CardLayout(5, 5, freeCell));
    }
}
