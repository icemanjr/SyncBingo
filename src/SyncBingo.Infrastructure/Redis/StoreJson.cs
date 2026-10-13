using System.Text.Json;
using System.Text.Json.Serialization;
using SyncBingo.Application;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Infrastructure.Redis;

/// <summary>How sessions, participants and callers are written to Redis.</summary>
internal static class StoreJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(Session session) => JsonSerializer.Serialize(session.ToSnapshot(), Options);

    public static Session ReadSession(string json) =>
        Session.Restore(JsonSerializer.Deserialize<SessionSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Stored session is empty."));

    /// <summary>A participant with its version, as one hash field so both are read together.</summary>
    public static string Write(Participant participant, long version) =>
        JsonSerializer.Serialize(new ParticipantEnvelope(version, participant.ToSnapshot()), Options);

    public static (Participant Participant, long Version) ReadParticipant(string json)
    {
        var envelope = JsonSerializer.Deserialize<ParticipantEnvelope>(json, Options)
            ?? throw new InvalidOperationException("Stored participant is empty.");
        return (Participant.Restore(envelope.P), envelope.V);
    }

    public static string Write(Caller caller) => caller.Role switch
    {
        CallerRole.Participant => "participant:" + caller.ParticipantId,
        var role => role.ToString().ToLowerInvariant(),
    };

    public static Caller ReadCaller(string value) => value switch
    {
        "host" => Caller.Host,
        "system" => Caller.System,
        "display" => new Caller(CallerRole.Display),
        _ when value.StartsWith("participant:", StringComparison.Ordinal) => Caller.Participant(value["participant:".Length..]),
        _ => throw new InvalidOperationException($"Stored token has an unknown caller '{value}'."),
    };

    /// <summary>Short names: the Lua compare-and-set reads <c>v</c>.</summary>
    private sealed record ParticipantEnvelope(long V, ParticipantSnapshot P);
}
