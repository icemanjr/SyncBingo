using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Players;

/// <summary>
/// What a player does with their own cards (docs/05 §2). Only a participant may call these,
/// and only on their own cards: the participant comes from the caller's token and the card
/// id is looked up among that participant's cards, so no request can name anyone else's.
/// Marks, Undo and the auto-fill switch are private and take no session lock; a redraw and a
/// Bingo press do (a redraw must not race the countdown, a claim is shared state).
/// </summary>
public sealed class PlayerCommandHandler(SessionCommandRunner runner, IEventPublisher publisher, IRandomSource random)
{
    private ISessionStore Store => runner.Store;

    /// <summary>Mark or unmark a cell (FR-22, FR-24). Returns the card's marks.</summary>
    public Task<Result<uint>> MarkAsync(GameCode code, Caller caller, int cardId, int cell, bool marked, CancellationToken cancellationToken) =>
        ParticipantUpdates.ChangeOwnAsync(
            Store,
            code,
            caller,
            (participant, session) => marked ? participant.Mark(cardId, cell, session) : participant.Unmark(cardId, cell, session),
            cancellationToken);

    /// <summary>Undo (FR-25): back to the card as it was when the last ball was called. Returns the marks.</summary>
    public Task<Result<uint>> UndoAsync(GameCode code, Caller caller, int cardId, CancellationToken cancellationToken) =>
        ParticipantUpdates.ChangeOwnAsync(Store, code, caller, (participant, session) => participant.Undo(cardId, session), cancellationToken);

    /// <summary>The player's own auto-fill switch, while the host makes it available (FR-16).</summary>
    public Task<Result<bool>> SetAutoFillAsync(GameCode code, Caller caller, bool on, CancellationToken cancellationToken) =>
        ParticipantUpdates.ChangeOwnAsync(Store, code, caller, (participant, session) => participant.SetAutoFill(on, session), cancellationToken);

    /// <summary>
    /// Swap a card for a new unique one between rounds, if the host allows it (FR-20, FR-39).
    /// Runs under the session lock so it cannot race the countdown or a host redraw. The new
    /// card is claimed only once the redraw is allowed; the old one is released once it is saved.
    /// </summary>
    public async Task<Result<Card>> RedrawCardAsync(GameCode code, Caller caller, int cardId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireParticipant() is { } forbidden)
        {
            return forbidden;
        }

        await using var sessionLock = (await Store.LockAsync(code, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        var session = await Store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        var stored = await Store.GetParticipantAsync(code, caller.ParticipantId!, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return Errors.NotInGame();
        }

        var allowed = stored.Participant.CheckRedraw(cardId, session);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var newCard = await new ColumnCardGenerator(session.Variant)
            .DealUniqueAsync(random, (fingerprint, ct) => Store.TryClaimCardAsync(code, fingerprint, ct), cancellationToken)
            .ConfigureAwait(false);
        var replaced = await ParticipantUpdates
            .UpdateAsync(Store, code, stored, participant => participant.RedrawCard(cardId, session, () => newCard), cancellationToken)
            .ConfigureAwait(false);
        if (!replaced.IsSuccess)
        {
            await Store.ReleaseCardAsync(code, newCard.Fingerprint, CancellationToken.None).ConfigureAwait(false);
            return replaced.Error!;
        }

        await Store.ReleaseCardAsync(code, replaced.Value.OldCard.Fingerprint, cancellationToken).ConfigureAwait(false);
        await publisher.NotifyAsync(
                code, new Recipient.Participant(caller.ParticipantId!), new CardReplaced(cardId, newCard.Cells), cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(newCard);
    }

    /// <summary>
    /// The Bingo button (FR-37a): recorded and sent to the host whether or not it qualifies.
    /// The claim reads the player's card but changes only the session.
    /// </summary>
    public Task<Result<CommandOutcome>> ClaimBingoAsync(GameCode code, Caller caller, int cardId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireParticipant() is { } forbidden)
        {
            return Task.FromResult<Result<CommandOutcome>>(forbidden);
        }

        return runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var stored = await Store.GetParticipantAsync(code, caller.ParticipantId!, ct).ConfigureAwait(false);
                if (stored is null)
                {
                    return Errors.NotInGame();
                }

                var claimed = session.ClaimBingo(stored.Participant, cardId, now);
                return claimed.IsSuccess ? Result.Success(CommandOutcome.Of(claimed.Value)) : claimed.Error!;
            },
            cancellationToken,
            hostOnly: false);
    }
}
