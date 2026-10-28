using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Content;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>Card sizes (FR-26a): 5×5, 4×4 and 3×3, with 15 balls per column.</summary>
public sealed class CardSizeTests
{
    private static SessionSettings Size(CardSize size) => SessionSettings.Default with { CardSize = size };

    [Theory]
    [InlineData(CardSize.FiveByFive, 5, 75, 12, "B I N G O")]
    [InlineData(CardSize.FourByFour, 4, 60, null, "B I N G")]
    [InlineData(CardSize.ThreeByThree, 3, 45, 4, "B I N")]
    public void Each_size_has_15_balls_per_column(CardSize size, int side, int balls, int? freeCell, string letters)
    {
        var variant = StandardVariants.For(size);

        variant.Layout.Rows.ShouldBe(side);
        variant.Layout.Columns.ShouldBe(side);
        variant.Layout.FreeCell.ShouldBe(freeCell);
        variant.BallMax.ShouldBe(balls);
        string.Join(' ', variant.Columns.Select(column => column.Label)).ShouldBe(letters);
        variant.Columns.ShouldAllBe(column => column.Count == 15);
    }

    [Fact]
    public void Letters_need_a_5x5_card_and_everything_else_works_on_any()
    {
        StandardVariants.Classic45.Patterns.Select(pattern => pattern.Id).ShouldBe(["rows", "columns", "diagonals", "corners", "blackout"]);
        StandardVariants.Classic60.Patterns.Select(pattern => pattern.Id).ShouldBe(["rows", "columns", "diagonals", "corners", "blackout"]);
        StandardVariants.Classic45.GetPattern("corners").Masks.ShouldBe([StandardVariants.Classic45.Layout.ParseShape("X.X", "...", "X.X")]);
        StandardVariants.Classic60.GetSelection("normal").Patterns.SelectMany(pattern => pattern.Masks).Count().ShouldBe(4 + 4 + 2);
    }

    [Fact]
    public void A_3x3_card_holds_balls_1_to_45_by_column_around_a_free_center()
    {
        var card = CardGenerators.For(StandardVariants.Classic45).Generate(new SeededRandom(2));

        card.Cells[4].ShouldBe(Card.FreeCellNumber);
        card.Cells.Where((_, cell) => cell % 3 == 0).ShouldAllBe(number => number >= 1 && number <= 15);
        card.Cells.Where((_, cell) => cell % 3 == 2).ShouldAllBe(number => number >= 31 && number <= 45);
    }

    [Fact]
    public void Choosing_a_size_in_the_lobby_changes_the_card()
    {
        var session = NewSession();

        session.ChangeSettings(Size(CardSize.ThreeByThree), Now).IsSuccess.ShouldBeTrue();

        session.CardShapeChanged.ShouldBeTrue();
        session.Variant.Layout.Columns.ShouldBe(3);
        session.BallsRemaining.ShouldBe(45);
    }

    [Fact]
    public void A_4x4_card_has_no_free_space_whatever_the_setting()
    {
        var session = NewSession(Size(CardSize.FourByFour));

        session.Variant.Layout.FreeCell.ShouldBeNull();
        session.Settings.FreeSpace.ShouldBe(FreeSpace.AlwaysMarked);
    }

    [Fact]
    public void A_3x3_card_without_a_free_space()
    {
        var session = NewSession(Size(CardSize.ThreeByThree) with { FreeSpace = FreeSpace.None });

        session.Variant.Layout.FreeCell.ShouldBeNull();
        session.Variant.Layout.CellCount.ShouldBe(9);
    }

    [Fact]
    public void The_card_size_is_lobby_only()
    {
        var session = InState(SessionState.Manual);

        session.ChangeSettings(Size(CardSize.ThreeByThree), Now).Error!.Message.ShouldBe("Card size can only be changed between rounds.");
    }

    [Fact]
    public void A_letter_pattern_refuses_a_smaller_card_with_the_reason()
    {
        var session = NewSession(winCondition: "B+corners");

        var result = session.ChangeSettings(Size(CardSize.ThreeByThree), Now);

        result.Error!.Message.ShouldBe("A 3 × 3 card has no Letter B: choose another pattern too.");
        session.Variant.Layout.Columns.ShouldBe(5);
    }

