namespace SyncBingo.Domain.Sessions;

public enum SessionState
{
    /// <summary>The lobby and the gap between rounds: joining and redraws happen here.</summary>
    BetweenRounds,

    /// <summary>
    /// The 10-second countdown after "Start Round N" (FR-45). Joining and redraws are
    /// already closed; the host can cancel back to <see cref="BetweenRounds"/>.
    /// </summary>
    Starting,

    /// <summary>In a round with no timer: the host draws with "Grab ball". Every round starts here.</summary>
    Manual,

    /// <summary>In a round with the timer drawing every interval. "Pause" returns to <see cref="Manual"/>.</summary>
    Auto,

    /// <summary>Terminal: the host ended the session (or it expired).</summary>
    Ended,
}

public enum DrawSource
{
    /// <summary>The host pressed "Grab ball" (manual mode).</summary>
    Manual,

    /// <summary>The auto-call scheduler.</summary>
    Auto,
}

/// <summary>One called ball. <see cref="Sequence"/> counts calls within the round, from 1.</summary>
/// <param name="Letter">The column label, e.g. "N"; a group's name with grouped custom content; empty when dealt free-form.</param>
/// <param name="Call">With custom content (ADR 0009): what is called, e.g. "2 + 5"; <c>null</c> for numbers.</param>
/// <param name="Square">With custom content: what players mark, e.g. "7"; <c>null</c> for numbers.</param>
public sealed record BallCall(
    int Sequence, int Number, string Letter, DateTimeOffset CalledAt, DrawSource Source, string? Call = null, string? Square = null);

/// <summary>Who an event is for (docs/05 §4). Private messages to one player are notifications, not events.</summary>
[Flags]
public enum Audience
{
    None = 0,
    Players = 1,
    Host = 2,
    Display = 4,
    Everyone = Players | Host | Display,
}

/// <summary>
/// An event's position in each stream it belongs to (ADR 0002, amended): every audience counts
/// only the events it receives, so a gap in a client's numbers always means a missed event.
/// 0 means "not in that stream".
/// </summary>
public readonly record struct StreamSeqs(long Players, long Host, long Display)
{
    public long For(Audience stream) => stream switch
    {
        Audience.Players => Players,
        Audience.Host => Host,
        Audience.Display => Display,
        _ => throw new ArgumentOutOfRangeException(nameof(stream), stream, "Pick one stream."),
    };
}

/// <summary>
/// Something that happened to a session, published to clients over SignalR.
/// <see cref="Seq"/> increases by one per event across the whole session; <see cref="Streams"/>
/// numbers it within each <see cref="Audience"/> it is sent to (ADR 0002).
/// </summary>
public abstract record SessionEvent
{
    public long Seq { get; init; }

    public Audience Audience { get; init; }

    public StreamSeqs Streams { get; init; }
}

/// <summary>
/// "Start Round N": the countdown runs until <see cref="EndsAt"/>; clients count down locally.
/// Every screen also shows the round's pattern, <see cref="WinConditionId"/>, until the first ball
/// (FR-47); it can't change during the countdown.
/// </summary>
public sealed record RoundCountdownStarted(int Round, DateTimeOffset EndsAt, bool StartInAuto, string WinConditionId) : SessionEvent;

public sealed record RoundCountdownCancelled(int Round) : SessionEvent;

/// <summary>The countdown reached zero. If it was started in auto mode, <see cref="AutoCallingStarted"/> and the first <see cref="BallCalled"/> follow.</summary>
public sealed record RoundStarted(int Round) : SessionEvent;

/// <summary>"Clear cards": marks and calls are cleared (cards replaced if <see cref="Redraw"/>), back in the lobby.</summary>
public sealed record CardsCleared(bool Redraw) : SessionEvent;

/// <summary>
/// The host started using a custom set, or went back to numbers (<paramref name="Name"/> <c>null</c>), in the
/// lobby (ADR 0009). Every card is redealt and the call board rebuilt; the caller deals the new cards.
/// </summary>
public sealed record ContentChanged(string? Name, int Items, bool Grouped) : SessionEvent;

public sealed record RoundEnded(int Round) : SessionEvent;

/// <summary>The host pressed "Auto mode"; the first ball is drawn at once (a <see cref="BallCalled"/> follows).</summary>
public sealed record AutoCallingStarted : SessionEvent;

/// <summary>The host pressed "Pause" (or a detected win paused the game): back to manual calling.</summary>
public sealed record AutoCallingPaused : SessionEvent;

/// <summary>
/// A ball was drawn. <see cref="PreviousNumber"/> is the ball before it in this round,
/// which participants with auto-fill on get filled in (FR-16).
/// </summary>
public sealed record BallCalled(BallCall Call, int? PreviousNumber) : SessionEvent;

public sealed record WinConditionChanged(string WinConditionId) : SessionEvent;

public sealed record SettingsChanged(SessionSettings Settings) : SessionEvent;

/// <summary>
/// One rule that changed, for the "Rules updated" notice (FR-47): <see cref="Rule"/> is the
/// setting's wire name (<c>winCondition</c>, <c>freeMarking</c>, …) and <see cref="Value"/> its new
/// value (<c>blackout</c>, <c>false</c>, <c>MismarksOnly</c>). Every change listed is in force at once.
/// </summary>
public sealed record RuleChange(string Rule, string Value);

/// <summary>
/// Rules that matter to players changed mid-round (FR-47). Every screen shows the list until
/// it is closed or the next ball is called, which can't happen for <see cref="Session.RulesReadingTime"/>.
/// </summary>
public sealed record RulesUpdated(IReadOnlyList<RuleChange> Changes, DateTimeOffset NextBallFrom) : SessionEvent;

public sealed record SessionEnded : SessionEvent;

/// <summary>The host's note shown with the join QR (FR-49), e.g. the password or a hint; <c>null</c> when cleared.</summary>
public sealed record JoinNoteChanged(string? Note) : SessionEvent;

/// <summary>The host chose which side of a custom set's items the call boards show (FR-29).</summary>
public sealed record BoardSideChanged(BoardSide Side) : SessionEvent;

/// <summary>
/// Which face of a custom set's items the call boards show (FR-29, ADR 0009): the ball side (what is
/// called; the default, so a board of words can't be used as an answer list) or the square side (what is
/// marked, easier to scan when the balls are long definitions). Numbers look the same either way.
/// </summary>
public enum BoardSide
{
    Ball,
    Square,
}
