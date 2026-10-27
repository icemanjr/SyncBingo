using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>
/// What the join page shows before anyone types a name (docs/05 §1): whether a round (or its countdown) is under
/// way, so a joiner waits for it to end before getting a card (A3), and which round is next.
/// </summary>
public sealed record JoinInfo(bool RoundInProgress, int Round);

public sealed class JoinInfoQuery(ISessionStore store)
{
    public async Task<Result<JoinInfo>> GetAsync(string code, CancellationToken cancellationToken)
    {
        if (!GameCode.TryParse(code, out var parsed)
            || await store.GetAsync(parsed, cancellationToken).ConfigureAwait(false) is not { State: not SessionState.Ended } session)
        {
            return Errors.SessionNotFound();
        }

        return Result.Success(new JoinInfo(session.JoinersWait, session.UpcomingRound));
    }
}
