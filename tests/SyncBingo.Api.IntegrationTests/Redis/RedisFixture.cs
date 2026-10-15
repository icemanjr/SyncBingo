using StackExchange.Redis;
using SyncBingo.Api.IntegrationTests.Redis;
using SyncBingo.Domain;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;
using SyncBingo.Infrastructure.Redis;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(RedisFixture))]

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>
/// One real Valkey (the same image as Compose; ADR 0011) for the whole test run. Valkey speaks the Redis
/// protocol, so the Redis Testcontainers module and every store, lock, scheduler and backplane test apply as is.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("valkey/valkey:8.1-alpine").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    public IDatabase Db => Connection.GetDatabase();

    public RedisSessionStore Store(TimeProvider time, RedisStoreOptions? options = null) =>
        new(Connection, options ?? new RedisStoreOptions(), time);

    /// <summary>A new session with a random code, so tests never share keys.</summary>
    public static Session NewSession(DateTimeOffset now) =>
        Session.Create(GameCode.Generate(new CryptoRandomSource()), StandardVariants.Classic75, SessionSettings.Default, "normal", now).Value;
}

/// <summary>A clock tests move by hand.</summary>
public sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 13, 19, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
