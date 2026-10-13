using StackExchange.Redis;
using SyncBingo.Application.Ports;
using SyncBingo.Domain.Sessions;
using SyncBingo.Infrastructure.Redis;

namespace SyncBingo.Infrastructure.Scheduling;

/// <summary>A session the scheduler claimed, and the lease score that marks the claim as ours.</summary>
public sealed record ScheduleClaim(GameCode Code, long LeaseUntilMs)
{
    /// <summary>Only the first group of the code, for logs: enough to correlate, not enough to join.</summary>
    public string LogCode { get; } = Code.Value[..4];
}

/// <summary>
/// The auto-call schedule (docs/02 §5) as one Redis sorted set: member = game code, score =
/// when its next tick is due (Unix ms). There is no leader: any instance may claim due
/// sessions, and the claim is atomic, so each tick runs on exactly one instance.
/// </summary>
public sealed class RedisCallScheduler(IConnectionMultiplexer redis, RedisStoreOptions options, TimeProvider time) : ICallScheduler
{
    /// <summary>
    /// Returns up to ARGV[3] sessions due by ARGV[1] and moves each one's score to the lease end,
    /// ARGV[2], so no other instance claims it again until the lease runs out.
    /// </summary>
    private const string ClaimScript = """
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, ARGV[3])
        for _, code in ipairs(due) do redis.call('ZADD', KEYS[1], 'XX', ARGV[2], code) end
        return due
        """;

    /// <summary>Removes ARGV[1] only if its score is still our lease ARGV[2]: nobody re-scheduled it meanwhile.</summary>
    private const string DropScript = """
        if redis.call('ZSCORE', KEYS[1], ARGV[1]) == ARGV[2] then return redis.call('ZREM', KEYS[1], ARGV[1]) end
        return 0
        """;

    private RedisKey Key => options.KeyPrefix + "schedule";

    private IDatabase Db => redis.GetDatabase();

    public Task ScheduleAsync(GameCode code, DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Db.SortedSetAddAsync(Key, code.Value, dueAt.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(GameCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Db.SortedSetRemoveAsync(Key, code.Value);
    }

    /// <summary>Claims up to <paramref name="max"/> sessions that are due now, each for <paramref name="lease"/>.</summary>
    public async Task<IReadOnlyList<ScheduleClaim>> ClaimDueAsync(TimeSpan lease, int max, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var leaseUntil = (now + lease).ToUnixTimeMilliseconds();
        var due = (RedisResult[]?)await Db
            .ScriptEvaluateAsync(ClaimScript, [Key], [now.ToUnixTimeMilliseconds(), leaseUntil, max])
            .ConfigureAwait(false) ?? [];

        // A code that no longer parses can never be ticked; leave it to expire with the lease rather than fail every claim.
        return [.. due.Select(code => GameCode.TryParse(code.ToString(), out var parsed) ? new ScheduleClaim(parsed, leaseUntil) : null)
            .OfType<ScheduleClaim>()];
    }

    /// <summary>
    /// Takes a claimed session off the schedule, unless it was re-scheduled since the claim
    /// (a host pressing <i>Auto mode</i> right after a failed tick must not lose its schedule).
    /// </summary>
    public async Task<bool> DropIfStillClaimedAsync(ScheduleClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        var dropped = await Db.ScriptEvaluateAsync(DropScript, [Key], [claim.Code.Value, claim.LeaseUntilMs]).ConfigureAwait(false);
        return (long)dropped == 1;
    }

    /// <summary>When the session is next due, or <c>null</c> if it is not scheduled.</summary>
    public async Task<DateTimeOffset?> DueAtAsync(GameCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var score = await Db.SortedSetScoreAsync(Key, code.Value).ConfigureAwait(false);
        return score is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms) : null;
    }
}
