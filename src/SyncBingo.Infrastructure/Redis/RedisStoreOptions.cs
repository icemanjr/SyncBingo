namespace SyncBingo.Infrastructure.Redis;

/// <summary>Settings for <see cref="RedisSessionStore"/>; defaults follow assumption A7 and docs/04 §4.</summary>
public sealed record RedisStoreOptions
{
    /// <summary>Prefix for every key, so the app can share a Redis with other tenants or test runs.</summary>
    public string KeyPrefix { get; init; } = "bingo:";

    /// <summary>A session with no activity for this long expires (every write refreshes it).</summary>
    public TimeSpan IdleExpiry { get; init; } = TimeSpan.FromHours(4);

    /// <summary>A session is gone this long after it was created, however busy it is.</summary>
    public TimeSpan MaxLifetime { get; init; } = TimeSpan.FromHours(12);

    /// <summary>How long a session lock lasts if its holder dies without releasing it.</summary>
    public TimeSpan LockLease { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a command waits for the session lock before giving up.</summary>
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(5);
}
