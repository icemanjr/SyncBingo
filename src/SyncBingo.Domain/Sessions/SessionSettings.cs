using SyncBingo.Domain.Rules;

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

/// <summary>
/// The game's colour theme (FR-50a), chosen by the host for everyone; a player may pick another on their own
/// device. Colours only: the palettes themselves live in the web app.
/// </summary>
public enum Theme
{
    Classic,
    Ocean,
    Forest,
    Halloween,
    Winter,
}

public enum FreeSpace
{
    AlwaysMarked,
    Earned,

    /// <summary>No free space: the centre holds a ball like any other square, so a card needs one more item (FR-26).</summary>
    None,
}

/// <summary>
/// Host settings (docs/01 §3.2). Defaults follow the documented decisions; the rest
/// favour a friendly first game. <see cref="FreeSpace"/>, <see cref="CardSize"/>, <see cref="RedrawPolicy"/> and
/// <see cref="MultipleWins"/> change only in the lobby (see <see cref="Session.ApplyRules"/>).
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

    /// <summary>FR-50a: the game's colours, for everyone (players may override on their device). Not a rule: no notice.</summary>
    public Theme Theme { get; init; } = Theme.Classic;

    /// <summary>FR-26a: the card's size, lobby only; changing it deals everyone a new card.</summary>
    public CardSize CardSize { get; init; } = CardSize.FiveByFive;

    /// <summary>FR-46: label rounds "Round 3" / "Start Round 3"; off: "This round" / "Start New Round".</summary>
    public bool ShowRoundNumbers { get; init; } = true;

    /// <summary>FR-43. Changes apply from the next round.</summary>
    public MultipleWins MultipleWins { get; init; } = MultipleWins.OncePerPattern;

    public static SessionSettings Default { get; } = new();

    /// <summary>Players get the called balls: some setting shows them a ball, a match or the board (FR-12 to FR-14).</summary>
    public bool PlayersSeeCalls => ShowBallToPlayers || HighlightMatches || ShowCallBoard;

    /// <summary>
    /// The settings players notice, by wire name, for the "Rules updated" notice (FR-47). Left out
    /// on purpose: the auto-call interval and win detection (how the host runs the game), the
    /// cosmetic win highlight and round numbers, and the round-scoped settings, which can't change
    /// mid-round.
    /// </summary>
    private static readonly (string Rule, Func<SessionSettings, object> Value)[] PlayerFacing =
    [
        ("showBallToPlayers", s => s.ShowBallToPlayers),
        ("highlightMatches", s => s.HighlightMatches),
        ("showCallBoard", s => s.ShowCallBoard),
        ("autoFillAvailable", s => s.AutoFillAvailable),
        ("freeMarking", s => s.FreeMarking),
        ("requireMarksForClaims", s => s.RequireMarksForClaims),
        ("unmarking", s => s.Unmarking),
    ];

    /// <summary>Settings that shape a whole round, so they change only in the lobby: the card's size and free space, redraws and multiple wins.</summary>
    private static readonly (string Name, Func<SessionSettings, object> Value)[] RoundScoped =
    [
        ("free space", s => s.FreeSpace),
        ("card size", s => s.CardSize),
        ("redraw policy", s => s.RedrawPolicy),
        ("multiple wins", s => s.MultipleWins),
    ];

    /// <summary>The settings players would notice changing from these to <paramref name="next"/>.</summary>
    internal static IEnumerable<RuleChange> PlayerFacingChanges(SessionSettings current, SessionSettings next) =>
        PlayerFacing
            .Where(field => !Equals(field.Value(current), field.Value(next)))
            .Select(field => new RuleChange(field.Rule, Format(field.Value(next))));

    /// <summary>The names of round-scoped settings that differ in <paramref name="next"/>, e.g. "free space".</summary>
    internal List<string> RoundScopedChanges(SessionSettings next) =>
        [.. RoundScoped.Where(field => !Equals(field.Value(this), field.Value(next))).Select(field => field.Name)];

    private static string Format(object value) => value is bool flag ? (flag ? "true" : "false") : value.ToString()!;

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
            && Enum.IsDefined(CardSize)
            && Enum.IsDefined(Theme)
            && Enum.IsDefined(MultipleWins)
                ? null
                : new DomainError(DomainErrorCode.Invalid, "A setting has an unknown value.");
    }
}
