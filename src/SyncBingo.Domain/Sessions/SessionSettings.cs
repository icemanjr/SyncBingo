namespace SyncBingo.Domain.Sessions;

public enum WinDetection
{
    Off,
    AlertHost,
    AlertHostAndPause,
}

public enum RedrawPolicy
{
    HostControls,
    ParticipantsMayRedraw,
}

public enum Unmarking
{
    Off,
    MismarksOnly,
    Any,
}

/// <summary>How often a card can be awarded a win in one round (FR-43).</summary>
public enum MultipleWins
{
    /// <summary>A card that has won is done until the next "Clear cards".</summary>
    OncePerRound,

    /// <summary>One win per card per win condition; changing the pattern allows another.</summary>
    OncePerPattern,

    /// <summary>Every win the player declares, e.g. two for a "double Bingo", as the host awards them.</summary>
    EveryDeclaredWin,
}

public enum FreeSpace
{
    AlwaysMarked,
    Earned,
}

/// <summary>
/// Host settings (docs/01 §3.2). Defaults follow the documented decisions; the rest
/// favour a friendly first game. <see cref="FreeSpace"/>, <see cref="RedrawPolicy"/> and
/// <see cref="MultipleWins"/> only change between rounds (see <see cref="Session.UpdateSettings"/>).
/// </summary>
public sealed record SessionSettings
{
    public static readonly TimeSpan MinAutoCallInterval = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaxAutoCallInterval = TimeSpan.FromMinutes(2);

    /// <summary>Time between draws in auto mode (FR-10).</summary>
    public TimeSpan AutoCallInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>FR-12.</summary>
    public bool ShowBallToPlayers { get; init; } = true;

    /// <summary>FR-13.</summary>
    public bool HighlightMatches { get; init; }

    /// <summary>FR-14.</summary>
    public bool ShowCallBoard { get; init; } = true;

    /// <summary>FR-16: lets each participant switch auto-fill on for their own card.</summary>
    public bool AutoFillAvailable { get; init; }

    /// <summary>FR-17.</summary>
    public bool BingoButton { get; init; } = true;

    /// <summary>FR-18.</summary>
    public bool WinHighlight { get; init; } = true;

    /// <summary>FR-19.</summary>
    public WinDetection WinDetection { get; init; } = WinDetection.AlertHost;

    /// <summary>FR-20. Changes apply from the next round.</summary>
    public RedrawPolicy RedrawPolicy { get; init; } = RedrawPolicy.HostControls;

    /// <summary>FR-22: participants may mark uncalled numbers.</summary>
    public bool FreeMarking { get; init; } = true;

    /// <summary>FR-23.</summary>
    public bool RequireMarksForClaims { get; init; }

    /// <summary>FR-24.</summary>
    public Unmarking Unmarking { get; init; } = Unmarking.Any;

    /// <summary>FR-26. Changes apply from the next round.</summary>
    public FreeSpace FreeSpace { get; init; } = FreeSpace.AlwaysMarked;

    /// <summary>FR-46: label rounds "Round 3" / "Start Round 3"; off: "This round" / "Start New Round".</summary>
    public bool ShowRoundNumbers { get; init; } = true;

    /// <summary>FR-43. Changes apply from the next round.</summary>
    public MultipleWins MultipleWins { get; init; } = MultipleWins.OncePerPattern;

    public static SessionSettings Default { get; } = new();

    internal DomainError? Validate()
    {
        if (AutoCallInterval < MinAutoCallInterval || AutoCallInterval > MaxAutoCallInterval)
        {
            return new DomainError(
                DomainErrorCode.Invalid,
                $"The auto-call interval must be between {MinAutoCallInterval.TotalSeconds:0} and {MaxAutoCallInterval.TotalSeconds:0} seconds.");
        }

        return Enum.IsDefined(WinDetection)
            && Enum.IsDefined(RedrawPolicy)
            && Enum.IsDefined(Unmarking)
            && Enum.IsDefined(FreeSpace)
            && Enum.IsDefined(MultipleWins)
                ? null
                : new DomainError(DomainErrorCode.Invalid, "A setting has an unknown value.");
    }

    /// <summary>This round's settings with the round-scoped ones kept as they were.</summary>
    internal SessionSettings KeepRoundScopedFrom(SessionSettings current) =>
        this with { RedrawPolicy = current.RedrawPolicy, FreeSpace = current.FreeSpace, MultipleWins = current.MultipleWins };
}
