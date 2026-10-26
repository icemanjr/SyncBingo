using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>"Start Round N", the 10-second countdown and "Clear cards" (FR-34, FR-45).</summary>
public sealed class RoundCountdownTests
{
    [Fact]
    public void Start_round_counts_down_ten_seconds_to_the_next_round()
    {
        var session = NewSession();

        var countdown = session.StartCountdown(startInAuto: false, Now).Single<RoundCountdownStarted>();

        countdown.ShouldBe(new RoundCountdownStarted(1, Now.AddSeconds(10), StartInAuto: false, Normal) { Seq = countdown.Seq, Audience = Audience.Everyone, Streams = countdown.Streams });
        session.State.ShouldBe(SessionState.Starting);
        session.IsInRound.ShouldBeFalse();
        session.UpcomingRound.ShouldBe(1);
    }

    [Fact]
    public void During_the_countdown_joiners_wait_and_redraws_close()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        kit.Session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();
        kit.Session.StartCountdown(startInAuto: false, Now).IsSuccess.ShouldBeTrue();

        kit.Session.JoinersWait.ShouldBeTrue();
        kit.Player.RedrawCard(1, kit.Session, () => PlayerTestKit.KnownCard).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Fact]
    public void Cancelling_returns_to_the_lobby_and_reopens_joining()
    {
        var session = InState(SessionState.Starting);

        session.CancelCountdown(Now).Single<RoundCountdownCancelled>().Round.ShouldBe(1);

        session.State.ShouldBe(SessionState.BetweenRounds);
        session.CountdownEndsAt.ShouldBeNull();
        session.CheckCanAdmit(0).IsSuccess.ShouldBeTrue();
        session.RoundNumber.ShouldBe(0, "the cancelled round never started");
    }

    [Fact]
    public void The_round_cannot_start_before_the_countdown_ends()
    {
        var session = InState(SessionState.Starting);

        session.StartRound(new SeededRandom(1), Now.AddSeconds(9)).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        session.State.ShouldBe(SessionState.Starting);
    }

    [Fact]
    public void Starting_in_auto_mode_draws_the_first_ball_at_zero()
    {
        var session = NewSession();
        session.StartCountdown(startInAuto: true, Now).IsSuccess.ShouldBeTrue();

        var events = session.StartRound(new SeededRandom(1), Now.AddSeconds(10)).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(RoundStarted), typeof(AutoCallingStarted), typeof(BallCalled)]);
        session.State.ShouldBe(SessionState.Auto);
        session.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public void Settings_chosen_in_the_lobby_apply_to_the_next_round()
    {
        var session = InState(SessionState.Manual);
        session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();
        session.ChangeSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now).IsSuccess.ShouldBeTrue();

        session.StartCountdown(startInAuto: false, Now).IsSuccess.ShouldBeTrue();
        session.StartRound(new SeededRandom(1), Now.AddSeconds(10)).IsSuccess.ShouldBeTrue();

        session.Settings.FreeSpace.ShouldBe(FreeSpace.Earned);
    }

    [Fact]
    public void The_rules_cannot_change_during_the_countdown()
    {
        var session = InState(SessionState.Starting);

        session.ChangePattern("blackout", Now).Error!.Message.ShouldBe("The round is about to start. Cancel the countdown to change the rules.");
        session.WinConditionId.ShouldBe(Normal);
    }

    [Fact]
    public void Clear_cards_is_refused_during_the_countdown()
    {
        InState(SessionState.Starting).ClearCards(redrawCards: true, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Fact]
    public void Clear_cards_in_the_lobby_clears_without_ending_a_round()
    {
        var session = InState(SessionState.Manual);
        session.EndRound(Now).IsSuccess.ShouldBeTrue(); // calls stay visible after End round

        var events = session.ClearCards(redrawCards: false, Now).Value;

        events.ShouldHaveSingleItem().ShouldBeOfType<CardsCleared>();
        session.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Cards_dealt_in_the_lobby_belong_to_the_next_round()
    {
        var session = InState(SessionState.Manual);
        session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();

        var player = Participant.Join("p2", PlayerTestKit.Name("Ava"), PlayerTestKit.KnownCard, session, Now);

        player.Cards[0].IssuedInRound.ShouldBe(2);
    }
}
