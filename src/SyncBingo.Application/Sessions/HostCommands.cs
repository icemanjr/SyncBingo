using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>
/// Host commands that change only shared session state. Each runs through
/// <see cref="SessionCommandRunner"/> and maps to one domain method.
/// </summary>
public sealed class HostCommandHandler(SessionCommandRunner runner)
{
    public Task<Result<CommandOutcome>> EndRoundAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.EndRound(now), cancellationToken);

    public Task<Result<CommandOutcome>> PauseAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.Pause(now), cancellationToken);

    public Task<Result<CommandOutcome>> ChangeWinConditionAsync(
        GameCode code, Caller caller, string winConditionId, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.ChangeWinCondition(winConditionId, now), cancellationToken);

    public Task<Result<CommandOutcome>> UpdateSettingsAsync(
        GameCode code, Caller caller, SessionSettings settings, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.UpdateSettings(settings, now), cancellationToken);

    public Task<Result<CommandOutcome>> ShowLeaderboardAsync(GameCode code, Caller caller, bool visible, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.ShowLeaderboard(visible, now), cancellationToken);

    public Task<Result<CommandOutcome>> EndSessionAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.End(now), cancellationToken);
}

/// <summary>
/// Moving between rounds (FR-34, FR-45): "Start Round N" begins the countdown, the host
/// can cancel it, and "Clear cards" ends the round and returns to the lobby. The round
/// itself starts when the countdown reaches zero (<see cref="Calling.DrawHandler.TickAsync"/>).
/// </summary>
public sealed class RoundHandler(SessionCommandRunner runner, IRandomSource random)
{
    public Task<Result<CommandOutcome>> StartCountdownAsync(GameCode code, Caller caller, bool startInAuto, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.StartCountdown(startInAuto, now), cancellationToken);

    public Task<Result<CommandOutcome>> CancelCountdownAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.CancelCountdown(now), cancellationToken);

    /// <summary>
    /// Clears every player's marks; with <paramref name="redrawCards"/> every card is replaced
    /// by a new unique one (FR-39): all fingerprints are released first, then each new card is claimed.
    /// </summary>
    public Task<Result<CommandOutcome>> ClearCardsAsync(GameCode code, Caller caller, bool redrawCards, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var cleared = session.ClearCards(redrawCards, now);
                if (!cleared.IsSuccess)
                {
                    return cleared.Error!;
                }

                var notifications = await ResetParticipantsAsync(runner.Store, session, redrawCards ? random : null, ct).ConfigureAwait(false);
                return Result.Success(new CommandOutcome(cleared.Value, notifications));
            },
            cancellationToken);

    /// <summary>
    /// Resets every participant's marks for the session's current settings, dealing each a new
    /// unique card when <paramref name="redraw"/> is given. Returns the card-replaced notifications.
    /// </summary>
    internal static async Task<IReadOnlyList<(Recipient, Notification)>> ResetParticipantsAsync(
        ISessionStore store, Session session, IRandomSource? redraw, CancellationToken cancellationToken)
    {
        var code = session.Code;
        var participants = await store.GetParticipantsAsync(code, cancellationToken).ConfigureAwait(false);
        var notifications = new List<(Recipient, Notification)>();
        if (redraw is not null)
        {
            await store.ReleaseAllCardsAsync(code, cancellationToken).ConfigureAwait(false);
        }

        var generator = new ColumnCardGenerator(session.Variant);
        foreach (var participant in participants)
        {
            var newCards = new Queue<Card>();
            for (var i = 0; redraw is not null && i < participant.Cards.Count; i++)
            {
                newCards.Enqueue(await generator
                    .DealUniqueAsync(redraw, (fingerprint, ct) => store.TryClaimCardAsync(code, fingerprint, ct), cancellationToken)
                    .ConfigureAwait(false));
            }

            var replaced = participant.ResetForNewRound(session, redraw is not null ? newCards.Dequeue : null);
            await store.SaveParticipantAsync(code, participant, cancellationToken).ConfigureAwait(false);
            foreach (var replacement in replaced)
            {
                notifications.Add((new Recipient.Participant(participant.Id), new CardReplaced(replacement.CardId, replacement.NewCard.Cells)));
            }
        }

        return notifications;
    }
}
