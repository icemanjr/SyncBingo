using System.Collections.Concurrent;
using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>
/// An in-memory <see cref="ISessionStore"/> with the same atomic rules the Redis store
/// must follow: name and card claims succeed only for the first caller, the lock really
/// is exclusive per session, and participants are stored as snapshots with a version, so
/// every load is a fresh copy and a stale save is refused.
/// </summary>
internal sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _passwords = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly Dictionary<(string Code, string Id), (ParticipantSnapshot Snapshot, long Version)> _participants = [];
    private readonly Lock _participantsGate = new();
    private readonly ConcurrentDictionary<(string Code, string Key), string> _names = new();
    private readonly ConcurrentDictionary<(string Code, string Fingerprint), byte> _cards = new();
    private readonly ConcurrentDictionary<(string Code, string Hash), Caller> _tokens = new();

    /// <summary>Codes <see cref="TryCreateAsync"/> pretends are taken, to test code clashes.</summary>
    public HashSet<string> TakenCodes { get; } = [];

    public int Saves { get; private set; }

    /// <summary>Card claims attempted, successful or not.</summary>
    public int CardClaims { get; private set; }

    /// <summary>
    /// Runs once, just before the next participant update is checked: a test uses it to
    /// make another write land between a command's load and its save.
    /// </summary>
    public Func<Task>? BeforeNextParticipantUpdate { get; set; }

    public IReadOnlyCollection<string> Tokens(GameCode code) =>
        [.. _tokens.Where(t => t.Key.Code == code.Value).Select(t => t.Value.ParticipantId ?? t.Value.Role.ToString())];

    public IReadOnlyCollection<string> CardsInPlay(GameCode code) =>
        [.. _cards.Keys.Where(k => k.Code == code.Value).Select(k => k.Fingerprint)];

    public IReadOnlyCollection<string> ReservedNames(GameCode code) =>
        [.. _names.Keys.Where(k => k.Code == code.Value).Select(k => k.Key)];

    public Task<bool> TryCreateAsync(Session session, string passwordHash, CancellationToken cancellationToken)
    {
        if (TakenCodes.Contains(session.Code.Value) || !_sessions.TryAdd(session.Code.Value, session))
        {
            return Task.FromResult(false);
        }

        _passwords[session.Code.Value] = passwordHash;
        return Task.FromResult(true);
    }

    public async Task<IAsyncDisposable> LockAsync(GameCode code, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(code.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    public Task<Session?> GetAsync(GameCode code, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.GetValueOrDefault(code.Value));

    public Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        Saves++;
        _sessions[session.Code.Value] = session;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(GameCode code, CancellationToken cancellationToken) =>
        Task.FromResult(_passwords.GetValueOrDefault(code.Value));

    public Task<StoredParticipant?> GetParticipantAsync(GameCode code, string participantId, CancellationToken cancellationToken)
    {
        lock (_participantsGate)
        {
            return Task.FromResult(_participants.TryGetValue((code.Value, participantId), out var stored) ? Load(stored) : null);
        }
    }

    public Task<IReadOnlyList<StoredParticipant>> GetParticipantsAsync(GameCode code, CancellationToken cancellationToken)
    {
        lock (_participantsGate)
        {
            return Task.FromResult<IReadOnlyList<StoredParticipant>>(
                [.. _participants.Where(p => p.Key.Code == code.Value).Select(p => Load(p.Value)).OrderBy(p => p.Participant.JoinedAt)]);
        }
    }

    public Task<int> CountParticipantsAsync(GameCode code, CancellationToken cancellationToken)
    {
        lock (_participantsGate)
        {
            return Task.FromResult(_participants.Keys.Count(k => k.Code == code.Value));
        }
    }

    public Task<bool> TryAddParticipantAsync(GameCode code, Participant participant, CancellationToken cancellationToken)
    {
        lock (_participantsGate)
        {
            return Task.FromResult(_participants.TryAdd((code.Value, participant.Id), (participant.ToSnapshot(), 1)));
        }
    }

    public async Task<bool> TryUpdateParticipantAsync(
        GameCode code, Participant participant, long expectedVersion, CancellationToken cancellationToken)
    {
        if (BeforeNextParticipantUpdate is { } interfere)
        {
            BeforeNextParticipantUpdate = null;
            await interfere();
        }

        lock (_participantsGate)
        {
            var key = (code.Value, participant.Id);
            if (!_participants.TryGetValue(key, out var stored) || stored.Version != expectedVersion)
            {
                return false;
            }

            _participants[key] = (participant.ToSnapshot(), expectedVersion + 1);
            return true;
        }
    }

    public Task DeleteParticipantAsync(GameCode code, string participantId, CancellationToken cancellationToken)
    {
        lock (_participantsGate)
        {
            _participants.Remove((code.Value, participantId));
        }

        foreach (var token in _tokens.Where(t => t.Key.Code == code.Value && t.Value.ParticipantId == participantId))
        {
            _tokens.TryRemove(token.Key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryReserveNameAsync(GameCode code, string nameKey, string participantId, CancellationToken cancellationToken) =>
        Task.FromResult(_names.TryAdd((code.Value, nameKey), participantId));

    public Task ReleaseNameAsync(GameCode code, string nameKey, CancellationToken cancellationToken)
    {
        _names.TryRemove((code.Value, nameKey), out _);
        return Task.CompletedTask;
    }

    public Task<bool> TryClaimCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken)
    {
        CardClaims++;
        return Task.FromResult(_cards.TryAdd((code.Value, fingerprint), 0));
    }

    public Task ReleaseCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken)
    {
        _cards.TryRemove((code.Value, fingerprint), out _);
        return Task.CompletedTask;
    }

    public Task ReleaseAllCardsAsync(GameCode code, CancellationToken cancellationToken)
    {
        foreach (var key in _cards.Keys.Where(k => k.Code == code.Value))
        {
            _cards.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task SaveTokenAsync(GameCode code, string tokenHash, Caller caller, CancellationToken cancellationToken)
    {
        _tokens[(code.Value, tokenHash)] = caller;
        return Task.CompletedTask;
    }

    public Task<Caller?> FindTokenAsync(GameCode code, string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.GetValueOrDefault((code.Value, tokenHash)));

    private static StoredParticipant Load((ParticipantSnapshot Snapshot, long Version) stored) =>
        new(Participant.Restore(stored.Snapshot), stored.Version);

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Records what would be pushed to clients, in order.</summary>
internal sealed class RecordingPublisher : IEventPublisher
{
    public List<object> Sent { get; } = [];

    public IEnumerable<SessionEvent> Events => Sent.OfType<SessionEvent>();

    public IEnumerable<(Recipient Recipient, Notification Notification)> Notifications =>
        Sent.OfType<(Recipient, Notification)>();

    public Task PublishAsync(GameCode code, IReadOnlyList<SessionEvent> events, CancellationToken cancellationToken)
    {
        Sent.AddRange(events);
        return Task.CompletedTask;
    }

    public Task NotifyAsync(GameCode code, Recipient recipient, Notification notification, CancellationToken cancellationToken)
    {
        Sent.Add((recipient, notification));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingScheduler : ICallScheduler
{
    public Dictionary<string, DateTimeOffset> Due { get; } = [];

    public Task ScheduleAsync(GameCode code, DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        Due[code.Value] = dueAt;
        return Task.CompletedTask;
    }

    public Task CancelAsync(GameCode code, CancellationToken cancellationToken)
    {
        Due.Remove(code.Value);
        return Task.CompletedTask;
    }
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 13, 19, 0, 0, TimeSpan.Zero);
}

/// <summary>Readable, not secure: "hashed:password".</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string hash) => hash == Hash(password);
}

/// <summary>Sequential tokens: token-1, token-2, …</summary>
internal sealed class FakeTokenService : ITokenService
{
    private int _next;

    public IssuedToken Issue()
    {
        var token = $"token-{++_next}";
        return new IssuedToken(token, Hash(token));
    }

    public string Hash(string token) => "sha:" + token;
}

internal sealed class SeededRandom(int seed) : IRandomSource
{
#pragma warning disable CA5394 // Predictable on purpose: tests must be repeatable.
    private readonly Random _random = new(seed);

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
#pragma warning restore CA5394
}
