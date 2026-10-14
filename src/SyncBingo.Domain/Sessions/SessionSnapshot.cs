namespace SyncBingo.Domain.Sessions;

/// <summary>
/// A session as plain data, for storage (docs/04 §4): the store saves this and
/// <see cref="Session.Restore"/> rebuilds the session from it. The variant is stored
/// by id; the called-number set is rebuilt from <see cref="Calls"/>.
/// </summary>
public sealed record SessionSnapshot(
    string Code,
    string VariantId,
    SessionState State,
    DateTimeOffset? CountdownEndsAt,
    bool StartInAuto,
    SessionSettings Settings,
    SessionSettings? NextRoundSettings,
    string WinConditionId,
    int RoundNumber,
    IReadOnlyList<BallCall> Calls,
    long LastEventSeq,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    IReadOnlyList<Claim> Claims,
    IReadOnlyList<WinRecord> Wins,
    IReadOnlyList<RoundSummary> Rounds,
    bool LeaderboardVisible,
    IReadOnlyList<WinAlert> Alerts,
    DateTimeOffset? NextBallFrom = null);

/// <summary>Lines already alerted for a card and win condition this round, so detection never repeats an alert.</summary>
public sealed record WinAlert(string ParticipantId, int CardId, string WinConditionId, int Lines);