    [Fact]
    public void A_new_pattern_and_size_are_applied_together()
    {
        var session = NewSession(winCondition: "B");

        session.ApplyRules(Size(CardSize.ThreeByThree), "corners", Now).IsSuccess.ShouldBeTrue();

        session.WinConditionId.ShouldBe("corners");
        session.Variant.Layout.Columns.ShouldBe(3);
    }

    [Fact]
    public void A_grouped_sets_card_changes_only_in_the_set_editor()
    {
        var session = NewSession();
        session.UseContent(ContentTestKit.Valid(ContentTestKit.TimesTables()), Now).IsSuccess.ShouldBeTrue();

        session.ChangeSettings(Size(CardSize.ThreeByThree), Now).Error!.Message
            .ShouldBe("This set's groups are the card's columns: change the card size in the set editor.");
    }

    [Fact]
    public void A_word_list_changes_card_in_options()
    {
        var session = NewSession();
        session.UseContent(ContentTestKit.Valid(ContentTestKit.Words(30)), Now);

        session.ChangeSettings(Size(CardSize.FourByFour), Now).IsSuccess.ShouldBeTrue();

        session.Variant.IsFreeForm.ShouldBeTrue();
        session.Variant.Layout.CellCount.ShouldBe(16);
        session.BallsRemaining.ShouldBe(30);
    }

    [Fact]
    public void The_set_editor_sends_a_set_and_its_card_together()
    {
        var session = NewSession();
        var threeGroups = ContentTestKit.Words(24).Select((entry, i) => entry with { Group = $"g{(i / 8) + 1}" });

        var events = session.UseContent(ContentTestKit.Valid(threeGroups), Now, CardSize.ThreeByThree).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(ContentChanged), typeof(SettingsChanged)]);
        session.Settings.CardSize.ShouldBe(CardSize.ThreeByThree);
        session.Variant.Columns.Select(column => column.Label).ShouldBe(["g1", "g2", "g3"]);
    }

    [Fact]
    public void A_set_whose_groups_dont_match_the_chosen_card_changes_nothing()
    {
        var session = NewSession();

        var result = session.UseContent(ContentTestKit.Valid(ContentTestKit.TimesTables()), Now, CardSize.ThreeByThree);

        result.Error!.Message.ShouldBe("A set with groups needs 3 groups, one for each card column; this set has 5.");
        session.Settings.CardSize.ShouldBe(CardSize.FiveByFive);
        session.Content.ShouldBeNull();
    }

    [Fact]
    public void A_stored_3x3_game_comes_back_on_its_card()
    {
        var session = NewSession(Size(CardSize.ThreeByThree));

        var restored = Session.Restore(session.ToSnapshot());

        restored.Variant.Layout.Columns.ShouldBe(3);
        restored.BallsRemaining.ShouldBe(45);
    }

    [Theory]
    [InlineData(new[] { 3, 2, 3 }, 72)]
    [InlineData(new[] { 3, 3, 3 }, 216)]
    [InlineData(new[] { 4, 2, 3 }, 288)]
    public void A_grouped_3x3_set_makes_this_many_different_cards(int[] groupSizes, long cards)
    {
        var entries = groupSizes.SelectMany((size, group) => Enumerable.Range(1, size).Select(i => new ContentEntry($"{group}.{i}", null, $"g{group}")));

        var variant = ContentVariant.For(StandardVariants.Classic45, ContentTestKit.Valid(entries)).Value;

        variant.PossibleCards.ShouldBe(cards);
    }

    [Fact]
    public void Eight_ungrouped_pairs_make_8_factorial_cards_and_standard_bingo_more_than_can_be_counted()
    {
        ContentVariant.For(StandardVariants.Classic45, ContentTestKit.Valid(ContentTestKit.Words(8))).Value.PossibleCards.ShouldBe(40_320);
        StandardVariants.Classic75.PossibleCards.ShouldBe(long.MaxValue);
    }

    [Fact]
    public void A_set_of_8_is_too_small_for_a_5x5_card()
    {
        var session = NewSession();

        session.UseContent(ContentTestKit.Valid(ContentTestKit.Words(8)), Now).Error!.Message
            .ShouldBe("This card needs at least 24 entries; the set has 8.");
    }
}
