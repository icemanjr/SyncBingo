using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Domain.Participants;

/// <summary>
/// How a card relates to the balls called this round (docs/03 §4). Wins, claims and
/// strict marking all judge a card by its <see cref="CalledMask"/>, never by marks alone.
/// </summary>
public static class CardRules
{
    /// <summary>
    /// Cells whose numbers have been called, plus the free cell when it counts:
    /// always with <see cref="FreeSpace.AlwaysMarked"/>, and with <see cref="FreeSpace.Earned"/>
    /// only once a called ball is missing from this card (FR-26).
    /// </summary>
    public static uint CalledMask(Card card, Session session)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(session);

        var mask = 0u;
        for (var cell = 0; cell < card.Cells.Length; cell++)
        {
            var number = card.Cells[cell];
            if (number != Card.FreeCellNumber && session.HasBeenCalled(number))
            {
                mask |= 1u << cell;
            }
        }

        return FreeCellCounts(card, session) ? mask | session.Variant.Layout.FreeCellMask : mask;
    }

    /// <summary>Whether the free cell counts as called for this card right now.</summary>
    public static bool FreeCellCounts(Card card, Session session) =>
        session.Settings.FreeSpace == FreeSpace.AlwaysMarked || EarningCall(card, session) is not null;

    /// <summary>
    /// With an <see cref="FreeSpace.Earned"/> free space: the first ball called this round
    /// that is not on <paramref name="card"/>, which earns the free cell; <c>null</c> if none yet.
    /// </summary>
    public static BallCall? EarningCall(Card card, Session session)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(session);

        foreach (var call in session.Calls)
        {
            if (card.CellOf(call.Number) is null)
            {
                return call;
            }
        }

        return null;
    }

    /// <summary>Marks a fresh card starts with: the free cell, unless it must be earned.</summary>
    public static uint InitialMarks(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Settings.FreeSpace == FreeSpace.AlwaysMarked ? session.Variant.Layout.FreeCellMask : 0;
    }

    /// <summary>
    /// How many masks of the selected patterns the card completes: for Normal Bingo the
    /// number of finished lines (two when one ball completes a row and a column), for
    /// single-shape patterns 0 or 1. With <paramref name="marks"/> <c>null</c> only called
    /// cells count (win detection, FR-19); otherwise this follows claim rules (FR-23).
    /// </summary>
    public static int MatchCount(Card card, uint? marks, Session session) =>
        session.Selection.MatchCount(Covered(card, marks, session));

    /// <summary>The selected patterns the card completes, judged as <see cref="MatchCount"/> is (for the host's eyes only).</summary>
    public static IReadOnlyList<string> CompletedPatterns(Card card, uint? marks, Session session) =>
        [.. session.Selection.Completed(Covered(card, marks, session)).Select(pattern => pattern.Id)];

    /// <summary>The cells that count: called ones, and with "Bingo needs marks" only those also marked.</summary>
    private static uint Covered(Card card, uint? marks, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var called = CalledMask(card, session);
        return marks is { } m && session.Settings.RequireMarksForClaims ? called & m : called;
    }
}
