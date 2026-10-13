using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Domain.Participants;

/// <summary>A cell auto-fill marked on one of a participant's cards.</summary>
public sealed record AutoFilledCell(int CardId, int Cell);

/// <summary>A card swapped for a new one; the old fingerprint must be released (FR-39).</summary>
public sealed record CardReplacement(int CardId, Card OldCard, Card NewCard);

/// <summary>
/// One player and their cards. Changed only by that player (marks, Undo, redraw,
/// auto-fill switch) or by a draw's auto-fill, never under the session lock
/// (docs/02 §6). Rules that depend on the game read the <see cref="Session"/>
/// passed in, without changing it.
/// </summary>
public sealed class Participant
{
    private readonly List<PlayerCard> _cards = [];

    private Participant(string id, ParticipantName name, DateTimeOffset joinedAt)
    {
        Id = id;
        Name = name;
        JoinedAt = joinedAt;
    }

    public string Id { get; }

    public ParticipantName Name { get; }

    public DateTimeOffset JoinedAt { get; }

    /// <summary>The player's own auto-fill switch (FR-16); only effective while the host makes it available.</summary>
    public bool AutoFillOn { get; private set; }

    /// <summary>One card in the MVP; a list so "Serious Bingo" needs no model change.</summary>
    public IReadOnlyList<PlayerCard> Cards => _cards;

    /// <summary>A new participant holding <paramref name="card"/>, already claimed as unique (FR-39).</summary>
    public static Participant Join(string id, ParticipantName name, Card card, Session session, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(session);

        var participant = new Participant(id, name, now);
        participant._cards.Add(new PlayerCard(1, card, CardRules.InitialMarks(session), session.UpcomingRound));
        return participant;
    }

    /// <summary>
    /// Mark a cell. With <see cref="SessionSettings.FreeMarking"/> off, only called cells (and
    /// an earned free cell) may be marked (FR-22). Marking a marked cell changes nothing.
    /// </summary>
    public Result<uint> Mark(int cardId, int cell, Session session)
    {
        var found = Locate(cardId, cell, session, "mark");
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        var card = found.Value;

        if (!session.Settings.FreeMarking && (CardRules.CalledMask(card.Card, session) & Bit(cell)) == 0)
        {
            return Refuse("Only called numbers can be marked in this game.");
        }

        return Change(card, card.Marks | Bit(cell), session);
    }

    /// <summary>
    /// Unmark a cell, as <see cref="SessionSettings.Unmarking"/> allows (FR-24). An always-marked
    /// free cell can never be unmarked. Unmarking an unmarked cell changes nothing.
    /// </summary>
    public Result<uint> Unmark(int cardId, int cell, Session session)
    {
        var found = Locate(cardId, cell, session, "unmark");
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        var card = found.Value;

        var bit = Bit(cell);
        if (cell == session.Variant.Layout.FreeCell && session.Settings.FreeSpace == FreeSpace.AlwaysMarked)
        {
            return Refuse("The free space is always marked.");
        }

        var allowed = session.Settings.Unmarking switch
        {
            Unmarking.Any => true,
            Unmarking.MismarksOnly => (CardRules.CalledMask(card.Card, session) & bit) == 0,
            _ => false,
        };

        if (!allowed)
        {
            return Refuse(session.Settings.Unmarking == Unmarking.Off
                ? "Marks can't be removed in this game. Use Undo before the next ball."
                : "Only squares whose number hasn't been called can be unmarked.");
        }

        return Change(card, card.Marks & ~bit, session);
    }

    /// <summary>
    /// Undo (FR-25): return the card to its marks when the last ball was called, whatever
    /// the unmarking setting. Does nothing if the card has not changed since that ball.
    /// </summary>
    public Result<uint> Undo(int cardId, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsInRound)
        {
            return WrongState("undo");
        }

        if (FindCard(cardId) is not { } card)
        {
            return UnknownCard(cardId);
        }

        if (card.UndoSequence == session.Calls.Count)
        {
            card.Marks = card.UndoMarks;
        }

