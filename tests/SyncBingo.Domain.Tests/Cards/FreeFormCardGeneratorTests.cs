using FsCheck.Xunit;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Tests.Content;

namespace SyncBingo.Domain.Tests.Cards;

/// <summary>Free-form dealing (ADR 0009): any item in any square.</summary>
public sealed class FreeFormCardGeneratorTests
{
    private static readonly BingoVariant Variant =
        ContentVariant.For(StandardVariants.Classic75, ContentTestKit.Valid(ContentTestKit.Words(30)), groupItems: false).Value;

    private static readonly FreeFormCardGenerator Generator = new(Variant);

    [Property(MaxTest = 500)]
    public void A_card_holds_24_different_items_and_the_free_space(int seed)
    {
        var card = Generator.Generate(new SeededRandom(seed));

        card.Cells[12].ShouldBe(Card.FreeCellNumber);
        var items = card.Cells.Where((_, cell) => cell != 12).ToList();
        items.Count.ShouldBe(24);
        items.ShouldBeUnique();
        items.ShouldAllBe(item => item >= 1 && item <= 30);
    }

    [Fact]
    public void Every_item_turns_up_somewhere_and_in_every_square()
    {
        var random = new SeededRandom(7);
        var seenItems = new HashSet<int>();
        var squaresPerItem1 = new HashSet<int>();
        for (var i = 0; i < 2000; i++)
        {
            var card = Generator.Generate(random);
            seenItems.UnionWith(card.Cells);
            if (card.CellOf(1) is { } cell)
            {
                squaresPerItem1.Add(cell);
            }
        }

        seenItems.ShouldBe([0, .. Enumerable.Range(1, 30)], ignoreOrder: true);
        squaresPerItem1.Count.ShouldBe(24, "item 1 lands in every square but the free one");
    }

    [Fact]
    public void Exactly_enough_items_deal_every_card_in_a_different_order()
    {
        var variant = ContentVariant.For(StandardVariants.Classic75, ContentTestKit.Valid(ContentTestKit.Words(24)), groupItems: false).Value;
        var claimed = new HashSet<string>();

        var cards = Enumerable.Range(0, 50)
            .Select(_ => new FreeFormCardGenerator(variant).GenerateUnique(new SeededRandom(claimed.Count), card => claimed.Add(card.Fingerprint)))
            .ToList();

        cards.ShouldAllBe(card => card.Cells.Where(item => item != 0).Order().SequenceEqual(Enumerable.Range(1, 24)));
        cards.Select(card => card.Fingerprint).ShouldBeUnique();
    }
}
