using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Content;
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
    /// <summary>Reads and checks the set file (before taking the lock), then uses it and redeals every card.</summary>
    public Task<Result<CommandOutcome>> UseAsync(GameCode code, Caller caller, string file, bool groupItems, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(file);

        if (caller.RequireHost() is { } forbidden)
        {
            return Task.FromResult<Result<CommandOutcome>>(forbidden);
        }

        var set = ContentSetFile.Read(file);
        return set.IsSuccess ? ChangeAsync(code, caller, set.Value, groupItems, cancellationToken) : Task.FromResult<Result<CommandOutcome>>(set.Error!);
    }

    /// <summary>Back to numbers: the standard balls and a new card for everyone.</summary>
    public Task<Result<CommandOutcome>> UseNumbersAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        ChangeAsync(code, caller, content: null, groupItems: false, cancellationToken);

    /// <summary>The set in use, as its file; the host only.</summary>
    public async Task<Result<ContentDownload>> DownloadAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        var session = await runner.Store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        // Not 404 when there is no set: clients read a 404 as "the game is over".
        return session.Content is { } content
            ? Result.Success(new ContentDownload(content.Name, ContentSetFile.Write(content)))
            : Result.Failure<ContentDownload>(DomainErrorCode.WrongState, "This game uses numbers, not a custom set.");
    }

    private Task<Result<CommandOutcome>> ChangeAsync(
        GameCode code, Caller caller, ContentSet? content, bool groupItems, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var changed = session.UseContent(content, groupItems, now);
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
