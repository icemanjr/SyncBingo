using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>The earned free space (FR-26) and whether a card qualifies to win (FR-23).</summary>
public sealed class FreeSpaceAndQualifyingTests
{
    private static readonly SessionSettings Earned = SessionSettings.Default with { FreeSpace = FreeSpace.Earned };

    [Fact]
    public void An_earned_free_space_starts_unmarked_and_uncounted()
    {
        var kit = new PlayerTestKit(Earned);

        kit.Marks.ShouldBe(0u);
        CardRules.FreeCellCounts(kit.Card.Card, kit.Session).ShouldBeFalse();
    }

    [Fact]
    public void Balls_on_the_card_do_not_earn_it_but_the_first_miss_does()
    {
        var kit = new PlayerTestKit(Earned);
        kit.Call(1);
        kit.Call(2);
        CardRules.FreeCellCounts(kit.Card.Card, kit.Session).ShouldBeFalse();

        kit.Call(6); // not on the card
        kit.Call(7);

        CardRules.EarningCall(kit.Card.Card, kit.Session)!.Number.ShouldBe(6);
        (CardRules.CalledMask(kit.Card.Card, kit.Session) & Bits(Free)).ShouldBe(Bits(Free));
    }

    [Fact]
    public void Without_free_marking_the_free_space_can_only_be_marked_once_earned()
    {
        var kit = new PlayerTestKit(Earned with { FreeMarking = false });
        kit.Call(1);

        kit.Mark(Free).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        kit.Call(6);
        kit.Mark(Free).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Auto_fill_marks_the_free_space_one_call_after_the_ball_that_earned_it()
    {
        var kit = new PlayerTestKit(Earned with { AutoFillAvailable = true });
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(6); // earns the free space

        kit.CallWithAutoFill(7).ShouldBe([new AutoFilledCell(1, Free)]);
        kit.CallWithAutoFill(8).ShouldBeEmpty("only the earning ball fills the free space");
    }

    [Fact]
    public void A_line_through_the_centre_needs_the_free_space_earned()
    {
        var kit = new PlayerTestKit(Earned);
        foreach (var ball in new[] { 3, 18, 48, 63 }) // middle row except the centre
        {
            kit.Call(ball);
        }

        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeFalse();
        kit.Call(6);
        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeTrue();
    }

    [Fact]
    public void An_always_marked_free_space_counts_from_the_start()
    {
        var kit = new PlayerTestKit();
        foreach (var ball in new[] { 3, 18, 48, 63 })
        {
            kit.Call(ball);
        }

        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeTrue();
    }

    [Fact]
    public void By_default_a_card_qualifies_on_called_numbers_even_if_unmarked()
    {
        var kit = new PlayerTestKit();
        foreach (var ball in new[] { 1, 16, 31, 46, 61 }) // top row
        {
            kit.Call(ball);
        }

        kit.Marks.ShouldBe(Bits(Free));
        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeTrue();
    }

    [Fact]
    public void Requiring_marks_means_the_pattern_must_be_called_and_marked()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RequireMarksForClaims = true });
        foreach (var ball in new[] { 1, 16, 31, 46, 61 })
        {
            kit.Call(ball);
        }

        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeFalse();
        foreach (var cell in new[] { 0, 1, 2, 3, 4 })
        {
            kit.Mark(cell);
        }

        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeTrue();
    }

    [Fact]
    public void Mismarks_never_help_a_card_qualify()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RequireMarksForClaims = true });
        foreach (var ball in new[] { 1, 16, 31, 46 }) // top row except 61
        {
            kit.Call(ball);
        }

        foreach (var cell in new[] { 0, 1, 2, 3, 4 })
        {
            kit.Mark(cell); // cell 4 (ball 61) is a mismark
        }

        kit.Player.Qualifies(1, kit.Session).Value.ShouldBeFalse();
    }
}
