namespace SyncBingo.Infrastructure.Redis;

/// <summary>
/// Lua scripts, each atomic in Redis. Every one receives all of a session's keys
/// (<see cref="SessionKeys.All"/>: 1 meta, 2 participants, 3 names, 4 tokens, 5 token-owners,
/// 6 cards, 7 displays) and the idle expiry in ms as <c>ARGV[1]</c>, and refreshes every key's expiry
/// after a write (then returns 1), so a session's keys always live and expire together.
/// </summary>
internal static class Scripts
{
    private const string Touch = "\nfor i = 1, #KEYS do redis.call('PEXPIRE', KEYS[i], ARGV[1]) end\nreturn 1";

    /// <summary>ARGV[2] session JSON, ARGV[3] password hash. 1 = created, 0 = the code is taken.</summary>
    public const string Create = """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('HSET', KEYS[1], 'session', ARGV[2], 'password', ARGV[3])
        """ + Touch;

    /// <summary>
    /// ARGV[2] session JSON; ARGV[3] the custom set's content id, or '' when the session uses none;
    /// ARGV[4] the set's file, or '' when Redis already has it under that id. Only an existing session is
    /// saved; one that expired stays gone. The set lives in the meta hash with the session, so they're
    /// written together and expire together. -1 = the set is needed but wasn't sent.
    /// </summary>
    public const string Save = """
        if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
        if ARGV[3] == '' then
          redis.call('HDEL', KEYS[1], 'content', 'content-id')
        elseif redis.call('HGET', KEYS[1], 'content-id') ~= ARGV[3] then
          if ARGV[4] == '' then return -1 end
          redis.call('HSET', KEYS[1], 'content', ARGV[4], 'content-id', ARGV[3])
        end
        redis.call('HSET', KEYS[1], 'session', ARGV[2])
        """ + Touch;

    /// <summary>ARGV[2] participant id, ARGV[3] envelope JSON at version 1. 1 = added, 0 = the id exists.</summary>
    public const string AddParticipant = """
        if redis.call('HSETNX', KEYS[2], ARGV[2], ARGV[3]) == 0 then return 0 end
        """ + Touch;

    /// <summary>
    /// Compare-and-set (docs/02 §6). ARGV[2] participant id, ARGV[3] expected version, ARGV[4] the
    /// new envelope JSON (at the next version). 1 = saved, 0 = changed since loaded, or removed.
    /// </summary>
    public const string UpdateParticipant = """
        local current = redis.call('HGET', KEYS[2], ARGV[2])
        if not current then return 0 end
        if tostring(cjson.decode(current).v) ~= ARGV[3] then return 0 end
        redis.call('HSET', KEYS[2], ARGV[2], ARGV[4])
        """ + Touch;

    /// <summary>ARGV[2] participant id. Deletes the participant and revokes their token.</summary>
    public const string DeleteParticipant = """
        redis.call('HDEL', KEYS[2], ARGV[2])
        local token = redis.call('HGET', KEYS[5], ARGV[2])
        if token then redis.call('HDEL', KEYS[4], token) end
        redis.call('HDEL', KEYS[5], ARGV[2])
        return 1
        """;

    /// <summary>ARGV[2] name key, ARGV[3] participant id. 1 = reserved, 0 = taken (FR-4).</summary>
    public const string ReserveName = """
        if redis.call('HSETNX', KEYS[3], ARGV[2], ARGV[3]) == 0 then return 0 end
        """ + Touch;

    /// <summary>ARGV[2] fingerprint. 1 = claimed, 0 = already in play (FR-39).</summary>
    public const string ClaimCard = """
        if redis.call('SADD', KEYS[6], ARGV[2]) == 0 then return 0 end
        """ + Touch;

    /// <summary>ARGV[2] token hash, ARGV[3] caller, ARGV[4] participant id or empty.</summary>
    public const string SaveToken = """
        redis.call('HSET', KEYS[4], ARGV[2], ARGV[3])
        if ARGV[4] ~= '' then redis.call('HSET', KEYS[5], ARGV[4], ARGV[2]) end
        """ + Touch;

    /// <summary>
    /// ARGV[2] display id, ARGV[3] display JSON, ARGV[4] token hash, ARGV[5] the most displays allowed.
    /// 1 = added, 0 = the session already has that many.
    /// </summary>
    public const string AddDisplay = """
        if redis.call('HLEN', KEYS[7]) >= tonumber(ARGV[5]) then return 0 end
        redis.call('HSET', KEYS[7], ARGV[2], ARGV[3])
        redis.call('HSET', KEYS[4], ARGV[4], 'display:' .. ARGV[2])
        redis.call('HSET', KEYS[5], 'display:' .. ARGV[2], ARGV[4])
        """ + Touch;

    /// <summary>ARGV[2] display id. Deletes the display and revokes its token. 1 = removed, 0 = no such display.</summary>
    public const string RemoveDisplay = """
        if redis.call('HDEL', KEYS[7], ARGV[2]) == 0 then return 0 end
        local token = redis.call('HGET', KEYS[5], 'display:' .. ARGV[2])
        if token then redis.call('HDEL', KEYS[4], token) end
        redis.call('HDEL', KEYS[5], 'display:' .. ARGV[2])
        return 1
        """;

    /// <summary>Releases a lock only if it is still ours. KEYS[1] the lock, ARGV[1] our value.</summary>
    public const string ReleaseLock = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) end
        return 0
        """;
}
