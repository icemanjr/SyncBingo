using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

public sealed class BallDrawingTests
{
    [Fact]
    public void A_draw_records_the_ball_its_letter_and_its_place_in_the_round()
    {
        var session = InState(SessionState.Running);

        var called = session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Single<BallCalled>();

        called.Call.Sequence.ShouldBe(1);
        called.Call.Number.ShouldBeInRange(1, 75);
        called.Call.Letter.ShouldBe(session.Variant.Columns[session.Variant.ColumnOf(called.Call.Number)].Label);
        called.Call.CalledAt.ShouldBe(Now);
        called.Call.Source.ShouldBe(DrawSource.Manual);
        called.PreviousNumber.ShouldBeNull();
        session.HasBeenCalled(called.Call.Number).ShouldBeTrue();
        session.BallsRemaining.ShouldBe(74);
    }

    [Fact]
    public void Each_draw_reports_the_previous_ball_for_auto_fill()
    {
        var session = InState(SessionState.Running);
        var random = new SeededRandom(2);
        var first = session.DrawBall(DrawSource.Manual, random, Now).Single<BallCalled>();

        var second = session.DrawBall(DrawSource.Manual, random, Now).Single<BallCalled>();

        second.PreviousNumber.ShouldBe(first.Call.Number);
        second.Call.Sequence.ShouldBe(2);
    }

    [Fact]
    public void All_75_balls_come_out_once_each_then_the_hopper_is_empty()
    {
        var session = InState(SessionState.Running);
        var random = new SeededRandom(4);

        var numbers = Enumerable.Range(0, 75)
            .Select(_ => session.DrawBall(DrawSource.Manual, random, Now).Single<BallCalled>().Call.Number)
            .ToList();

        numbers.Order().ShouldBe(Enumerable.Range(1, 75));
        session.BallsRemaining.ShouldBe(0);
        session.DrawBall(DrawSource.Manual, random, Now).Error!.Code.ShouldBe(DomainErrorCode.HopperEmpty);
    }

    [Fact]
    public void The_host_can_grab_a_ball_while_paused()
    {
        var session = InState(SessionState.Paused);

        session.DrawBall(DrawSource.Manual, new SeededRandom(5), Now).IsSuccess.ShouldBeTrue(); // A6
        session.State.ShouldBe(SessionState.Paused);
    }

    [Fact]
    public void The_scheduler_never_draws_while_paused()
    {
        var session = InState(SessionState.Paused, new SessionSettings { CallerMode = CallerMode.Auto });

        session.DrawBall(DrawSource.Auto, new SeededRandom(5), Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Fact]
    public void The_scheduler_only_draws_in_auto_mode()
    {
        var session = InState(SessionState.Running);

        session.DrawBall(DrawSource.Auto, new SeededRandom(5), Now).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
    }

    [Fact]
    public void A_new_round_starts_the_previous_ball_over()
    {
        var session = InState(SessionState.Running);
        var random = new SeededRandom(6);
        session.DrawBall(DrawSource.Manual, random, Now).IsSuccess.ShouldBeTrue();
        session.StartRound(redrawCards: false, Now).IsSuccess.ShouldBeTrue();

        var first = session.DrawBall(DrawSource.Manual, random, Now).Single<BallCalled>();

        first.PreviousNumber.ShouldBeNull();
        first.Call.Sequence.ShouldBe(1);
    }

    [Fact]
    public void Every_ball_is_equally_likely_to_be_drawn_first()
    {
        var random = new SeededRandom(8);
        var counts = new int[76];
        for (var game = 0; game < 7500; game++)
        {
            var session = InState(SessionState.Running);
            counts[session.DrawBall(DrawSource.Manual, random, Now).Single<BallCalled>().Call.Number]++;
        }

        // 100 expected per ball; a fair draw stays well inside ±40%.
        counts.Skip(1).ShouldAllBe(count => count > 60 && count < 140);
    }
}
