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
    /// The scheduler's tick. A session that has left auto mode since it was scheduled
    /// (a pause racing the tick) is skipped quietly: nothing changes and nothing is published.
    /// </summary>
    public Task<Result<CommandOutcome>> AutoDrawAsync(GameCode code, CancellationToken cancellationToken) =>
        DrawAsync(
            code,
            Caller.System,
            (session, now) => session.State == SessionState.Auto
                ? session.DrawBall(DrawSource.Auto, random, now)
                : Result.Success<IReadOnlyList<SessionEvent>>([]),
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
                if (!drawn.IsSuccess || drawn.Value.Count == 0)
                {
                    return drawn.IsSuccess ? Result.Success(CommandOutcome.Of([])) : drawn.Error!;
                }

                var store = runner.Store;
                var participants = await store.GetParticipantsAsync(code, ct).ConfigureAwait(false);
                var notifications = new List<(Recipient, Notification)>();
                if (drawn.Value.OfType<BallCalled>().Single().PreviousNumber is { } previous)
                {
                    foreach (var participant in participants)
                    {
                        var filled = participant.ApplyAutoFill(previous, session);
                        if (filled.Count > 0)
                        {
                            await store.SaveParticipantAsync(code, participant, ct).ConfigureAwait(false);
                            notifications.Add((new Recipient.Participant(participant.Id), new AutoFillApplied(filled)));
                        }
                    }
                }

                var detected = session.DetectWins(participants, now);
                IReadOnlyList<SessionEvent> events = detected.IsSuccess ? [.. drawn.Value, .. detected.Value] : drawn.Value;
                return Result.Success(new CommandOutcome(events, notifications));
            },
            cancellationToken);
}
