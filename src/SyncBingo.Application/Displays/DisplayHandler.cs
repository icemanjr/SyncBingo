using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Displays;

/// <summary>A new display: its id and token (returned once; only the token's hash is stored).</summary>
public sealed record DisplayIssued(string DisplayId, string Token);

/// <summary>
/// The host's display views (FR-28): read-only screens such as a projector tab or a TV. Each has its
/// own display token, so the host can see them all and disconnect any one without touching the others.
/// </summary>
public sealed class DisplayHandler(ISessionStore store, ITokenService tokens, IEventPublisher publisher, IClock clock)
{
    /// <summary>More displays than any room needs; a cap so a game can't collect unlimited tokens.</summary>
    public const int MaxDisplays = 10;

    /// <summary>A display link (FR-28): opened in a new tab or sent to another device.</summary>
    public Task<Result<DisplayIssued>> CreateLinkAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        IssueAsync(code, caller, DisplayKind.Link, cancellationToken);

    /// <summary>Issues a display of any kind; a paired TV (ADR 0010) gets its token this way too.</summary>
    public async Task<Result<DisplayIssued>> IssueAsync(GameCode code, Caller caller, DisplayKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (await CheckAsync(code, caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var token = tokens.Issue();
        var display = new DisplayRecord(Guid.NewGuid().ToString("N"), kind, clock.UtcNow);
        return await store.TryAddDisplayAsync(code, display, token.Hash, MaxDisplays, cancellationToken).ConfigureAwait(false)
            ? Result.Success(new DisplayIssued(display.DisplayId, token.Token))
            : TooManyDisplays();
    }

    public async Task<Result<IReadOnlyList<DisplayRecord>>> ListAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (await CheckAsync(code, caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        return Result.Success(await store.GetDisplaysAsync(code, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Disconnects one display: its token stops working and the screen is told to close.</summary>
    public async Task<Result<bool>> DisconnectAsync(GameCode code, Caller caller, string displayId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (await CheckAsync(code, caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!await store.RemoveDisplayAsync(code, displayId, cancellationToken).ConfigureAwait(false))
        {
            return new DomainError(DomainErrorCode.NotFound, "That display isn't connected.");
        }

        await publisher.NotifyAsync(code, new Recipient.Display(displayId), new DisplayDisconnected(), cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    /// <summary>Revokes every display link (docs/05); paired TVs stay, each can be disconnected on its own.</summary>
    public async Task<Result<int>> RevokeLinksAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        var displays = await ListAsync(code, caller, cancellationToken).ConfigureAwait(false);
        if (!displays.IsSuccess)
        {
            return displays.Error!;
        }

        var links = displays.Value.Where(display => display.Kind == DisplayKind.Link).ToList();
        foreach (var link in links)
        {
            await DisconnectAsync(code, caller, link.DisplayId, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(links.Count);
    }

    /// <summary>The game already has <see cref="MaxDisplays"/> displays.</summary>
    internal static DomainError TooManyDisplays() =>
        new(DomainErrorCode.Conflict, $"A game can have up to {MaxDisplays} displays. Disconnect one first.");

    private async Task<DomainError?> CheckAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        if (caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        return await store.GetAsync(code, cancellationToken).ConfigureAwait(false) is { State: not SessionState.Ended }
            ? null
            : Errors.SessionNotFound();
    }
}
