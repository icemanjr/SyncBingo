using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Cards;

/// <summary>
/// Statistical checks over many cards. A fixed seed keeps them repeatable, and the
/// tolerances are wide enough that a correct generator passes for any seed.
/// </summary>
public sealed class CardGeneratorTests
{
    private const int Deals = 3000;
    private static readonly BingoVariant Variant = StandardVariants.Classic75;

    [Fact]
    public void Columns_are_shuffled_not_sorted()
    {
        var random = new SeededRandom(42);
        var columns = Enumerable.Range(0, Deals)
            .Select(_ => CardGenerator.Generate(Variant, random))
            .SelectMany(card => Enumerable.Range(0, 5).Select(column => NumbersInColumn(card, column)))
            .ToList();

        var sorted = columns.Count(numbers => numbers.SequenceEqual(numbers.Order()));

        // A random column of 5 is ascending 1 time in 120 (1 in 24 for the 4-number N column).
        ((double)sorted / columns.Count).ShouldBeLessThan(0.03);
    }

    [Fact]
    public void Every_number_in_a_column_is_equally_likely()
    {
        var random = new SeededRandom(7);
        var appearances = new int[Variant.BallMax + 1];
        for (var deal = 0; deal < Deals; deal++)
        {
            foreach (var number in CardGenerator.Generate(Variant, random).Cells)
            {
                appearances[number]++;
            }
        }

        foreach (var range in Variant.Columns)
        {
            var slots = range.Label == "N" ? 4 : 5;
            var expected = Deals * slots / (double)range.Count;
            for (var number = range.Min; number <= range.Max; number++)
            {
                appearances[number].ShouldBeInRange((int)(expected * 0.85), (int)(expected * 1.15), $"ball {number}");
            }
        }
    }

    [Fact]
    public void Every_number_can_land_in_every_row_of_its_column()
    {
        var random = new SeededRandom(99);
        var seen = new HashSet<(int Number, int Row)>();
        for (var deal = 0; deal < Deals; deal++)
        {
            var card = CardGenerator.Generate(Variant, random);
            for (var cell = 0; cell < 25; cell++)
            {
                seen.Add((card.Cells[cell], cell / 5));
            }
        }

        // 4 columns × 15 numbers × 5 rows, plus N: 15 numbers × 4 rows, plus the free centre.
        seen.Count.ShouldBe((4 * 15 * 5) + (15 * 4) + 1);
    }

    private static int[] NumbersInColumn(Card card, int column) =>
        [.. Enumerable.Range(0, 5)
            .Select(row => card.Cells[(row * 5) + column])
            .Where(number => number != Card.FreeCellNumber)];
}
