using SyncBingo.Application.Players;
using SyncBingo.Application.Ports;
using SyncBingo.Application.Queries;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>The rules in force after an Apply: the settings and the pattern.</summary>
public sealed record RulesApplied(SessionSettings Settings, string WinConditionId);

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

    /// <summary>
    /// The host's <b>Apply</b> in Options (docs/06): new settings and, if given, a new pattern, in
    /// one step and only in the lobby or manual mode (<see cref="Session.ApplyRules"/>).
    /// <paramref name="change"/> runs under the lock on the current settings, so it changes only
    /// what the host changed. Returns the rules now in force.
    /// </summary>
    public async Task<Result<RulesApplied>> ApplyRulesAsync(
        GameCode code, Caller caller, Func<SessionSettings, SessionSettings> change, string? winConditionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        RulesApplied? applied = null;
        var result = await runner.RunAsync(
                code,
                caller,
                (session, now) =>
                {
                    var outcome = session.ApplyRules(change(session.Settings), winConditionId ?? session.WinConditionId, now);
                    applied = new RulesApplied(session.Settings, session.WinConditionId);
                    return outcome;
                },
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess ? Result.Success(applied!) : result.Error!;
    }

    /// <summary>The note shown with the join QR on the display (FR-49), or <c>null</c> to clear it.</summary>
    public Task<Result<CommandOutcome>> SetJoinNoteAsync(GameCode code, Caller caller, string? note, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.SetJoinNote(note, now), cancellationToken);

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
                return notifications.IsSuccess ? Result.Success(new CommandOutcome(cleared.Value, notifications.Value)) : notifications.Error!;
            },
            cancellationToken);

    /// <summary>
    /// Resets every participant's marks for the session's current settings, dealing each a new
    /// unique card when <paramref name="redraw"/> is given. Returns the card-replaced notifications,
    /// or an error if a participant could not be saved (the caller then saves nothing).
    /// </summary>
    internal static async Task<Result<IReadOnlyList<(Recipient, Notification)>>> ResetParticipantsAsync(
        ISessionStore store, Session session, IRandomSource? redraw, CancellationToken cancellationToken)
    {
        var code = session.Code;
        var participants = await store.GetParticipantsAsync(code, cancellationToken).ConfigureAwait(false);
        var notifications = new List<(Recipient, Notification)>();
        if (redraw is not null)
        {
            await store.ReleaseAllCardsAsync(code, cancellationToken).ConfigureAwait(false);
        }

        var generator = CardGenerators.For(session.Variant);
        foreach (var stored in participants)
        {
            var newCards = new List<Card>();
            for (var i = 0; redraw is not null && i < stored.Participant.Cards.Count; i++)
            {
                newCards.Add(await generator
                    .DealUniqueAsync(redraw, (fingerprint, ct) => store.TryClaimCardAsync(code, fingerprint, ct), cancellationToken)
                    .ConfigureAwait(false));
            }

            var replaced = await ParticipantUpdates.UpdateAsync(
                    store,
                    code,
                    stored,
                    participant =>
                    {
                        var next = 0; // the same new cards on every try
                        return Result.Success(participant.ResetForNewRound(session, redraw is not null ? () => newCards[next++] : null));
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!replaced.IsSuccess)
            {
                return replaced.Error!;
            }

            foreach (var replacement in replaced.Value)
            {
                notifications.Add((new Recipient.Participant(stored.Participant.Id), new CardReplaced(replacement.CardId, session.Content is null ? replacement.NewCard.Cells : CardView.Placeholders(replacement.NewCard))));
            }
        }

        return Result.Success<IReadOnlyList<(Recipient, Notification)>>(notifications);
    }
}
