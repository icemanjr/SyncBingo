using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>The host starts a new game (FR-2).</summary>
public sealed record CreateSession(string Password, SessionSettings? Settings = null, string WinConditionId = "normal");

/// <summary>The new game's code and the host's token (returned once; only its hash is stored).</summary>
public sealed record SessionCreated(GameCode Code, string HostToken);

public sealed class CreateSessionHandler(
    ISessionStore store,
    IPasswordHasher passwords,
    ITokenService tokens,
    IRandomSource random,
    IClock clock)
{
    public const int MinPasswordLength = 4;
    public const int MaxPasswordLength = 64;

    /// <summary>
    /// A clash between random 79-bit codes is practically impossible, but the store
    /// decides atomically, so a clash just means trying another code.
    /// </summary>
    private const int CodeAttempts = 5;

    public async Task<Result<SessionCreated>> HandleAsync(CreateSession command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var password = command.Password ?? string.Empty;
        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return Result.Failure<SessionCreated>(
                DomainErrorCode.Invalid, $"The game password must be {MinPasswordLength}–{MaxPasswordLength} characters.");
        }

        var passwordHash = passwords.Hash(password);
        for (var attempt = 0; attempt < CodeAttempts; attempt++)
        {
            var created = Session.Create(
                GameCode.Generate(random),
                StandardVariants.Classic75,
                command.Settings ?? SessionSettings.Default,
                command.WinConditionId,
                clock.UtcNow);
            if (!created.IsSuccess)
            {
                return created.Error!;
            }

            var session = created.Value;
            if (await store.TryCreateAsync(session, passwordHash, cancellationToken).ConfigureAwait(false))
            {
                var host = tokens.Issue();
                await store.SaveTokenAsync(session.Code, host.Hash, Caller.Host, cancellationToken).ConfigureAwait(false);
                return Result.Success(new SessionCreated(session.Code, host.Token));
            }
        }

        throw new InvalidOperationException($"No free game code after {CodeAttempts} attempts.");
    }
}
