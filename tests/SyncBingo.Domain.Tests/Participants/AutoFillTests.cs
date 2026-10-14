using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>Auto-fill is made available by the host and switched on per player (FR-16).</summary>
public sealed class AutoFillTests
{
    private static readonly SessionSettings Available = SessionSettings.Default with { AutoFillAvailable = true };

    [Fact]
    public void A_missed_number_is_filled_when_the_next_ball_is_called()
    {
        var kit = new PlayerTestKit(Available);
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(1);

        kit.Marks.ShouldBe(Bits(Free), "the player still has a full call to mark it");
        kit.CallWithAutoFill(2).ShouldBe([new AutoFilledCell(1, 0)]);
        kit.Marks.ShouldBe(Bits(Free, 0));
    }

    [Fact]
    public void Only_players_who_switched_it_on_get_auto_fill()
    {
        var kit = new PlayerTestKit(Available);
        kit.Call(1);

        kit.CallWithAutoFill(2).ShouldBeEmpty();
        kit.Marks.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Players_cannot_switch_it_on_unless_the_host_made_it_available()
    {
        var kit = new PlayerTestKit();

        kit.Player.SetAutoFill(true, kit.Session).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        kit.Player.SetAutoFill(false, kit.Session).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Nothing_is_filled_while_the_host_has_it_unavailable()
    {
        var kit = new PlayerTestKit(Available);
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Session.ChangeSettings(SessionSettings.Default, Sessions.SessionTestKit.Now).IsSuccess.ShouldBeTrue();
        kit.Call(1);

        kit.CallWithAutoFill(2).ShouldBeEmpty();
    }

    [Fact]
    public void Numbers_not_on_the_card_and_cells_already_marked_are_left_alone()
    {
        var kit = new PlayerTestKit(Available);
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(1);
        kit.Mark(0);

        kit.CallWithAutoFill(6).ShouldBeEmpty(); // ball 1 already marked
        kit.CallWithAutoFill(7).ShouldBeEmpty(); // ball 6 is not on the card
    }

    [Fact]
    public void The_last_ball_of_a_round_is_never_filled()
    {
        var kit = new PlayerTestKit(Available);
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(1);
        kit.Session.EndRound(Sessions.SessionTestKit.Now).IsSuccess.ShouldBeTrue();

        kit.Player.ApplyAutoFill(1, kit.Session).ShouldBeEmpty();
        kit.Marks.ShouldBe(Bits(Free));
    }
}
