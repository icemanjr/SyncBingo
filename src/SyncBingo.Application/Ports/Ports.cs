using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Ports;

/// <summary>Current time, injected so tests control it.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Salted, slow hashing for the game password (docs/04 §5).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>A new opaque token: <see cref="Token"/> goes to the client once, only <see cref="Hash"/> is stored.</summary>
public sealed record IssuedToken(string Token, string Hash);

/// <summary>Session tokens (ADR 0005): 256-bit random values stored as SHA-256 hashes.</summary>
public interface ITokenService
{
    IssuedToken Issue();

    string Hash(string token);
}

/// <summary>
/// The auto-call schedule (docs/02 §5): which sessions are in auto mode and when their
/// next ball is due. A background service claims due sessions and runs the auto draw.
/// </summary>
public interface ICallScheduler
{
    Task ScheduleAsync(GameCode code, DateTimeOffset dueAt, CancellationToken cancellationToken);

    Task CancelAsync(GameCode code, CancellationToken cancellationToken);
}

/// <summary>Who a private notification is for.</summary>
public abstract record Recipient
{
    public sealed record Host : Recipient;

    public sealed record Participant(string ParticipantId) : Recipient;
}

/// <summary>Something only one connection should hear, unlike session events, which fan out by audience.</summary>
public abstract record Notification;

public sealed record ParticipantJoined(string ParticipantId, string Name) : Notification;

public sealed record AutoFillApplied(IReadOnlyList<AutoFilledCell> Cells) : Notification;

public sealed record CardReplaced(int CardId, IReadOnlyList<int> Cells) : Notification;

/// <summary>Pushes events to connected clients (SignalR in production).</summary>
public interface IEventPublisher
{
    /// <summary>Session events, in order; the API decides each one's audience (docs/05 §4).</summary>
    Task PublishAsync(GameCode code, IReadOnlyList<SessionEvent> events, CancellationToken cancellationToken);

    Task NotifyAsync(GameCode code, Recipient recipient, Notification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Storage for live sessions (ADR 0003; Redis in production). Shared session state is
/// changed only while holding <see cref="LockAsync"/>; participants, names and card
/// fingerprints have their own atomic operations so players never wait on the host
/// (docs/02 §6, docs/04 §4).
/// </summary>
public interface ISessionStore
{
    /// <summary>Stores a new session with its password hash; <c>false</c> if the code is already in use.</summary>
    Task<bool> TryCreateAsync(Session session, string passwordHash, CancellationToken cancellationToken);

    /// <summary>The short per-session lock for shared-state changes. Dispose to release.</summary>
    Task<IAsyncDisposable> LockAsync(GameCode code, CancellationToken cancellationToken);

    Task<Session?> GetAsync(GameCode code, CancellationToken cancellationToken);

    Task SaveAsync(Session session, CancellationToken cancellationToken);

    Task<string?> GetPasswordHashAsync(GameCode code, CancellationToken cancellationToken);

    Task<Participant?> GetParticipantAsync(GameCode code, string participantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Participant>> GetParticipantsAsync(GameCode code, CancellationToken cancellationToken);

    Task<int> CountParticipantsAsync(GameCode code, CancellationToken cancellationToken);

    Task SaveParticipantAsync(GameCode code, Participant participant, CancellationToken cancellationToken);

    /// <summary>Reserves a name key for a participant (HSETNX); <c>false</c> if it is taken (FR-4).</summary>
    Task<bool> TryReserveNameAsync(GameCode code, string nameKey, string participantId, CancellationToken cancellationToken);

    Task ReleaseNameAsync(GameCode code, string nameKey, CancellationToken cancellationToken);

    /// <summary>Claims a card fingerprint (SADD); <c>false</c> if that card is already in play (FR-39).</summary>
    Task<bool> TryClaimCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken);

    Task ReleaseCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken);

    /// <summary>Forgets every card in play, before the host redraws all cards at round start.</summary>
    Task ReleaseAllCardsAsync(GameCode code, CancellationToken cancellationToken);

    Task SaveTokenAsync(GameCode code, string tokenHash, Caller caller, CancellationToken cancellationToken);

    Task<Caller?> FindTokenAsync(GameCode code, string tokenHash, CancellationToken cancellationToken);
}
