using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Cards;

/// <summary>No two cards in play in a session may be identical (FR-39).</summary>
public sealed class UniqueCardTests
{
    /// <summary>
    /// A 1×2 card with columns {1, 2} and {3, 4}: exactly four distinct cards exist,
    /// small enough to exhaust.
    /// </summary>
    private static readonly BingoVariant Tiny = new(
        "tiny",
        new CardLayout(rows: 1, columns: 2, freeCell: null),
        ballMax: 4,
        columns: [new("A", 1, 2), new("B", 3, 4)],
        patterns: []);

    [Fact]
    public void Skips_cards_that_are_already_in_play()
    {
        var rejected = new List<Card>();

        var card = CardGenerator.GenerateUnique(StandardVariants.Classic75, new SeededRandom(1), candidate =>
        {
            if (rejected.Count < 3)
            {
                rejected.Add(candidate);
                return false;
            }

            return true;
        });

        rejected.Count.ShouldBe(3);
        rejected.ShouldNotContain(card);
    }

    [Fact]
    public void Deals_every_distinct_card_before_running_out()
    {
        var inPlay = new HashSet<string>();
        var random = new SeededRandom(3);

        var dealt = Enumerable.Range(0, 4)
            .Select(_ => CardGenerator.GenerateUnique(Tiny, random, card => inPlay.Add(card.Fingerprint)))
            .ToList();

        dealt.Select(card => card.Fingerprint).ShouldBe(["1-3", "1-4", "2-3", "2-4"], ignoreOrder: true);
    }

    [Fact]
    public void Fails_clearly_when_no_unused_card_is_left()
    {
        var inPlay = new HashSet<string> { "1-3", "1-4", "2-3", "2-4" };

        var error = Should.Throw<InvalidOperationException>(() =>
            CardGenerator.GenerateUnique(Tiny, new SeededRandom(5), card => inPlay.Add(card.Fingerprint)));

        error.Message.ShouldContain("not already in play");
    }

    [Fact]
    public void A_card_freed_from_play_can_be_dealt_again()
    {
        var inPlay = new HashSet<string> { "1-3", "1-4", "2-3", "2-4" };
        inPlay.Remove("2-4"); // e.g. its owner redrew or was removed

        var card = CardGenerator.GenerateUnique(Tiny, new SeededRandom(5), candidate => inPlay.Add(candidate.Fingerprint));

        card.Fingerprint.ShouldBe("2-4");
    }

    [Fact]
    public void A_full_session_of_players_all_get_different_cards()
    {
        var inPlay = new HashSet<string>();
        var random = new SeededRandom(11);

        var cards = Enumerable.Range(0, 100)
            .Select(_ => CardGenerator.GenerateUnique(StandardVariants.Classic75, random, card => inPlay.Add(card.Fingerprint)))
            .ToList();

        cards.ShouldBeUnique();
    }
}
