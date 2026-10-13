using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Players;

/// <summary>
/// Saving a participant without losing anyone else's change (docs/02 §6). A player's own
/// commands take no session lock, and a draw's auto-fill can write the same participant, so
/// every save is checked against the version that was loaded. A save that lost the race
/// reloads and applies the change again; the domain rules decide again on fresh data.
/// </summary>
internal static class ParticipantUpdates
{
    /// <summary>Tries before giving up; each retry means someone else's write just succeeded.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// A player's own change, without the session lock: the session and the participant are
    /// both reloaded on every try, so a change that raced a new round is judged under it.
    /// </summary>
    public static async Task<Result<T>> ChangeOwnAsync<T>(
        ISessionStore store,
        GameCode code,
        Caller caller,
        Func<Participant, Session, Result<T>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireParticipant() is { } forbidden)
        {
            return forbidden;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return Errors.SessionNotFound();
            }

            var stored = await store.GetParticipantAsync(code, caller.ParticipantId!, cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                return Errors.NotInGame();
            }

            var result = change(stored.Participant, session);
            if (!result.IsSuccess
                || await store.TryUpdateParticipantAsync(code, stored.Participant, stored.Version, cancellationToken).ConfigureAwait(false))
            {
                return result;
            }
        }

        return Errors.Busy();
    }

    /// <summary>
    /// A change made under the session lock (auto-fill, round resets, redraws): the session is
    /// already loaded and locked, so only the participant is reloaded after a lost race.
    /// <paramref name="changed"/> says whether a successful result needs saving at all.
    /// </summary>
    public static async Task<Result<T>> UpdateAsync<T>(
        ISessionStore store,
        GameCode code,
        StoredParticipant loaded,
        Func<Participant, Result<T>> change,
        CancellationToken cancellationToken,
        Func<T, bool>? changed = null)
    {
        var current = loaded;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var result = change(current.Participant);
            if (!result.IsSuccess
                || changed?.Invoke(result.Value) == false
                || await store.TryUpdateParticipantAsync(code, current.Participant, current.Version, cancellationToken).ConfigureAwait(false))
            {
                return result;
            }

            current = await store.GetParticipantAsync(code, current.Participant.Id, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return Errors.PlayerNotFound();
            }
        }

        return Errors.Busy();
    }
}