        return Result.Success(card.Marks);
    }

    /// <summary>Switch auto-fill for this player's own cards (FR-16).</summary>
    public Result<bool> SetAutoFill(bool on, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.State == SessionState.Ended)
        {
            return new DomainError(DomainErrorCode.WrongState, "The game has ended.");
        }

        if (on && !session.Settings.AutoFillAvailable)
        {
            return Refuse("The host hasn't made auto-fill available.");
        }

        AutoFillOn = on;
        return Result.Success(on);
    }

    /// <summary>
    /// Auto-fill for a draw (FR-16): if this player has auto-fill on and the host makes it
    /// available, mark <paramref name="previousNumber"/> wherever it is still unmarked, and
    /// the free cell if that ball was the one that earned it (FR-26). Called while the new
    /// ball is drawn, before it is announced, so the last ball of a round is never filled.
    /// </summary>
    public IReadOnlyList<AutoFilledCell> ApplyAutoFill(int previousNumber, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!AutoFillOn || !session.Settings.AutoFillAvailable || !session.IsInRound)
        {
            return [];
        }

        var filled = new List<AutoFilledCell>();
        foreach (var card in _cards)
        {
            var target = card.Card.CellOf(previousNumber);
            if (target is null
                && session.Settings.FreeSpace == FreeSpace.Earned
                && CardRules.EarningCall(card.Card, session)?.Number == previousNumber)
            {
                target = session.Variant.Layout.FreeCell;
            }

            if (target is { } cell && !card.IsMarked(cell))
            {
                card.Marks |= Bit(cell);
                filled.Add(new AutoFilledCell(card.Id, cell));
            }
        }

        return filled;
    }

    /// <summary>
    /// Swap a card for a new one between rounds, when the host allows it (FR-20).
    /// <paramref name="deal"/> is only called once the redraw is allowed, so no card is
    /// claimed for a refused request. The caller releases the old card's fingerprint.
    /// </summary>
    public Result<CardReplacement> RedrawCard(int cardId, Session session, Func<Card> deal)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deal);

        if (session.State != SessionState.BetweenRounds)
        {
            return WrongState("redraw a card");
        }

        if (session.Settings.RedrawPolicy != RedrawPolicy.ParticipantsMayRedraw)
        {
            return Refuse("Only the host can redraw cards in this game.");
        }

        if (FindCard(cardId) is not { } card)
        {
            return UnknownCard(cardId);
        }

        var old = card.Card;
        card.Reset(deal(), CardRules.InitialMarks(session), session.UpcomingRound);
        return Result.Success(new CardReplacement(card.Id, old, card.Card));
    }

    /// <summary>
    /// Clear the cards for a new round (FR-34): marks back to the initial state under the
    /// new round's settings, Undo cleared, and optionally a new card from <paramref name="deal"/>
    /// (host redraw). Call after <see cref="Session.ClearCards"/> and again after <see cref="Session.StartRound"/>,
    /// so the marks follow the settings the round actually starts with. Returns the replacements.
    /// </summary>
    public IReadOnlyList<CardReplacement> ResetForNewRound(Session session, Func<Card>? deal)
    {
        ArgumentNullException.ThrowIfNull(session);

        var replaced = new List<CardReplacement>();
        foreach (var card in _cards)
        {
            var old = card.Card;
            var next = deal?.Invoke() ?? old;
            card.Reset(next, CardRules.InitialMarks(session), deal is null ? card.IssuedInRound : session.UpcomingRound);
            if (deal is not null)
            {
                replaced.Add(new CardReplacement(card.Id, old, next));
            }
        }

        return replaced;
    }

    /// <summary>Whether a card qualifies for the current win condition (FR-23); see <see cref="CardRules.Qualifies"/>.</summary>
    public Result<bool> Qualifies(int cardId, Session session)
    {
        var matches = Matches(cardId, session);
        return matches.IsSuccess ? Result.Success(matches.Value > 0) : matches.Error!;
    }

    /// <summary>How many lines (pattern masks) a card completes under claim rules; see <see cref="CardRules.MatchCount"/>.</summary>
    public Result<int> Matches(int cardId, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return FindCard(cardId) is { } card
            ? Result.Success(CardRules.MatchCount(card.Card, card.Marks, session))
            : UnknownCard(cardId);
    }

    private Result<PlayerCard> Locate(int cardId, int cell, Session session, string action)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsInRound)
        {
            return WrongState(action);
        }

        if (FindCard(cardId) is not { } card)
        {
            return UnknownCard(cardId);
        }

        return cell >= 0 && cell < session.Variant.Layout.CellCount
            ? Result.Success(card)
            : new DomainError(DomainErrorCode.Invalid, $"Cell {cell} is not on the card.");
    }

    private PlayerCard? FindCard(int cardId) => _cards.Find(card => card.Id == cardId);

    private static Result<uint> Change(PlayerCard card, uint marks, Session session)
    {
        if (marks != card.Marks)
        {
            card.SnapshotBeforeChange(session.Calls.Count);
            card.Marks = marks;
        }

        return Result.Success(card.Marks);
    }

    private static uint Bit(int cell) => 1u << cell;

    private static DomainError Refuse(string message) => new(DomainErrorCode.NotAllowed, message);

    private static DomainError UnknownCard(int cardId) => new(DomainErrorCode.Invalid, $"You have no card {cardId}.");

    private static DomainError WrongState(string action) =>
        new(DomainErrorCode.WrongState, $"You can only {action} during a round.");
}
