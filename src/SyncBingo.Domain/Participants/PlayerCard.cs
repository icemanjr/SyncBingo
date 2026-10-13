using SyncBingo.Domain.Cards;

namespace SyncBingo.Domain.Participants;

/// <summary>
/// A card in a participant's hands: the numbers, the player's marks, and the state
/// Undo returns to (FR-25).
/// </summary>
public sealed class PlayerCard
{
    /// <summary><see cref="UndoSequence"/> value meaning "no snapshot this round".</summary>
    internal const int NoSnapshot = -1;

    internal PlayerCard(int id, Card card, uint marks, int issuedInRound)
    {
        Id = id;
        Card = card;
        Marks = marks;
        IssuedInRound = issuedInRound;
    }

    /// <summary>Identifies the card among its owner's cards.</summary>
    public int Id { get; }

    public Card Card { get; internal set; }

    /// <summary>Bit <c>i</c> is set when the player marked cell <c>i</c> (mismarks included).</summary>
    public uint Marks { get; internal set; }

    /// <summary>The marks Undo restores: the card as it was when ball <see cref="UndoSequence"/> was called.</summary>
    public uint UndoMarks { get; internal set; }

    /// <summary>The ball (call sequence; 0 = before the first ball) <see cref="UndoMarks"/> belongs to.</summary>
    public int UndoSequence { get; internal set; } = NoSnapshot;

    public int IssuedInRound { get; internal set; }

    public bool IsMarked(int cell) => (Marks & (1u << cell)) != 0;

    /// <summary>
    /// Saves the marks before the first change since the latest ball, so Undo can
    /// return to them. Later changes before the next ball keep that snapshot.
    /// </summary>
    internal void SnapshotBeforeChange(int currentSequence)
    {
        if (UndoSequence != currentSequence)
        {
            UndoMarks = Marks;
            UndoSequence = currentSequence;
        }
    }

    internal void Reset(Card card, uint marks, int round)
    {
        Card = card;
        Marks = marks;
        IssuedInRound = round;
        UndoMarks = 0;
        UndoSequence = NoSnapshot;
    }
}
