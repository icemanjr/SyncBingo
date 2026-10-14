using System.Text.Json;
using StackExchange.Redis;
using SyncBingo.Application.Pairing;
using SyncBingo.Domain.Pairing;
using SyncBingo.Infrastructure.Redis;

namespace SyncBingo.Infrastructure.Pairing;

/// <summary>
/// Pairings in Redis (ADR 0010), each key expiring by itself: <c>pairing-code:{code}</c> → pairing id
/// (claimed with <c>SET NX</c>, used with <c>GETDEL</c>, so once only), <c>pairing:{id}</c> while the TV
/// waits, and <c>pairing-result:{id}</c> for a TV that connects a moment after the host approved, taken
/// with <c>GETDEL</c>. The result holds the display token in clear for at most two minutes, so a TV that
/// briefly lost its connection is not stranded.
/// </summary>
public sealed class RedisPairingStore(IConnectionMultiplexer redis, RedisStoreOptions options) : IPairingStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();

    public async Task<bool> TryCreateAsync(string pairingId, PairingCode code, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!await Db.StringSetAsync(CodeKey(code), pairingId, lifetime, When.NotExists).ConfigureAwait(false))
        {
            return false;
        }

        await Db.StringSetAsync(PairingKey(pairingId), "waiting", lifetime).ConfigureAwait(false);
        return true;
    }

    public async Task<string?> TryClaimAsync(PairingCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var pairingId = await Db.StringGetDeleteAsync(CodeKey(code)).ConfigureAwait(false);
        return pairingId.IsNull ? null : pairingId.ToString();
    }

    public Task<bool> ExistsAsync(string pairingId, CancellationToken cancellationToken) =>
        Db.KeyExistsAsync(PairingKey(pairingId));

    public Task SaveResultAsync(string pairingId, PairingResult result, TimeSpan keepFor, CancellationToken cancellationToken) =>
        Db.StringSetAsync(ResultKey(pairingId), JsonSerializer.Serialize(result, Json), keepFor);

    public async Task<PairingResult?> TakeResultAsync(string pairingId, CancellationToken cancellationToken)
    {
        var json = await Db.StringGetDeleteAsync(ResultKey(pairingId)).ConfigureAwait(false);
        return json.IsNull ? null : JsonSerializer.Deserialize<PairingResult>(json.ToString(), Json);
    }

    private RedisKey CodeKey(PairingCode code) => $"{options.KeyPrefix}pairing-code:{code.Value}";

    private RedisKey PairingKey(string pairingId) => $"{options.KeyPrefix}pairing:{pairingId}";

    private RedisKey ResultKey(string pairingId) => $"{options.KeyPrefix}pairing-result:{pairingId}";
}
