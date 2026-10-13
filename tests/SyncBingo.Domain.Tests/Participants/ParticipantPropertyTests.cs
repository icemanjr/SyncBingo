using FsCheck.Xunit;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>
/// Random settings and random sequences of draws, marks, unmarks and undos. After every
/// step the marking rules must hold, and Undo must always land on the card as it was
/// right after the latest draw.
/// </summary>
public sealed class ParticipantPropertyTests
{
    [Property(MaxTest = 400)]
    public void Marking_rules_and_undo_hold_for_any_play(
        int seed, byte[] script, bool freeMarking, byte unmarking, bool earnedFreeSpace, bool autoFill)
    {
        var settings = SessionSettings.Default with
        {
            FreeMarking = freeMarking,
            Unmarking = (Unmarking)(unmarking % 3),
            FreeSpace = earnedFreeSpace ? FreeSpace.Earned : FreeSpace.AlwaysMarked,
            AutoFillAvailable = autoFill,
        };
        var kit = new PlayerTestKit(settings);
        kit.Player.SetAutoFill(autoFill, kit.Session).IsSuccess.ShouldBeTrue();
        var random = new SeededRandom(seed);
        var marksAtLastCall = kit.Marks;

        foreach (var step in script)
        {
            var cell = step % 25;
            switch (step % 4)
            {
                case 0 when kit.Session.BallsRemaining > 0:
                    var uncalled = Enumerable.Range(1, 75).Where(n => !kit.Session.HasBeenCalled(n)).ToList();
                    kit.CallWithAutoFill(uncalled[random.NextInt(uncalled.Count)]);
                    marksAtLastCall = kit.Marks;
                    break;
                case 1:
                    kit.Mark(cell);
                    break;
                case 2:
                    kit.Unmark(cell);
                    break;
                case 3:
                    kit.Undo().Value.ShouldBe(marksAtLastCall);
                    break;
            }

            var called = CardRules.CalledMask(kit.Card.Card, kit.Session);
            if (!freeMarking)
            {
                (kit.Marks & ~called).ShouldBe(0u, "without free marking, only called cells can be marked");
            }

            if (!earnedFreeSpace)
            {
                (kit.Marks & Bits(Free)).ShouldBe(Bits(Free), "an always-marked free space stays marked");
            }

            if (autoFill && kit.Session.Calls.Count >= 2)
            {
                var previous = kit.Session.Calls[^2].Number;
                if (kit.Card.Card.CellOf(previous) is { } filled && kit.Card.UndoSequence != kit.Session.Calls.Count)
                {
                    kit.Card.IsMarked(filled).ShouldBeTrue("auto-fill marks the previous ball before any later change");
                }
            }
        }

        kit.Undo().Value.ShouldBe(marksAtLastCall);
    }
}
