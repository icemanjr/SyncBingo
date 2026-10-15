namespace SyncBingo.Domain.Sessions;

public enum ClaimStatus
{
    Pending,
    Awarded,
    Dismissed,
}

/// <summary>
/// A Bingo press (FR-37a). Every press is recorded and sent to the host, valid or not;
/// <see cref="Matches"/> is how many lines the card completed at that moment (0 = not
/// qualified, 2 = a "double Bingo" on Normal) and <see cref="AlreadyAwarded"/> how many
/// wins this card already got for this pattern this round, so the host can check a
/// declaration, including a second line the player only noticed later.
/// <see cref="Awardable"/> is how many more wins the card could be awarded under the
/// <see cref="MultipleWins"/> setting; 0 shows the host "no awardable matches".
/// <see cref="AlreadyWon"/> means the card can't be awarded again: in the "once" modes it has
/// already won this pattern (or, once per round, anything) this round, so the host gets no Award button.
/// </summary>
public sealed record Claim(
    int Id,
    int Round,
    string ParticipantId,
    string Name,
    int CardId,
    string WinConditionId,
    DateTimeOffset ClaimedAt,
    int Matches,
    int AlreadyAwarded,
    int Awardable,
    ClaimStatus Status,
    bool AlreadyWon = false)
{
    public bool Qualified => Matches > 0;
}

public enum WinSource
{
    /// <summary>Awarded after a Bingo press.</summary>
    Claimed,

    /// <summary>Awarded after win detection alerted the host.</summary>
    Detected,

    /// <summary>Awarded with no alert, e.g. a shouted Bingo the host checked (FR-38).</summary>
    Manual,
}

/// <summary>
/// Wins the host credited in one award (ADR 0008): usually one, more when the player
/// declared several and <see cref="MultipleWins.EveryDeclaredWin"/> is on (FR-43).
/// </summary>
public sealed record WinRecord(
    int Round,
    string ParticipantId,
    string Name,
    int CardId,
    string WinConditionId,
    DateTimeOffset AwardedAt,
    WinSource Source,
    int Wins = 1);

public sealed record LeaderboardEntry(string ParticipantId, string Name, int Wins);

/// <summary>Session statistics for one round (FR-40).</summary>
public sealed record RoundSummary(
    int Number,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<string> WinConditionsUsed,
    IReadOnlyList<WinRecord> Wins);

/// <summary>Every Bingo press, valid or not (host only).</summary>
public sealed record WinClaimed(Claim Claim) : SessionEvent;

/// <summary>The host cleared a claim without awarding it (host only).</summary>
public sealed record ClaimDismissed(int ClaimId) : SessionEvent;

/// <summary>
/// A card met the win condition on called numbers, whether or not the player noticed
/// (host only, FR-19). <see cref="Matches"/> is how many lines it completes.
/// </summary>
public sealed record WinDetected(string ParticipantId, string Name, int CardId, string WinConditionId, int Matches) : SessionEvent;

/// <summary>The host credited a win.</summary>
public sealed record WinAwarded(WinRecord Win) : SessionEvent;

public sealed record LeaderboardVisibilityChanged(bool Visible) : SessionEvent;

/// <summary>The host removed a participant (FR-7); their pending claims are dismissed.</summary>
public sealed record ParticipantRemoved(string ParticipantId, string Name) : SessionEvent;
