using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

public sealed class SessionLifecycleTests
{
    [Fact]
    public void A_new_session_waits_in_the_lobby_before_round_one()
    {
        var session = NewSession();

        session.State.ShouldBe(SessionState.BetweenRounds);
        session.RoundNumber.ShouldBe(0);
        session.Calls.ShouldBeEmpty();
        session.LastEventSeq.ShouldBe(0);
    }

    [Fact]
    public void Create_rejects_a_win_condition_the_variant_does_not_have()
    {
        var result = Session.Create(
            GameCode.Parse("9ve4-shp3-k7mw-a2qx"), SyncBingo.Domain.Rules.StandardVariants.Classic75, SessionSettings.Default, "zigzag", Now);

        result.Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public void A_round_starts_in_manual_mode_with_a_full_hopper_when_the_countdown_ends()
    {
        var session = InState(SessionState.Starting);

        var started = session.StartRound(new SeededRandom(1), Now + Session.CountdownLength).Single<RoundStarted>();

        started.Round.ShouldBe(1);
        session.State.ShouldBe(SessionState.Manual); // A4: nothing is called until the host acts
        session.BallsRemaining.ShouldBe(75);
        session.CountdownEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Clear_cards_mid_round_ends_the_round_and_returns_to_the_lobby()
    {
        var session = InState(SessionState.Manual);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        var events = session.ClearCards(redrawCards: false, Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(RoundEnded), typeof(CardsCleared)]);
        ((RoundEnded)events[0]).Round.ShouldBe(1);
        session.State.ShouldBe(SessionState.BetweenRounds, "late arrivals can join before the next round");
        session.Calls.ShouldBeEmpty();
        session.BallsRemaining.ShouldBe(75);
        session.CheckCanAdmit(0).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Ending_a_round_keeps_its_calls_visible_until_the_next_round()
    {
        var session = InState(SessionState.Manual);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        session.EndRound(Now).Single<RoundEnded>().Round.ShouldBe(1);

        session.State.ShouldBe(SessionState.BetweenRounds);
        session.Calls.Count.ShouldBe(1);
    }


    [Fact]
    public void The_win_condition_can_change_mid_round_without_touching_the_calls()
    {
        var session = InState(SessionState.Manual);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        session.ChangePattern("blackout", Now).Value.OfType<WinConditionChanged>().ShouldHaveSingleItem().WinConditionId.ShouldBe("blackout");

        session.WinConditionId.ShouldBe("blackout");
        session.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public void Changing_to_an_unknown_win_condition_is_refused()
    {
        var session = InState(SessionState.Manual);

        session.ChangePattern("zigzag", Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        session.WinConditionId.ShouldBe("normal");
    }

    [Fact]
    public void Ending_the_session_is_final()
    {
        var session = InState(SessionState.Manual);

        session.End(Now).Single<SessionEnded>();

        session.State.ShouldBe(SessionState.Ended);
        session.StartCountdown(startInAuto: false, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Theory]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.Manual)]
    [InlineData(SessionState.Auto)]
    public void Joining_during_a_countdown_or_a_round_gets_a_friendly_refusal(SessionState state)
    {
        var error = InState(state).CheckCanAdmit(currentParticipants: 3).Error!;

        error.Code.ShouldBe(DomainErrorCode.WrongState);
        error.Message.ShouldBe("A round is in progress. You can join between rounds.");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public void Joining_stops_at_the_participant_limit(int current, bool allowed)
    {
        var result = NewSession().CheckCanAdmit(current);

        result.IsSuccess.ShouldBe(allowed);
        if (!allowed)
        {
            result.Error!.Code.ShouldBe(DomainErrorCode.SessionFull);
        }
    }

    [Fact]
    public void Every_event_gets_the_next_sequence_number_across_rounds()
    {
        var session = NewSession();
        var random = new SeededRandom(3);

        var events = new[]
        {
            session.NextRound(Now), // countdown started, round started
            session.DrawBall(DrawSource.Manual, random, Now).Value,
            session.StartAutoCalling(random, Now).Value, // auto started, ball
            session.NextRound(Now), // round ended, cards cleared, countdown started, round started
        }.SelectMany(batch => batch);

        events.Select(e => e.Seq).ShouldBe([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        session.LastEventSeq.ShouldBe(9);
    }

    [Fact]
    public void Successful_commands_record_activity_for_idle_expiry()
    {
        var session = NewSession();
        var later = Now.AddMinutes(5);

        session.StartCountdown(startInAuto: false, later).IsSuccess.ShouldBeTrue();

        session.LastActivityAt.ShouldBe(later);
        session.Version.ShouldBe(1);
    }
}
