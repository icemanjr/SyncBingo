using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>Auto mode returns to manual on its own once every ball is called (FR-10).</summary>
public sealed class LastBallTests
{
    [Fact]
    public void The_last_auto_ball_returns_to_manual_mode()
    {
        var session = ManualWithBallsLeft(2);
        session.StartAutoCalling(new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        var events = session.DrawBall(DrawSource.Auto, new SeededRandom(1), Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(BallCalled), typeof(AutoCallingPaused)]);
        session.State.ShouldBe(SessionState.Manual);
        session.BallsRemaining.ShouldBe(0);
    }

    [Fact]
    public void Auto_mode_on_the_last_ball_calls_it_and_is_back_in_manual_at_once()
    {
        var session = ManualWithBallsLeft(1);

        var events = session.StartAutoCalling(new SeededRandom(1), Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(AutoCallingStarted), typeof(BallCalled), typeof(AutoCallingPaused)]);
        session.State.ShouldBe(SessionState.Manual);
    }

    [Fact]
    public void Auto_mode_with_balls_left_keeps_going()
    {
        var session = ManualWithBallsLeft(3);
        session.StartAutoCalling(new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();

        session.DrawBall(DrawSource.Auto, new SeededRandom(1), Now).Single<BallCalled>();

        session.State.ShouldBe(SessionState.Auto);
    }

    [Fact]
    public void The_last_manual_ball_just_empties_the_hopper()
    {
        var session = ManualWithBallsLeft(1);

        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Single<BallCalled>();

        session.State.ShouldBe(SessionState.Manual);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Error!.Code.ShouldBe(DomainErrorCode.HopperEmpty);
    }

    private static Session ManualWithBallsLeft(int left)
    {
        var session = InState(SessionState.Manual);
        var random = new SeededRandom(7);
        while (session.BallsRemaining > left)
        {
            session.DrawBall(DrawSource.Manual, random, Now).IsSuccess.ShouldBeTrue();
        }

        return session;
    }
}
