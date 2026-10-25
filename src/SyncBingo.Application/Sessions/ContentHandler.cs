using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>A game's custom set as its file, for the host to download and keep (ADR 0009).</summary>
public sealed record ContentDownload(string Name, string File);

/// <summary>
/// The host's custom set (FR-44, ADR 0009): "Use this set" with the editor's whole draft, going back to
/// numbers, and downloading the set in use. Changing the set is lobby-only and deals every player a new
/// card, as Deal New Cards does.
/// </summary>
public sealed class ContentHandler(SessionCommandRunner runner, IRandomSource random)
{
    /// <summary>
    /// Reads and checks the set file (before taking the lock), then uses it and redeals every card; with
    /// <paramref name="cardSize"/>, on that card (FR-26a), so a set and its card change together.
    /// </summary>
    public Task<Result<CommandOutcome>> UseAsync(GameCode code, Caller caller, string file, CardSize? cardSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(file);

        if (caller.RequireHost() is { } forbidden)
        {
            return Task.FromResult<Result<CommandOutcome>>(forbidden);
        }

        var set = ContentSetFile.Read(file);
        return set.IsSuccess ? ChangeAsync(code, caller, set.Value, cardSize, cancellationToken) : Task.FromResult<Result<CommandOutcome>>(set.Error!);
    }

    /// <summary>Back to numbers: the standard balls and a new card for everyone.</summary>
    public Task<Result<CommandOutcome>> UseNumbersAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        ChangeAsync(code, caller, content: null, cardSize: null, cancellationToken);

    /// <summary>Lets players download the set in use, or stops them (off whenever a set is first used).</summary>
    public Task<Result<CommandOutcome>> SetSharingAsync(GameCode code, Caller caller, bool playersMayDownload, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.SetContentSharing(playersMayDownload, now), cancellationToken);

    /// <summary>
    /// The set in use, as its file: the host any time; a player only when the host allows it and the game
    /// is in the lobby (the file pairs every clue with its word).
    /// </summary>
    public async Task<Result<ContentDownload>> DownloadAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.Role is not (CallerRole.Host or CallerRole.Participant))
        {
            return new DomainError(DomainErrorCode.Forbidden, "Only the host and players can download the set.");
        }

        var session = await runner.Store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        if (caller.Role == CallerRole.Participant && session.Content is not null && !session.PlayersCanDownloadNow)
        {
            return new DomainError(
                DomainErrorCode.Forbidden,
                session.PlayersMayDownload ? "The set can be downloaded between rounds." : "The host hasn't shared this set.");
        }

        // Not 404 when there is no set: clients read a 404 as "the game is over".
        return session.Content is { } content
            ? Result.Success(new ContentDownload(content.Name, ContentSetFile.Write(content)))
            : Result.Failure<ContentDownload>(DomainErrorCode.WrongState, "This game uses numbers, not a custom set.");
    }

    private Task<Result<CommandOutcome>> ChangeAsync(
        GameCode code, Caller caller, ContentSet? content, CardSize? cardSize, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var changed = session.UseContent(content, now, cardSize);
                if (!changed.IsSuccess)
                {
                    return changed.Error!;
                }

                // Cards hold item ids of the old set: everyone gets a new one (ADR 0009).
                var notifications = await RoundHandler.ResetParticipantsAsync(runner.Store, session, random, ct).ConfigureAwait(false);
                return notifications.IsSuccess ? Result.Success(new CommandOutcome(changed.Value, notifications.Value)) : notifications.Error!;
            },
            cancellationToken);
}
