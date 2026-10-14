using SyncBingo.Application.Displays;
using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Pairing;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Pairing;

/// <summary>A TV waiting to be connected: the secret id it listens on, the code it shows, and when that expires.</summary>
public sealed record PairingStarted(string PairingId, PairingCode Code, DateTimeOffset ExpiresAt);

/// <summary>What a paired TV receives (ADR 0010): the game to show and its read-only display token.</summary>
public sealed record PairingResult(string SessionCode, string DisplayToken);

/// <summary>
/// Pairings live outside any game until approved (ADR 0010). A code is claimed at most once; a result is
/// handed over at most once and kept only briefly.
/// </summary>
public interface IPairingStore
{
    /// <summary>Stores a pairing and its code for <paramref name="lifetime"/>; <c>false</c> if the code is in use.</summary>
    Task<bool> TryCreateAsync(string pairingId, PairingCode code, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Claims a code once: the waiting pairing's id, or <c>null</c> if the code is unknown, used or expired.</summary>
    Task<string?> TryClaimAsync(PairingCode code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string pairingId, CancellationToken cancellationToken);

    /// <summary>Keeps the result for a TV that connects a moment later; <see cref="TakeResultAsync"/> removes it.</summary>
    Task SaveResultAsync(string pairingId, PairingResult result, TimeSpan keepFor, CancellationToken cancellationToken);

    Task<PairingResult?> TakeResultAsync(string pairingId, CancellationToken cancellationToken);
}

/// <summary>Pushes a result to the TV waiting on that pairing (SignalR in production).</summary>
public interface IPairingNotifier
{
    Task PairedAsync(string pairingId, PairingResult result, CancellationToken cancellationToken);
}

/// <summary>
/// Connecting a smart TV or projector with a short code (ADR 0010), like "enter the code shown on your TV"
/// sign-ins with the roles reversed: the TV shows the code and the signed-in host approves it.
/// </summary>
public sealed class PairingHandler(
    IPairingStore pairings,
    IPairingNotifier notifier,
    DisplayHandler displays,
    IRandomSource random,
    IClock clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>How long an approved result waits for a TV that wasn't connected at that moment.</summary>
    public static readonly TimeSpan ResultKeptFor = TimeSpan.FromMinutes(2);

    private const int CodeAttempts = 5;

    /// <summary>The TV asks for a code (anonymous; rate-limited per address by the API).</summary>
    public async Task<PairingStarted> StartAsync(CancellationToken cancellationToken)
    {
        var pairingId = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());
        for (var attempt = 0; attempt < CodeAttempts; attempt++)
        {
            var code = PairingCode.Generate(random);
            if (await pairings.TryCreateAsync(pairingId, code, Lifetime, cancellationToken).ConfigureAwait(false))
            {
                return new PairingStarted(pairingId, code, clock.UtcNow + Lifetime);
            }
        }

        throw new InvalidOperationException($"No free pairing code after {CodeAttempts} attempts.");
    }

    /// <summary>
    /// The host types the TV's code: the code is used up, the TV gets a read-only display token (a display
    /// of kind <see cref="DisplayKind.Tv"/>, so the 10-display cap and Disconnect apply), and the result is pushed.
    /// </summary>
    public async Task<Result<string>> ApproveAsync(GameCode code, Caller caller, string typedCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        // Check the game first, so a full game doesn't use up the TV's code.
        var existing = await displays.ListAsync(code, caller, cancellationToken).ConfigureAwait(false);
        if (!existing.IsSuccess)
        {
            return existing.Error!;
        }

        if (existing.Value.Count >= DisplayHandler.MaxDisplays)
        {
            return new DomainError(DomainErrorCode.Conflict, $"A game can have up to {DisplayHandler.MaxDisplays} displays. Disconnect one first.");
        }

        if (!PairingCode.TryParse(typedCode, out var pairingCode)
            || await pairings.TryClaimAsync(pairingCode, cancellationToken).ConfigureAwait(false) is not { } pairingId)
        {
            return new DomainError(DomainErrorCode.NotFound, "That code isn't valid or has expired. Check the code on the TV.");
        }

        var issued = await displays.IssueAsync(code, caller, DisplayKind.Tv, cancellationToken).ConfigureAwait(false);
        if (!issued.IsSuccess)
        {
            return issued.Error!;
        }

        var result = new PairingResult(code.Value, issued.Value.Token);
        await pairings.SaveResultAsync(pairingId, result, ResultKeptFor, cancellationToken).ConfigureAwait(false);
        await notifier.PairedAsync(pairingId, result, cancellationToken).ConfigureAwait(false);
        return Result.Success(issued.Value.DisplayId);
    }
}
