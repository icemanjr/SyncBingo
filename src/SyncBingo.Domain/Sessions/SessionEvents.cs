namespace SyncBingo.Domain.Sessions;

public enum SessionState
{
    /// <summary>The lobby and the gap between rounds: joining and redraws happen here.</summary>
    BetweenRounds,

    Running,

    /// <summary>Still in the round: marks and manual draws continue, auto-calls stop.</summary>
    Paused,

    /// <summary>Terminal: the host ended the session (or it expired).</summary>
    Ended,
}

public enum DrawSource
{
    /// <summary>The host pressed "Grab a new ball".</summary>
    Manual,

    /// <summary>The auto-call scheduler.</summary>
    Auto,
}

/// <summary>One called ball. <see cref="Sequence"/> counts calls within the round, from 1.</summary>
public sealed record BallCall(int Sequence, int Number, string Letter, DateTimeOffset CalledAt, DrawSource Source);

/// <summary>
/// Something that happened to a session, published to clients over SignalR.
/// <see cref="Seq"/> increases by one per event across the whole session (ADR 0002).
/// </summary>
public abstract record SessionEvent
{
    public long Seq { get; init; }
}

public sealed record RoundStarted(int Round, bool RedrawCards) : SessionEvent;

public sealed record RoundEnded(int Round) : SessionEvent;

public sealed record SessionPaused : SessionEvent;

public sealed record SessionResumed : SessionEvent;

/// <summary>
/// A ball was drawn. <see cref="PreviousNumber"/> is the ball before it in this round,
/// which participants with auto-fill on get filled in (FR-16).
/// </summary>
public sealed record BallCalled(BallCall Call, int? PreviousNumber) : SessionEvent;

public sealed record WinConditionChanged(string WinConditionId) : SessionEvent;

public sealed record SettingsChanged(SessionSettings Settings, SessionSettings? NextRoundSettings) : SessionEvent;

public sealed record SessionEnded : SessionEvent;
