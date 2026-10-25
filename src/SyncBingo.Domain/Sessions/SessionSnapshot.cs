using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Sessions;

/// <summary>
/// A session as plain data, for storage (docs/04 §4): the store saves this and
/// <see cref="Session.Restore"/> rebuilds the session from it. The variant is stored
/// by id; the called-number set is rebuilt from <see cref="Calls"/>. A custom set is stored apart from the
/// session (it changes rarely and can be 64 KB); <see cref="ContentName"/> says one is in use.
/// </summary>
public sealed record SessionSnapshot(
    string Code,
    string VariantId,
    SessionState State,
    DateTimeOffset? CountdownEndsAt,
    bool StartInAuto,
    SessionSettings Settings,
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
    DateTimeOffset? NextBallFrom = null,
    StreamSeqs StreamSeqs = default,
    IReadOnlyList<string>? AwardKeys = null,
    string? JoinNote = null,
    string? ContentName = null,
    bool GroupItems = false,
    BoardSide BoardSide = BoardSide.Ball,
    bool PlayersMayDownload = false,
    IReadOnlyList<CustomPattern>? CustomPatterns = null);

/// <summary>Lines already alerted for a card and win condition this round, so detection never repeats an alert.</summary>
public sealed record WinAlert(string ParticipantId, int CardId, string WinConditionId, int Lines);
