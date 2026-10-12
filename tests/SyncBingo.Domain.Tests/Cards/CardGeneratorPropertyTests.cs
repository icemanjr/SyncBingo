using FsCheck.Xunit;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Cards;

/// <summary>
/// Properties every dealt card must have. FsCheck supplies hundreds of random seeds
/// per run and shrinks any failure to the simplest seed that reproduces it.
/// </summary>
public sealed class CardGeneratorPropertyTests
{
    private static readonly BingoVariant Variant = StandardVariants.Classic75;

    [Property(MaxTest = 500)]
    public void Every_column_holds_distinct_numbers_from_its_own_range(int seed)
    {
        var card = CardGenerator.Generate(Variant, new SeededRandom(seed));

        for (var column = 0; column < 5; column++)
        {
            var numbers = NumbersInColumn(card, column);
            var range = Variant.Columns[column];

            numbers.ShouldAllBe(number => range.Contains(number), $"column {range.Label}");
            numbers.ShouldBeUnique();
        }
    }

    [Property(MaxTest = 500)]
    public void Only_the_centre_is_free(int seed)
    {
        var card = CardGenerator.Generate(Variant, new SeededRandom(seed));

        card.Cells.Length.ShouldBe(25);
        card.Cells[Cells.FreeCentre].ShouldBe(Card.FreeCellNumber);
        card.Cells.Count(number => number == Card.FreeCellNumber).ShouldBe(1);
    }

    [Property(MaxTest = 500)]
    public void No_number_appears_twice_on_a_card(int seed)
    {
        var card = CardGenerator.Generate(Variant, new SeededRandom(seed));

        card.Cells.Where(number => number != Card.FreeCellNumber).ShouldBeUnique();
    }

    [Property(MaxTest = 100)]
    public void The_same_seed_deals_the_same_card(int seed)
    {
        var first = CardGenerator.Generate(Variant, new SeededRandom(seed));
        var second = CardGenerator.Generate(Variant, new SeededRandom(seed));

        second.ShouldBe(first);
    }

    private static int[] NumbersInColumn(Card card, int column) =>
        [.. Enumerable.Range(0, 5)
            .Select(row => card.Cells[(row * 5) + column])
            .Where(number => number != Card.FreeCellNumber)];
}
