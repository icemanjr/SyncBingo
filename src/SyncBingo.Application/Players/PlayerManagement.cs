using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Players;

/// <summary>An award from the host (ADR 0008, FR-43); see <see cref="Session.AwardWin"/>.</summary>
public sealed record AwardWin(string ParticipantId, int? CardId = null, int Wins = 1, int? ClaimId = null, string? IdempotencyKey = null);

/// <summary>
/// The host's commands about players: crediting wins, dismissing claims and removing a
/// player (FR-7). Host only, under the session lock.
/// </summary>
public sealed class PlayerManagementHandler(SessionCommandRunner runner)
{
    public Task<Result<CommandOutcome>> AwardWinAsync(GameCode code, Caller caller, AwardWin command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var stored = await runner.Store.GetParticipantAsync(code, command.ParticipantId, ct).ConfigureAwait(false);
                if (stored is null)
                {
                    return Errors.PlayerNotFound();
                }

                var awarded = session.AwardWin(stored.Participant, command.CardId, now, command.Wins, command.ClaimId, command.IdempotencyKey);
                return awarded.IsSuccess ? Result.Success(CommandOutcome.Of(awarded.Value)) : awarded.Error!;
            },
            cancellationToken);
    }

    public Task<Result<CommandOutcome>> DismissClaimAsync(GameCode code, Caller caller, int claimId, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.DismissClaim(claimId, now), cancellationToken);

    /// <summary>
    /// Removes a player (FR-7): their pending claims are dismissed, their record and session
    /// tokens deleted, and their name and cards released for others. Wins already awarded
    /// stay in the history. A command they send afterwards finds no participant.
    /// </summary>
    public Task<Result<CommandOutcome>> RemoveParticipantAsync(
        GameCode code, Caller caller, string participantId, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var store = runner.Store;
                var stored = await store.GetParticipantAsync(code, participantId, ct).ConfigureAwait(false);
                if (stored is null)
                {
                    return Errors.PlayerNotFound();
                }

                var removed = session.RemoveParticipant(stored.Participant, now);
                if (!removed.IsSuccess)
                {
                    return removed.Error!;
                }

                await DeleteAsync(store, code, stored, ct).ConfigureAwait(false);
                return Result.Success(CommandOutcome.Of(removed.Value));
            },
            cancellationToken);

    /// <summary>Deletes a departing participant's record and tokens, and releases their name and cards for others.</summary>
    internal static async Task DeleteAsync(ISessionStore store, GameCode code, StoredParticipant stored, CancellationToken cancellationToken)
    {
        await store.DeleteParticipantAsync(code, stored.Participant.Id, cancellationToken).ConfigureAwait(false);
        await store.ReleaseNameAsync(code, stored.Participant.Name.Key, cancellationToken).ConfigureAwait(false);
        foreach (var card in stored.Participant.Cards)
        {
            await store.ReleaseCardAsync(code, card.Card.Fingerprint, cancellationToken).ConfigureAwait(false);
        }
    }
}
