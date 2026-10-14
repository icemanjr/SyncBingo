using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>
/// A session in manual mode with one participant holding a known card:
/// <code>
///   B   I   N   G   O        cells
///   1  16  31  46  61         0  1  2  3  4
///   2  17  32  47  62         5  6  7  8  9
///   3  18  ★   48  63        10 11 12 13 14
///   4  19  34  49  64        15 16 17 18 19
///   5  20  35  50  65        20 21 22 23 24
/// </code>
/// Numbers 6–15, 21–30, 33, 36–45, 51–60 and 66–75 are not on the card.
/// </summary>
internal sealed class PlayerTestKit
{
    public const int Free = 12;

    public static readonly Card KnownCard = new([
        1, 16, 31, 46, 61,
        2, 17, 32, 47, 62,
        3, 18, 0, 48, 63,
        4, 19, 34, 49, 64,
        5, 20, 35, 50, 65,
    ]);

    public PlayerTestKit(SessionSettings? settings = null)
    {
        Session = NewSession(settings);
        Player = Participant.Join("p1", Name("Sam"), KnownCard, Session, Now);
        Session.NextRound(Now);
        Player.ResetForNewRound(Session, deal: null);
    }

    public Session Session { get; }

    public Participant Player { get; }

    public PlayerCard Card => Player.Cards[0];

    public uint Marks => Card.Marks;

    public static ParticipantName Name(string name)
    {
        ParticipantName.TryCreate(name, out var parsed, out _).ShouldBeTrue();
        return parsed;
    }

    /// <summary>A random source that makes the next draw pick exactly <paramref name="number"/>.</summary>
    public IRandomSource Picks(int number) =>
        new FixedRandom(Enumerable.Range(1, number - 1).Count(n => !Session.HasBeenCalled(n)));

    /// <summary>
    /// Calls exactly <paramref name="number"/> (manual draw) and returns the previous ball. Like a host,
    /// it waits out the reading time after a mid-round rules change (FR-47).
    /// </summary>
    public int? Call(int number)
    {
        var called = Session.DrawBall(DrawSource.Manual, Picks(number), Session.NextBallFrom ?? Now).Single<BallCalled>();
        called.Call.Number.ShouldBe(number);
        return called.PreviousNumber;
    }

    /// <summary>Draws <paramref name="number"/> and applies auto-fill for the previous ball, as the draw handler will.</summary>
    public IReadOnlyList<AutoFilledCell> CallWithAutoFill(int number) =>
        Call(number) is { } previous ? Player.ApplyAutoFill(previous, Session) : [];

    public Result<uint> Mark(int cell) => Player.Mark(1, cell, Session);

    public Result<uint> Unmark(int cell) => Player.Unmark(1, cell, Session);

    public Result<uint> Undo() => Player.Undo(1, Session);

    public static uint Bits(params int[] cells) => Domain.Tests.Cells.Of(cells);

    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int NextInt(int maxExclusive) => value;
    }
}
