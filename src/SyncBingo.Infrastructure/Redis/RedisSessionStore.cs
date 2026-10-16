using StackExchange.Redis;
using SyncBingo.Application;
using SyncBingo.Application.Ports;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Infrastructure.Redis;

/// <summary>
/// <see cref="ISessionStore"/> on Redis (ADR 0003, docs/04 §4). Every check-and-write is a
/// Lua script, so it is atomic; every write refreshes the idle expiry of all the session's
/// keys; and a session past its maximum lifetime reads as gone, however recently it was used.
/// </summary>
public sealed class RedisSessionStore(IConnectionMultiplexer redis, RedisStoreOptions options, TimeProvider time) : ISessionStore
{
    private IDatabase Db => redis.GetDatabase();

    public async Task<bool> TryCreateAsync(Session session, string passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        return await RunAsync(Scripts.Create, session.Code, StoreJson.Write(session), passwordHash).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the session lock: <c>SET NX</c> with a random value and a lease, retried until
    /// <see cref="RedisStoreOptions.LockWait"/> runs out. The lease frees a lock whose holder died.
    /// </summary>
    public async Task<IAsyncDisposable> LockAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var key = Keys(code).Lock;
        var owner = Guid.NewGuid().ToString("N");
        var giveUpAt = time.GetUtcNow() + options.LockWait;
        var delay = TimeSpan.FromMilliseconds(5);
        while (!await Db.StringSetAsync(key, owner, options.LockLease, When.NotExists).ConfigureAwait(false))
        {
            if (time.GetUtcNow() >= giveUpAt)
            {
                throw new TimeoutException($"Session {code.Value} stayed locked for {options.LockWait.TotalSeconds:0} s.");
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 100));
        }

        return new SessionLock(Db, key, owner);
    }

    public async Task<Session?> GetAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var json = await Db.HashGetAsync(Keys(code).Meta, "session").ConfigureAwait(false);
        if (json.IsNull)
        {
            return null;
        }

        var session = StoreJson.ReadSession(json.ToString());
        if (time.GetUtcNow() < session.CreatedAt + options.MaxLifetime)
        {
            return session;
        }

        await Db.KeyDeleteAsync(Keys(code).All).ConfigureAwait(false);
        return null;
    }

    public Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // An ended game keeps only a few minutes; the domain refuses every command on it, so nothing extends that.
        var expiry = session.State == SessionState.Ended ? options.EndedExpiry : options.IdleExpiry;
        return RunWithExpiryAsync(Scripts.Save, session.Code, expiry, StoreJson.Write(session));
    }

    public async Task<string?> GetPasswordHashAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var hash = await Db.HashGetAsync(Keys(code).Meta, "password").ConfigureAwait(false);
        return hash.IsNull ? null : hash.ToString();
    }

    public async Task<StoredParticipant?> GetParticipantAsync(GameCode code, string participantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var json = await Db.HashGetAsync(Keys(code).Participants, participantId).ConfigureAwait(false);
        return json.IsNull ? null : ToStored(json.ToString());
    }

    public async Task<IReadOnlyList<StoredParticipant>> GetParticipantsAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var entries = await Db.HashGetAllAsync(Keys(code).Participants).ConfigureAwait(false);
        return [.. entries.Select(entry => ToStored(entry.Value.ToString())).OrderBy(stored => stored.Participant.JoinedAt)];
    }

    public async Task<int> CountParticipantsAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return (int)await Db.HashLengthAsync(Keys(code).Participants).ConfigureAwait(false);
    }

    public Task<bool> TryAddParticipantAsync(GameCode code, Participant participant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participant);

        return RunAsync(Scripts.AddParticipant, code, participant.Id, StoreJson.Write(participant, version: 1));
    }

    public Task<bool> TryUpdateParticipantAsync(
        GameCode code, Participant participant, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participant);

        return RunAsync(
            Scripts.UpdateParticipant, code, participant.Id, expectedVersion, StoreJson.Write(participant, expectedVersion + 1));
    }

    public Task DeleteParticipantAsync(GameCode code, string participantId, CancellationToken cancellationToken) =>
        RunAsync(Scripts.DeleteParticipant, code, participantId);

    public Task<bool> TryReserveNameAsync(GameCode code, string nameKey, string participantId, CancellationToken cancellationToken) =>
        RunAsync(Scripts.ReserveName, code, nameKey, participantId);

    public Task ReleaseNameAsync(GameCode code, string nameKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Db.HashDeleteAsync(Keys(code).Names, nameKey);
    }

    public Task<bool> TryClaimCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken) =>
        RunAsync(Scripts.ClaimCard, code, fingerprint);

    public Task ReleaseCardAsync(GameCode code, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Db.SetRemoveAsync(Keys(code).Cards, fingerprint);
    }

    public Task ReleaseAllCardsAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Db.KeyDeleteAsync(Keys(code).Cards);
    }

    public Task SaveTokenAsync(GameCode code, string tokenHash, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return RunAsync(Scripts.SaveToken, code, tokenHash, StoreJson.Write(caller), caller.ParticipantId ?? string.Empty);
    }

    public async Task<Caller?> FindTokenAsync(GameCode code, string tokenHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var caller = await Db.HashGetAsync(Keys(code).Tokens, tokenHash).ConfigureAwait(false);
        return caller.IsNull ? null : StoreJson.ReadCaller(caller.ToString());
    }

    public Task<bool> TryAddDisplayAsync(GameCode code, DisplayRecord display, string tokenHash, int maxDisplays, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);

        return RunAsync(Scripts.AddDisplay, code, display.DisplayId, StoreJson.Write(display), tokenHash, maxDisplays);
    }

    public async Task<IReadOnlyList<DisplayRecord>> GetDisplaysAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var entries = await Db.HashGetAllAsync(Keys(code).Displays).ConfigureAwait(false);
        return [.. entries.Select(entry => StoreJson.ReadDisplay(entry.Value.ToString())).OrderBy(display => display.CreatedAt)];
    }

    public Task<bool> RemoveDisplayAsync(GameCode code, string displayId, CancellationToken cancellationToken) =>
        RunAsync(Scripts.RemoveDisplay, code, displayId);

    private static StoredParticipant ToStored(string json)
    {
        var (participant, version) = StoreJson.ReadParticipant(json);
        return new StoredParticipant(participant, version);
    }

    private SessionKeys Keys(GameCode code) => new(options.KeyPrefix, code);

    /// <summary>Runs a script over all the session's keys with the idle expiry first; true when it returned 1.</summary>
    private Task<bool> RunAsync(string script, GameCode code, params RedisValue[] args) =>
        RunWithExpiryAsync(script, code, options.IdleExpiry, args);

    private async Task<bool> RunWithExpiryAsync(string script, GameCode code, TimeSpan expiry, params RedisValue[] args)
    {
        ArgumentNullException.ThrowIfNull(code);

        RedisValue[] values = [(long)expiry.TotalMilliseconds, .. args];
        var result = await Db.ScriptEvaluateAsync(script, Keys(code).All, values).ConfigureAwait(false);
        return (long)result == 1;
    }

    private sealed class SessionLock(IDatabase db, RedisKey key, string owner) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await db.ScriptEvaluateAsync(Scripts.ReleaseLock, [key], [owner]).ConfigureAwait(false);
    }
}
