using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Rules;

public sealed class BingoVariantTests
{
    private static readonly BingoVariant Classic = StandardVariants.Classic75;

    [Fact]
    public void Classic75_uses_the_standard_card_and_ball_ranges()
    {
        Classic.BallMax.ShouldBe(75);
        Classic.Layout.Rows.ShouldBe(5);
        Classic.Layout.Columns.ShouldBe(5);
        Classic.Layout.FreeCell.ShouldBe(Cells.FreeCenter);
        Classic.Columns.ShouldBe(
        [
            new ColumnRange("B", 1, 15),
            new ColumnRange("I", 16, 30),
            new ColumnRange("N", 31, 45),
            new ColumnRange("G", 46, 60),
            new ColumnRange("O", 61, 75),
        ]);
    }

    [Theory]
    [InlineData(1, "B")]
    [InlineData(15, "B")]
    [InlineData(16, "I")]
    [InlineData(37, "N")]
    [InlineData(60, "G")]
    [InlineData(61, "O")]
    [InlineData(75, "O")]
    public void ColumnOf_maps_each_ball_to_its_letter(int ball, string letter)
    {
        Classic.Columns[Classic.ColumnOf(ball)].Label.ShouldBe(letter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(76)]
    public void ColumnOf_rejects_balls_outside_the_range(int ball)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Classic.ColumnOf(ball));
    }

    [Fact]
    public void GetPattern_rejects_unknown_ids()
    {
        Should.Throw<KeyNotFoundException>(() => Classic.GetPattern("zigzag"));
        Classic.TryGetPattern("zigzag", out _).ShouldBeFalse();
    }

    [Fact]
    public void Pattern_ids_are_case_sensitive()
    {
        Classic.TryGetPattern("b", out _).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_column_count_that_does_not_match_the_layout()
    {
        Should.Throw<ArgumentException>(() => Create(columns: [new("A", 1, 10)]));
    }

    [Fact]
    public void Rejects_overlapping_column_ranges()
    {
        Should.Throw<ArgumentException>(() => Create(columns: [new("A", 1, 10), new("B", 10, 20)]));
    }

    [Fact]
    public void Rejects_a_column_range_beyond_the_highest_ball()
    {
        Should.Throw<ArgumentException>(() => Create(columns: [new("A", 1, 10), new("B", 11, 21)]));
    }

    [Fact]
    public void Rejects_a_column_too_small_to_fill_the_card()
    {
        Should.Throw<ArgumentException>(() => Create(columns: [new("A", 1, 1), new("B", 11, 20)]));
    }

    [Fact]
    public void Allows_one_fewer_ball_in_the_column_holding_the_free_cell()
    {
        // 2×2 card, free cell 0 (top-left): column A only needs one ball.
        var variant = Create(layout: new CardLayout(2, 2, freeCell: 0), columns: [new("A", 1, 1), new("B", 11, 12)]);

        variant.InitialMarks.ShouldBe(Cells.Of(0));
    }

    [Fact]
    public void Rejects_duplicate_pattern_ids()
    {
        var pattern = new WinPattern("p", "P", [Cells.Of(0)]);

        Should.Throw<ArgumentException>(() => Create(patterns: [pattern, pattern]));
    }

    [Fact]
    public void Rejects_patterns_that_use_cells_outside_the_card()
    {
        Should.Throw<ArgumentException>(() => Create(patterns: [new WinPattern("p", "P", [Cells.Of(4)])]));
    }

    private static BingoVariant Create(
        CardLayout? layout = null,
        ColumnRange[]? columns = null,
        WinPattern[]? patterns = null) =>
        new(
            "test",
            layout ?? new CardLayout(2, 2, freeCell: null),
            ballMax: 20,
            columns ?? [new("A", 1, 10), new("B", 11, 20)],
            patterns ?? []);
}
