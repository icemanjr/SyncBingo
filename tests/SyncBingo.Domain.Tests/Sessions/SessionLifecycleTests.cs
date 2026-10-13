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
    public void A_round_starts_in_manual_mode_with_a_full_hopper()
    {
        var session = NewSession();

        var started = session.StartRound(redrawCards: true, Now).Single<RoundStarted>();

        started.ShouldBe(new RoundStarted(1, RedrawCards: true) { Seq = 1 });
        session.State.ShouldBe(SessionState.Manual); // A4: nothing is called until the host acts
        session.BallsRemaining.ShouldBe(75);
    }

    [Fact]
    public void Clear_cards_mid_round_ends_the_round_then_starts_the_next()
    {
        var session = InState(SessionState.Manual);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        var events = session.StartRound(redrawCards: false, Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(RoundEnded), typeof(RoundStarted)]);
        ((RoundEnded)events[0]).Round.ShouldBe(1);
        ((RoundStarted)events[1]).Round.ShouldBe(2);
        session.Calls.ShouldBeEmpty();
        session.BallsRemaining.ShouldBe(75);
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

        session.ChangeWinCondition("blackout", Now).Single<WinConditionChanged>().WinConditionId.ShouldBe("blackout");

        session.WinConditionId.ShouldBe("blackout");
        session.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public void Changing_to_an_unknown_win_condition_is_refused()
    {
        var session = InState(SessionState.Manual);

        session.ChangeWinCondition("zigzag", Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        session.WinConditionId.ShouldBe("normal");
    }

    [Fact]
    public void Ending_the_session_is_final()
    {
        var session = InState(SessionState.Manual);

        session.End(Now).Single<SessionEnded>();

        session.State.ShouldBe(SessionState.Ended);
        session.StartRound(redrawCards: false, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Fact]
    public void Joining_mid_round_gets_a_friendly_refusal()
    {
        var error = InState(SessionState.Manual).CheckCanAdmit(currentParticipants: 3).Error!;

        error.Code.ShouldBe(DomainErrorCode.WrongState);
        error.Message.ShouldContain("You can join when it ends");
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
            session.StartRound(false, Now).Value,
            session.DrawBall(DrawSource.Manual, random, Now).Value,
            session.StartAutoCalling(random, Now).Value,
            session.StartRound(false, Now).Value,
        }.SelectMany(batch => batch);

        events.Select(e => e.Seq).ShouldBe([1, 2, 3, 4, 5, 6]);
        session.LastEventSeq.ShouldBe(6);
    }

    [Fact]
    public void Successful_commands_record_activity_for_idle_expiry()
    {
        var session = NewSession();
        var later = Now.AddMinutes(5);

        session.StartRound(false, later).IsSuccess.ShouldBeTrue();

        session.LastActivityAt.ShouldBe(later);
        session.Version.ShouldBe(1);
    }
}
