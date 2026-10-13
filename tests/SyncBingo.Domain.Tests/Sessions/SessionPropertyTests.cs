using FsCheck.Xunit;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>
/// Random sequences of host and scheduler commands, accepted or refused, must never
/// break the session's invariants. FsCheck shrinks a failure to the shortest sequence.
/// </summary>
public sealed class SessionPropertyTests
{
    private static readonly Func<Session, IRandomSource, Result<IReadOnlyList<SessionEvent>>>[] Commands =
    [
        (s, _) => s.StartCountdown(startInAuto: false, Now),
        (s, r) => s.StartRound(r, Now + Session.CountdownLength),
        (s, _) => s.CancelCountdown(Now),
        (s, _) => s.ClearCards(redrawCards: false, Now),
        (s, _) => s.EndRound(Now),
        (s, r) => s.StartAutoCalling(r, Now),
        (s, _) => s.Pause(Now),
        (s, r) => s.DrawBall(DrawSource.Manual, r, Now),
        (s, r) => s.DrawBall(DrawSource.Auto, r, Now),
        (s, r) => s.DrawBall(DrawSource.Manual, r, Now), // draws twice as often, to empty hoppers
        (s, _) => s.ChangeWinCondition("corners", Now),
        (s, _) => s.UpdateSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now),
        (s, _) => s.End(Now),
    ];

    [Property(MaxTest = 300)]
    public void Invariants_hold_after_any_sequence_of_commands(int seed, byte[] script)
    {
        var session = NewSession();
        var random = new SeededRandom(seed);
        var lastSeq = 0L;

        foreach (var step in script)
        {
            var before = (session.State, session.Version, session.Calls.Count);
            var result = Commands[step % Commands.Length](session, random);

            if (result.IsSuccess)
            {
                result.Value.ShouldNotBeEmpty();
                foreach (var e in result.Value)
                {
                    e.Seq.ShouldBe(++lastSeq);
                }

                session.Version.ShouldBe(before.Version + 1);
            }
            else
            {
                (session.State, session.Version, session.Calls.Count).ShouldBe(before);
            }

            var numbers = session.Calls.Select(call => call.Number).ToList();
            numbers.ShouldBeUnique();
            numbers.ShouldAllBe(n => n >= 1 && n <= 75);
            session.Calls.Select(call => call.Sequence).ShouldBe(Enumerable.Range(1, numbers.Count));
            session.BallsRemaining.ShouldBe(75 - numbers.Count);
            session.LastEventSeq.ShouldBe(lastSeq);
        }
    }

    [Property(MaxTest = 300)]
    public void Once_ended_nothing_changes(byte[] script)
    {
        var session = InState(SessionState.Ended);
        var random = new SeededRandom(1);
        var version = session.Version;

        foreach (var step in script)
        {
            Commands[step % Commands.Length](session, random).IsSuccess.ShouldBeFalse();
        }

        session.State.ShouldBe(SessionState.Ended);
        session.Version.ShouldBe(version);
    }
}
