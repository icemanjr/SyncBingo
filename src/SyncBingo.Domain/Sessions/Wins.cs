namespace SyncBingo.Domain.Sessions;

public enum ClaimStatus
{
    Pending,
    Awarded,
    Dismissed,
}

/// <summary>
/// A Bingo press (FR-37a). Every press is recorded and sent to the host, valid or not;
/// <see cref="Qualified"/> says whether the card met the win condition at that moment.
/// </summary>
public sealed record Claim(
    int Id,
    int Round,
    string ParticipantId,
    string Name,
    int CardId,
    string WinConditionId,
    DateTimeOffset ClaimedAt,
    bool Qualified,
    ClaimStatus Status);

public enum WinSource
{
    /// <summary>Awarded after a Bingo press.</summary>
    Claimed,

    /// <summary>Awarded after win detection alerted the host.</summary>
    Detected,

    /// <summary>Awarded with no alert, e.g. a shouted Bingo the host checked (FR-38).</summary>
    Manual,
}

/// <summary>A win the host credited (ADR 0008). One per participant, win condition and round.</summary>
public sealed record WinRecord(
    int Round,
    string ParticipantId,
    string Name,
    int? CardId,
    string WinConditionId,
    DateTimeOffset AwardedAt,
    WinSource Source);

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

/// <summary>A card met the win condition on called numbers, whether or not the player noticed (host only, FR-19).</summary>
public sealed record WinDetected(string ParticipantId, string Name, int CardId, string WinConditionId) : SessionEvent;

/// <summary>The host credited a win.</summary>
public sealed record WinAwarded(WinRecord Win) : SessionEvent;

public sealed record LeaderboardVisibilityChanged(bool Visible) : SessionEvent;

/// <summary>The host removed a participant (FR-7); their pending claims are dismissed.</summary>
public sealed record ParticipantRemoved(string ParticipantId, string Name) : SessionEvent;
