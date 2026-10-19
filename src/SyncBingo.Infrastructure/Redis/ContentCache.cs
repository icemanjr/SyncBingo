using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using SyncBingo.Domain.Content;

namespace SyncBingo.Infrastructure.Redis;

/// <summary>
/// Custom sets by content id (a hash of their file), so a session that uses one isn't re-read and
/// re-parsed from Redis on every command (a set can be 64 KB; a mark is a few bytes). Content-addressed,
/// so an entry never goes stale: a changed set has a new id. Holds at most <see cref="Capacity"/> sets and
/// drops the least recently used. Also remembers, per set object, its id and whether Redis already has it,
/// so saving a session doesn't send the set again.
/// </summary>
internal sealed class ContentCache
{
    public const int Capacity = 200;

    private readonly Dictionary<string, (ContentSet Set, long LastUsed)> _byId = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<ContentSet, Stored> _stored = [];
    private readonly Lock _lock = new();
    private long _clock;

    /// <summary>A set's id and file, and whether Redis has it under that id.</summary>
    public sealed class Stored(string id, string file)
    {
        public string Id { get; } = id;

        public string File { get; } = file;

        public bool InRedis { get; set; }
    }

    /// <summary>The id and file of a set, computed once per set object.</summary>
    public Stored Describe(ContentSet set) =>
        _stored.GetValue(set, static set =>
        {
            var file = ContentSetFile.Write(set);
            return new Stored(IdOf(file), file);
        });

    public bool TryGet(string id, out ContentSet? set)
    {
        lock (_lock)
        {
            if (_byId.TryGetValue(id, out var entry))
            {
                _byId[id] = (entry.Set, ++_clock);
                set = entry.Set;
                return true;
            }
        }

        set = null;
        return false;
    }

    /// <summary>Remembers a set read from (or written to) Redis under <paramref name="id"/>.</summary>
    public void Add(string id, string file, ContentSet set)
    {
        _stored.AddOrUpdate(set, new Stored(id, file) { InRedis = true });
        lock (_lock)
        {
            _byId[id] = (set, ++_clock);
            if (_byId.Count > Capacity)
            {
                _byId.Remove(_byId.MinBy(entry => entry.Value.LastUsed).Key);
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _byId.Count;
            }
        }
    }

    private static string IdOf(string file) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(file)))[..32];
}
