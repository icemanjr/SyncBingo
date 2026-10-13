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
public sealed record BallCall(int Sequence, int Number, string Letter, DateTimeOffset CalledAt, DrawSource Source);

/// <summary>
/// Something that happened to a session, published to clients over SignalR.
/// <see cref="Seq"/> increases by one per event across the whole session (ADR 0002).
/// </summary>
public abstract record SessionEvent
{
    public long Seq { get; init; }
}

/// <summary>"Start Round N": the countdown runs until <see cref="EndsAt"/>; clients count down locally.</summary>
public sealed record RoundCountdownStarted(int Round, DateTimeOffset EndsAt, bool StartInAuto) : SessionEvent;

public sealed record RoundCountdownCancelled(int Round) : SessionEvent;

/// <summary>The countdown reached zero. If it was started in auto mode, <see cref="AutoCallingStarted"/> and the first <see cref="BallCalled"/> follow.</summary>
public sealed record RoundStarted(int Round) : SessionEvent;

/// <summary>"Clear cards": marks and calls are cleared (cards replaced if <see cref="Redraw"/>), back in the lobby.</summary>
public sealed record CardsCleared(bool Redraw) : SessionEvent;

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

public sealed record SettingsChanged(SessionSettings Settings, SessionSettings? NextRoundSettings) : SessionEvent;

public sealed record SessionEnded : SessionEvent;
