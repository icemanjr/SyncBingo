using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Content;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>A card with no free space (FR-26 <c>None</c>): the center is an ordinary square.</summary>
public sealed class NoFreeSpaceTests
{
    private static readonly SessionSettings None = SessionSettings.Default with { FreeSpace = FreeSpace.None };

    [Fact]
    public void The_variant_without_a_free_cell_keeps_its_columns_and_patterns()
    {
        var standard = StandardVariants.Classic75;

        var plain = standard.WithoutFreeCell();

        plain.Layout.FreeCell.ShouldBeNull();
        plain.InitialMarks.ShouldBe(0u);
        plain.Columns.ShouldBe(standard.Columns);
        plain.Patterns.ShouldBe(standard.Patterns);
        plain.WithoutFreeCell().ShouldBeSameAs(plain);
    }

    [Fact]
    public void Every_square_of_the_card_holds_a_ball()
    {
        var variant = StandardVariants.Classic75.WithoutFreeCell();

        var card = CardGenerators.For(variant).Generate(new SeededRandom(1));

        card.Cells.ShouldNotContain(Card.FreeCellNumber);
        card.Cells[12].ShouldBeInRange(31, 45, "the center is an N square");
    }

    [Fact]
    public void A_game_can_start_without_a_free_space()
    {
        var session = NewSession(None);

        session.Variant.Layout.FreeCell.ShouldBeNull();
        CardRules.InitialMarks(session).ShouldBe(0u);
    }

    [Fact]
    public void Choosing_none_in_the_lobby_changes_the_card_and_back_again()
    {
        var session = NewSession();

        session.ChangeSettings(None, Now).IsSuccess.ShouldBeTrue();
        session.CardShapeChanged.ShouldBeTrue();
        session.Variant.Layout.FreeCell.ShouldBeNull();

        session.ChangeSettings(SessionSettings.Default, Now).IsSuccess.ShouldBeTrue();
        session.CardShapeChanged.ShouldBeTrue();
        session.Variant.Layout.FreeCell.ShouldBe(12);
    }

    [Fact]
    public void Switching_between_always_marked_and_earned_keeps_the_card()
    {
        var session = NewSession();

        session.ChangeSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now).IsSuccess.ShouldBeTrue();

        session.CardShapeChanged.ShouldBeFalse();
        session.Variant.Layout.FreeCell.ShouldBe(12);
    }

    [Fact]
    public void None_is_lobby_only()
    {
        var session = InState(SessionState.Manual);

        var result = session.ChangeSettings(None, Now);

        result.Error!.Message.ShouldBe("Free space can only be changed between rounds.");
        session.Variant.Layout.FreeCell.ShouldBe(12);
    }

    [Fact]
    public void A_set_of_24_needs_the_free_space()
    {
        var session = NewSession();
        session.UseContent(ContentTestKit.Valid(ContentTestKit.Words(24)), Now).IsSuccess.ShouldBeTrue();

        var result = session.ChangeSettings(None, Now);

        result.Error!.Message.ShouldBe("This card needs at least 25 entries; the set has 24.");
        session.Settings.FreeSpace.ShouldBe(FreeSpace.AlwaysMarked);
        session.Variant.Layout.FreeCell.ShouldBe(12);
    }

    [Fact]
    public void Without_a_free_space_the_middle_group_must_fill_its_whole_column()
    {
        var entries = ContentTestKit.TimesTables();
        entries.RemoveAll(entry => entry.Ball == "3 × 3"); // ×3, the middle column, now has 4
        entries.Add(new ContentEntry("spare", null, "×5"));
        var session = NewSession();
        session.UseContent(ContentTestKit.Valid(entries), Now).IsSuccess.ShouldBeTrue();

        session.ChangeSettings(None, Now).Error!.Message.ShouldBe("Group '×3' has 4 entries; its column needs at least 5.");
    }

    [Fact]
    public void A_set_used_without_a_free_space_fills_every_square()
    {
        var session = NewSession(None);

        session.UseContent(ContentTestKit.Valid(ContentTestKit.Words(24)), Now).Error!.Message
            .ShouldBe("This card needs at least 25 entries; the set has 24.");
        session.UseContent(ContentTestKit.Valid(ContentTestKit.Words(25)), Now).IsSuccess.ShouldBeTrue();
        session.Variant.Layout.FreeCell.ShouldBeNull();
    }

    [Fact]
    public void A_stored_game_without_a_free_space_comes_back_without_one()
    {
        var session = NewSession(None);
        var set = ContentTestKit.Valid(ContentTestKit.Words(30));
        session.UseContent(set, Now);

        var restored = Session.Restore(session.ToSnapshot(), set);

        restored.Variant.Layout.FreeCell.ShouldBeNull();
        restored.Variant.IsFreeForm.ShouldBeTrue();
    }
}
