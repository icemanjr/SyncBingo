using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>A participant survives being stored and restored, Undo state included.</summary>
public sealed class SnapshotTests
{
    [Fact]
    public void A_restored_participant_has_the_same_snapshot()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { AutoFillAvailable = true });
        kit.Player.SetAutoFill(true, kit.Session).IsSuccess.ShouldBeTrue();
        kit.Call(1);
        kit.Mark(0);
        kit.Mark(7);

        var snapshot = kit.Player.ToSnapshot();
        var restored = Participant.Restore(snapshot).ToSnapshot();

        restored.ShouldBeEquivalentTo(snapshot);
    }

    [Fact]
    public void Undo_still_works_after_a_restore()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);
        kit.Mark(0);
        kit.Mark(7); // a mismark, saved before the player undoes it

        var restored = Participant.Restore(kit.Player.ToSnapshot());

        restored.Undo(1, kit.Session).Value.ShouldBe(Bits(Free));
    }

    [Fact]
    public void A_restored_participant_keeps_its_name_and_card()
    {
        var kit = new PlayerTestKit();

        var restored = Participant.Restore(kit.Player.ToSnapshot());

        restored.Name.ShouldBe(kit.Player.Name);
        restored.Cards[0].Card.ShouldBe(KnownCard);
        restored.Cards[0].IssuedInRound.ShouldBe(kit.Card.IssuedInRound);
    }

    [Fact]
    public void A_corrupt_name_is_rejected()
    {
        var snapshot = new PlayerTestKit().Player.ToSnapshot() with { Name = "<script>" };

        Should.Throw<InvalidOperationException>(() => Participant.Restore(snapshot));
    }
}
