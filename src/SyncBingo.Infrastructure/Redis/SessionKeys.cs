using StackExchange.Redis;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Infrastructure.Redis;

/// <summary>
/// The Redis keys of one session (docs/04 §4). The code sits in braces, a Redis Cluster
/// hash tag, so every key of a session lands in the same slot and one Lua script may
/// touch them all.
/// </summary>
internal sealed class SessionKeys
{
    public SessionKeys(string prefix, GameCode code)
    {
        var root = $"{prefix}s:{{{code.Value}}}:";
        Meta = root + "meta";
        Participants = root + "participants";
        Names = root + "names";
        Tokens = root + "tokens";
        TokenOwners = root + "token-owners";
        Cards = root + "cards";
        Lock = root + "lock";
        Displays = root + "displays";
        All = [Meta, Participants, Names, Tokens, TokenOwners, Cards, Displays];
    }

    /// <summary>
    /// Hash: <c>session</c> (snapshot JSON), <c>password</c> (hash) and, with a custom set, <c>content</c> (its file)
    /// and <c>content-id</c> (a hash of the file, see <see cref="ContentCache"/>).
    /// </summary>
    public RedisKey Meta { get; }

    /// <summary>Hash: participant id → <c>{"v": version, "p": snapshot}</c>.</summary>
    public RedisKey Participants { get; }

    /// <summary>Hash: normalized name → participant id.</summary>
    public RedisKey Names { get; }

    /// <summary>Hash: token hash → caller (<c>host</c>, <c>display:{id}</c> or <c>participant:{id}</c>).</summary>
    public RedisKey Tokens { get; }

    /// <summary>Hash: participant id → their token hash, so removing a player can revoke it.</summary>
    public RedisKey TokenOwners { get; }

    /// <summary>Set: fingerprints of every card in play (FR-39).</summary>
    public RedisKey Cards { get; }

    /// <summary>Hash: display id → display JSON (FR-28); its token's hash is in <see cref="TokenOwners"/> under <c>display:{id}</c>.</summary>
    public RedisKey Displays { get; }

    /// <summary>String: the per-session lock, holding its owner's random value.</summary>
    public RedisKey Lock { get; }

    /// <summary>Every data key, in the order <see cref="Scripts"/> expects them as <c>KEYS</c>.</summary>
    public RedisKey[] All { get; }
}
