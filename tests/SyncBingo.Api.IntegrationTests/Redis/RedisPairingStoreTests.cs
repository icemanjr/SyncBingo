using SyncBingo.Application.Pairing;
using SyncBingo.Domain;
using SyncBingo.Domain.Pairing;
using SyncBingo.Infrastructure.Pairing;
using SyncBingo.Infrastructure.Redis;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>Pairings in Redis: codes are unique and used once, results are taken once, everything expires.</summary>
public sealed class RedisPairingStoreTests(RedisFixture redis)
{
    private readonly RedisPairingStore _store = new(redis.Connection, new RedisStoreOptions { KeyPrefix = $"t{Guid.NewGuid():N}:" });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_code_is_unique_while_it_lives_and_claimed_once()
    {
        var code = PairingCode.Generate(new CryptoRandomSource());

        (await _store.TryCreateAsync("pairing-1", code, TimeSpan.FromMinutes(10), Ct)).ShouldBeTrue();
        (await _store.TryCreateAsync("pairing-2", code, TimeSpan.FromMinutes(10), Ct)).ShouldBeFalse();

        var claims = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _store.TryClaimAsync(code, Ct)));
        claims.Count(id => id == "pairing-1").ShouldBe(1, "a code is used once, however many hosts race");
        (await _store.ExistsAsync("pairing-1", Ct)).ShouldBeTrue("the TV keeps waiting until it collects");
    }

    [Fact]
    public async Task A_result_is_taken_once()
    {
        await _store.SaveResultAsync("pairing-1", new PairingResult("code", "token"), TimeSpan.FromMinutes(2), Ct);

        (await _store.TakeResultAsync("pairing-1", Ct)).ShouldBe(new PairingResult("code", "token"));
        (await _store.TakeResultAsync("pairing-1", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task A_pairing_expires_by_itself()
    {
        var code = PairingCode.Generate(new CryptoRandomSource());
        await _store.TryCreateAsync("pairing-1", code, TimeSpan.FromMilliseconds(200), Ct);

        await Task.Delay(400, Ct);

        (await _store.ExistsAsync("pairing-1", Ct)).ShouldBeFalse();
        (await _store.TryClaimAsync(code, Ct)).ShouldBeNull();
    }
}
