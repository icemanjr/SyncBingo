namespace SyncBingo.Domain.Participants;

/// <summary>
/// A participant as plain data, for storage (docs/04 §4): the store saves this and
/// <see cref="Participant.Restore"/> rebuilds the participant from it.
/// </summary>
public sealed record ParticipantSnapshot(
    string Id,
    string Name,
    DateTimeOffset JoinedAt,
    bool AutoFillOn,
    IReadOnlyList<PlayerCardSnapshot> Cards);

/// <summary>One card in a <see cref="ParticipantSnapshot"/>, including its Undo state (FR-25).</summary>
public sealed record PlayerCardSnapshot(
    int Id,
    IReadOnlyList<int> Cells,
    uint Marks,
    uint UndoMarks,
    int UndoSequence,
    int IssuedInRound);
