using System.Globalization;
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
public sealed class HostCommandHandler(SessionCommandRunner runner, IRandomSource random)
{
    /// <summary>Ends the round early; anyone waiting joins now (A3).</summary>
    public Task<Result<CommandOutcome>> EndRoundAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        RoundHandler.ThenAdmitAsync(runner, random, code, caller, (session, now) => session.EndRound(now), cancellationToken);

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
                async (session, now, ct) =>
                {
                    var outcome = session.ApplyRules(change(session.Settings), winConditionId ?? session.WinConditionId, now);
                    if (!outcome.IsSuccess)
                    {
                        return outcome.Error!;
                    }

                    applied = new RulesApplied(session.Settings, session.WinConditionId);
                    if (!session.CardShapeChanged)
                    {
                        return Result.Success(CommandOutcome.Of(outcome.Value));
                    }

                    // Another card size, or a free space added or taken away (lobby only): the old cards no longer fit,
                    // so everyone gets a new one.
                    var notifications = await RoundHandler.ResetParticipantsAsync(runner.Store, session, random, ct).ConfigureAwait(false);
                    return notifications.IsSuccess ? Result.Success(new CommandOutcome(outcome.Value, notifications.Value)) : notifications.Error!;
                },
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess ? Result.Success(applied!) : result.Error!;
    }

    /// <summary>The note shown with the join QR on the display (FR-49), or <c>null</c> to clear it.</summary>
    public Task<Result<CommandOutcome>> SetJoinNoteAsync(GameCode code, Caller caller, string? note, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.SetJoinNote(note, now), cancellationToken);

    /// <summary>Adds a pattern the host drew, on the card in use (FR-21a); patterns can't be deleted.</summary>
    public Task<Result<CommandOutcome>> AddPatternAsync(GameCode code, Caller caller, string? name, IReadOnlyCollection<int> cells, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.AddPattern(name, cells, now), cancellationToken);

    /// <summary>Renames one of the host's patterns (FR-21a), e.g. a mistake to "Do not use".</summary>
    public Task<Result<CommandOutcome>> RenamePatternAsync(GameCode code, Caller caller, string id, string? name, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.RenamePattern(id, name, now), cancellationToken);

    /// <summary>Which face of a custom set the call boards show (FR-29).</summary>
    public Task<Result<CommandOutcome>> SetBoardSideAsync(GameCode code, Caller caller, BoardSide side, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.SetBoardSide(side, now), cancellationToken);

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

    /// <summary>Back to the lobby; anyone who arrived during the countdown joins now (A3).</summary>
    public Task<Result<CommandOutcome>> CancelCountdownAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        ThenAdmitAsync(runner, random, code, caller, (session, now) => session.CancelCountdown(now), cancellationToken);

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
                if (!notifications.IsSuccess)
                {
                    return notifications.Error!;
                }

                var admitted = await AdmitWaitingAsync(runner.Store, session, random, ct).ConfigureAwait(false);
                return Result.Success(new CommandOutcome(cleared.Value, [.. notifications.Value, .. admitted]));
            },
            cancellationToken);

    /// <summary>Runs a command that returns the game to the lobby, then admits anyone waiting (A3).</summary>
    internal static Task<Result<CommandOutcome>> ThenAdmitAsync(
        SessionCommandRunner runner,
        IRandomSource random,
        GameCode code,
        Caller caller,
        Func<Session, DateTimeOffset, Result<IReadOnlyList<SessionEvent>>> command,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var done = command(session, now);
                if (!done.IsSuccess)
                {
                    return done.Error!;
                }

                var admitted = await AdmitWaitingAsync(runner.Store, session, random, ct).ConfigureAwait(false);
                return Result.Success(new CommandOutcome(done.Value, admitted));
            },
            cancellationToken);

    /// <summary>
    /// The waiting room (A3): once the game is back in the lobby, everyone who joined during the round gets a
    /// card, in the order they arrived. If the rules run out of different cards (FR-39), the rest keep waiting.
    /// Each is told (their app loads the card) and so is the host.
    /// </summary>
    internal static async Task<IReadOnlyList<(Recipient, Notification)>> AdmitWaitingAsync(
        ISessionStore store, Session session, IRandomSource random, CancellationToken cancellationToken)
    {
        var notifications = new List<(Recipient, Notification)>();
        if (session.State != SessionState.BetweenRounds)
        {
            return notifications;
        }

        var code = session.Code;
        var waiting = (await store.GetParticipantsAsync(code, cancellationToken).ConfigureAwait(false))
            .Where(stored => stored.Participant.IsWaiting)
            .OrderBy(stored => stored.Participant.JoinedAt);
        var generator = CardGenerators.For(session.Variant);
        foreach (var stored in waiting)
        {
            var dealt = await generator
                .DealUniqueAsync(random, (fingerprint, ct) => store.TryClaimCardAsync(code, fingerprint, ct), session.Variant.PossibleCards, cancellationToken)
                .ConfigureAwait(false);
            if (!dealt.IsSuccess)
            {
                break;
            }

            var admitted = await ParticipantUpdates.UpdateAsync(
                    store,
                    code,
                    stored,
                    participant =>
                    {
                        if (!participant.IsWaiting)
                        {
                            return Result.Failure<bool>(DomainErrorCode.Conflict, "Already admitted.");
                        }

                        participant.Admit(dealt.Value, session);
                        return Result.Success(true);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!admitted.IsSuccess)
            {
                await store.ReleaseCardAsync(code, dealt.Value.Fingerprint, CancellationToken.None).ConfigureAwait(false);
                continue;
            }

            var id = stored.Participant.Id;
            notifications.Add((new Recipient.Participant(id), new CardReplaced(1, CardView.CellsFor(dealt.Value, session.Content))));
            notifications.Add((new Recipient.Host(), new ParticipantJoined(id, stored.Participant.Name.Display)));
        }

        return notifications;
    }

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
            // Check before anything changes: a small set may make fewer different cards than there are players.
            var cardsNeeded = participants.Sum(stored => stored.Participant.Cards.Count);
            if (cardsNeeded > session.Variant.PossibleCards)
            {
                return new DomainError(
                    DomainErrorCode.SessionFull,
                    $"These rules make only {session.Variant.PossibleCards.ToString("N0", CultureInfo.InvariantCulture)} different cards, and the players need {cardsNeeded}. Add entries to the set.");
            }

            await store.ReleaseAllCardsAsync(code, cancellationToken).ConfigureAwait(false);
        }

        var generator = CardGenerators.For(session.Variant);
        foreach (var stored in participants)
        {
            var newCards = new List<Card>();
            for (var i = 0; redraw is not null && i < stored.Participant.Cards.Count; i++)
            {
                var dealt = await generator
                    .DealUniqueAsync(redraw, (fingerprint, ct) => store.TryClaimCardAsync(code, fingerprint, ct), session.Variant.PossibleCards, cancellationToken)
                    .ConfigureAwait(false);
                if (!dealt.IsSuccess)
                {
                    return dealt.Error!;
                }

                newCards.Add(dealt.Value);
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
                notifications.Add((
                    new Recipient.Participant(stored.Participant.Id),
                    new CardReplaced(replacement.CardId, CardView.CellsFor(replacement.NewCard, session.Content))));
            }
        }

        return Result.Success<IReadOnlyList<(Recipient, Notification)>>(notifications);
    }
}
