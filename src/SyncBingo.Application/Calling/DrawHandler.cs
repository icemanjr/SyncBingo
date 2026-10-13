using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Calling;

/// <summary>
/// Every way a ball gets drawn: the host's "Grab ball" (manual mode), "Auto mode" (draws at
/// once, then the schedule takes over), and the scheduler's tick. After the draw, inside
/// the same lock (docs/02 §4, docs/03 §3):
/// <list type="number">
/// <item>auto-fill the previous ball for players who switched it on (FR-16);</item>
/// <item>check every card for wins on called numbers (FR-19), which may pause auto mode.</item>
/// </list>
/// Auto-fill notifications go out before the new ball is announced.
/// </summary>
public sealed class DrawHandler(SessionCommandRunner runner, IRandomSource random)
{
    public Task<Result<CommandOutcome>> GrabBallAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        DrawAsync(code, caller, (session, now) => session.DrawBall(DrawSource.Manual, random, now), cancellationToken);

    public Task<Result<CommandOutcome>> StartAutoModeAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        DrawAsync(code, caller, (session, now) => session.StartAutoCalling(random, now), cancellationToken);

    /// <summary>
    /// The scheduler's tick for a session (docs/02 §5), doing whatever is due:
    /// <list type="bullet">
    /// <item>a countdown that reached zero starts the round (FR-45), resetting every player's
    /// marks for it, and draws the first ball if it was started in auto mode;</item>
    /// <item>in auto mode, the next ball is drawn.</item>
    /// </list>
    /// A session that changed since it was scheduled (a pause or cancel racing the tick) is
    /// skipped quietly: nothing changes and nothing is published.
    /// </summary>
    public Task<Result<CommandOutcome>> TickAsync(GameCode code, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            Caller.System,
            async (session, now, ct) =>
            {
                switch (session.State)
                {
                    case SessionState.Starting when now >= session.CountdownEndsAt:
                        var started = session.StartRound(random, now);
                        if (!started.IsSuccess)
                        {
                            return started.Error!;
                        }

                        _ = await RoundHandler.ResetParticipantsAsync(runner.Store, session, redraw: null, ct).ConfigureAwait(false);
                        return await FollowUpAsync(session, started.Value, now, ct).ConfigureAwait(false);
                    case SessionState.Auto:
                        var drawn = session.DrawBall(DrawSource.Auto, random, now);
                        return drawn.IsSuccess ? await FollowUpAsync(session, drawn.Value, now, ct).ConfigureAwait(false) : drawn.Error!;
                    default:
                        return Result.Success(CommandOutcome.Of([]));
                }
            },
            cancellationToken);

    private Task<Result<CommandOutcome>> DrawAsync(
        GameCode code,
        Caller caller,
        Func<Session, DateTimeOffset, Result<IReadOnlyList<SessionEvent>>> draw,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var drawn = draw(session, now);
                return drawn.IsSuccess ? await FollowUpAsync(session, drawn.Value, now, ct).ConfigureAwait(false) : drawn.Error!;
            },
            cancellationToken);

    /// <summary>After a draw: auto-fill the previous ball, then check for wins.</summary>
    private async Task<Result<CommandOutcome>> FollowUpAsync(
        Session session, IReadOnlyList<SessionEvent> events, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (events.OfType<BallCalled>().SingleOrDefault() is not { } called)
        {
            return Result.Success(CommandOutcome.Of(events));
        }

        var store = runner.Store;
        var participants = await store.GetParticipantsAsync(session.Code, cancellationToken).ConfigureAwait(false);
        var notifications = new List<(Recipient, Notification)>();
        if (called.PreviousNumber is { } previous)
        {
            foreach (var participant in participants)
            {
                var filled = participant.ApplyAutoFill(previous, session);
                if (filled.Count > 0)
                {
                    await store.SaveParticipantAsync(session.Code, participant, cancellationToken).ConfigureAwait(false);
                    notifications.Add((new Recipient.Participant(participant.Id), new AutoFillApplied(filled)));
                }
            }
        }

        var detected = session.DetectWins(participants, now);
        IReadOnlyList<SessionEvent> all = detected.IsSuccess ? [.. events, .. detected.Value] : events;
        return Result.Success(new CommandOutcome(all, notifications));
    }
}
