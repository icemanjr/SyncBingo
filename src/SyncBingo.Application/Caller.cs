using SyncBingo.Domain;

namespace SyncBingo.Application;

public enum CallerRole
{
    Host,
    Participant,

    /// <summary>Read-only display view (FR-28, ADR 0010).</summary>
    Display,

    /// <summary>The auto-call scheduler inside the API, not a person.</summary>
    System,
}

/// <summary>Who sent a command, as resolved from their session token by the API.</summary>
public sealed record Caller(CallerRole Role, string? ParticipantId = null, string? DisplayId = null)
{
    public static Caller Host { get; } = new(CallerRole.Host);

    public static Caller System { get; } = new(CallerRole.System);

    public static Caller Participant(string participantId) => new(CallerRole.Participant, participantId);

    /// <summary>A read-only display view (FR-28): a display link or a paired TV.</summary>
    public static Caller Display(string displayId) => new(CallerRole.Display, DisplayId: displayId);

    /// <summary>Host commands may come from the host, or from the system acting for the host (the auto-call scheduler).</summary>
    internal DomainError? RequireHost() =>
        Role is CallerRole.Host or CallerRole.System ? null : new DomainError(DomainErrorCode.Forbidden, "Only the host can do that.");

    /// <summary>
    /// Player commands come only from a participant and act on that participant's own cards:
    /// whose cards they are comes from the token, never from the request. The host does not play.
    /// </summary>
    internal DomainError? RequireParticipant() =>
        Role == CallerRole.Participant && ParticipantId is not null
            ? null
            : new DomainError(DomainErrorCode.Forbidden, "Only players can do that.");
}
