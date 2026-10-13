using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>What a shared-state command produced: session events, plus private notifications sent first.</summary>
public sealed record CommandOutcome(
    IReadOnlyList<SessionEvent> Events,
    IReadOnlyList<(Recipient Recipient, Notification Notification)> Notifications)
{
    public static CommandOutcome Of(IReadOnlyList<SessionEvent> events) => new(events, []);
}

/// <summary>
/// The pipeline every shared-state command follows (docs/02 §3.2): authorise, take the
/// session lock, load, apply the domain rule, persist, keep the auto-call schedule in
/// step with the state, then publish. Private notifications go out before the session
/// events, so auto-fill reaches a player before the next ball is announced (FR-16).
/// </summary>
public sealed class SessionCommandRunner(ISessionStore store, IEventPublisher publisher, ICallScheduler scheduler, IClock clock)
{
    public ISessionStore Store => store;

    public async Task<Result<CommandOutcome>> RunAsync(
        GameCode code,
        Caller caller,
        Func<Session, DateTimeOffset, CancellationToken, Task<Result<CommandOutcome>>> apply,
        CancellationToken cancellationToken,
        bool hostOnly = true)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(apply);

        if (hostOnly && caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        await using var sessionLock = (await store.LockAsync(code, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        var wasScheduled = IsScheduled(session.State);
        var now = clock.UtcNow;
        var result = await apply(session, now, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || (result.Value.Events.Count == 0 && result.Value.Notifications.Count == 0))
        {
            return result;
        }

        await store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        await SyncScheduleAsync(session, wasScheduled, result.Value.Events, now, cancellationToken).ConfigureAwait(false);

        foreach (var (recipient, notification) in result.Value.Notifications)
        {
            await publisher.NotifyAsync(code, recipient, notification, cancellationToken).ConfigureAwait(false);
        }

        if (result.Value.Events.Count > 0)
        {
            await publisher.PublishAsync(code, result.Value.Events, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>A command that only changes the session, with no extra work.</summary>
    public Task<Result<CommandOutcome>> RunAsync(
        GameCode code, Caller caller, Func<Session, DateTimeOffset, Result<IReadOnlyList<SessionEvent>>> apply, CancellationToken cancellationToken) =>
        RunAsync(
            code,
            caller,
            (session, now, _) =>
            {
                var result = apply(session, now);
                return Task.FromResult(result.IsSuccess ? Result.Success(CommandOutcome.Of(result.Value)) : (Result<CommandOutcome>)result.Error!);
            },
            cancellationToken);

    /// <summary>
    /// The schedule holds the next thing due for a session: the end of a round countdown
    /// (FR-45), or in auto mode the next ball, one interval after the latest draw. Leaving
    /// both (a cancel, Pause, Alert and pause, Clear cards, ending) cancels it.
    /// </summary>
    private async Task SyncScheduleAsync(
        Session session, bool wasScheduled, IReadOnlyList<SessionEvent> events, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (session.State == SessionState.Starting && session.CountdownEndsAt is { } endsAt)
        {
            await scheduler.ScheduleAsync(session.Code, endsAt, cancellationToken).ConfigureAwait(false);
        }
        else if (session.State == SessionState.Auto && events.OfType<BallCalled>().Any())
        {
            await scheduler.ScheduleAsync(session.Code, now + session.Settings.AutoCallInterval, cancellationToken).ConfigureAwait(false);
        }
        else if (wasScheduled && !IsScheduled(session.State))
        {
            await scheduler.CancelAsync(session.Code, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsScheduled(SessionState state) => state is SessionState.Starting or SessionState.Auto;
}
