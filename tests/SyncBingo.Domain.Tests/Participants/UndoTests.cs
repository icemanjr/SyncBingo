using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>Undo returns the card to how it was when the last ball was called (FR-25).</summary>
public sealed class UndoTests
{
    [Fact]
    public void Undo_reverts_every_change_since_the_last_ball()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);
        kit.Mark(0);
        kit.Call(2);
        kit.Mark(5);
        kit.Mark(7); // a fat-fingered mismark
        kit.Unmark(0);

        kit.Undo().Value.ShouldBe(Bits(Free, 0));
    }

    [Fact]
    public void Undo_works_even_when_unmarking_is_off()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { Unmarking = Unmarking.Off });
        kit.Call(1);
        kit.Mark(1); // wrong square

        kit.Unmark(1).IsSuccess.ShouldBeFalse();
        kit.Undo().Value.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Before_the_first_ball_undo_returns_the_card_as_dealt()
    {
        var kit = new PlayerTestKit();
        kit.Mark(3);

        kit.Undo().Value.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Marks_are_locked_in_once_the_next_ball_is_called()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);
        kit.Mark(0);
        kit.Call(2);

        kit.Undo().Value.ShouldBe(Bits(Free, 0));
    }

    [Fact]
    public void Pressing_undo_again_changes_nothing()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);
        kit.Mark(0);

        kit.Undo().Value.ShouldBe(Bits(Free));
        kit.Undo().Value.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Undo_keeps_what_auto_fill_marked_during_the_draw()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { AutoFillAvailable = true });
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(1);
        kit.CallWithAutoFill(2); // fills ball 1 (cell 0)
        kit.Mark(5);

        kit.Undo().Value.ShouldBe(Bits(Free, 0));
    }

    [Fact]
    public void A_new_round_forgets_the_last_rounds_undo()
    {
        var kit = new PlayerTestKit();
        kit.Mark(3); // snapshot for "before the first ball" of round 1
        kit.Session.NextRound(Sessions.SessionTestKit.Now);
        kit.Player.ResetForNewRound(kit.Session, deal: null);

        kit.Undo().Value.ShouldBe(Bits(Free));
        kit.Card.UndoSequence.ShouldBe(-1);
    }
}
