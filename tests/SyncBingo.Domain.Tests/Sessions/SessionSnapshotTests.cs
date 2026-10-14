using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>A session survives being stored and restored, and behaves the same afterwards.</summary>
public sealed class SessionSnapshotTests
{
    [Fact]
    public void A_busy_session_restores_to_the_same_snapshot()
    {
        var kit = PlayedRound();

        var snapshot = kit.Session.ToSnapshot();

        Session.Restore(snapshot).ToSnapshot().ShouldBeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData(SessionState.BetweenRounds)]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.Manual)]
    [InlineData(SessionState.Auto)]
    [InlineData(SessionState.Ended)]
    public void Every_state_restores(SessionState state)
    {
        var snapshot = InState(state).ToSnapshot();

        var restored = Session.Restore(snapshot);

        restored.State.ShouldBe(state);
        restored.ToSnapshot().ShouldBeEquivalentTo(snapshot);
    }

    [Fact]
    public void A_restored_session_carries_on_where_it_left_off()
    {
        var kit = PlayedRound();
        var original = kit.Session;

        var restored = Session.Restore(original.ToSnapshot());

        restored.HasBeenCalled(original.LastCall!.Number).ShouldBeTrue("the called set is rebuilt from the calls");
        restored.BallsRemaining.ShouldBe(original.BallsRemaining);
        restored.Leaderboard().ShouldBe(original.Leaderboard());
        restored.ChangePattern("normal", Now).IsSuccess.ShouldBeTrue();
        restored.DetectWins([kit.Player], Now).Value.ShouldBeEmpty("the line alerted before the restore is not alerted again");
        restored.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Error!.Message
            .ShouldBe("Give everyone a moment to read the new rules.", "the reading time survives a restore");
        restored.DrawBall(DrawSource.Manual, new SeededRandom(1), restored.NextBallFrom!.Value).Single<BallCalled>().Seq.ShouldBe(restored.LastEventSeq);
        restored.LastEventSeq.ShouldBe(original.LastEventSeq + 3, "numbering carries on after the restore (pattern, notice, ball)");
    }

    [Fact]
    public void A_snapshot_with_an_unknown_variant_is_rejected()
    {
        var snapshot = NewSession().ToSnapshot() with { VariantId = "no-such-variant" };

        Should.Throw<InvalidOperationException>(() => Session.Restore(snapshot));
    }

    /// <summary>A round with calls, a detected and claimed line, an award and a pattern change.</summary>
    private static PlayerTestKit PlayedRound()
    {
        var kit = new PlayerTestKit();
        foreach (var number in new[] { 1, 16, 31, 46, 61 })
        {
            kit.Call(number);
        }

        kit.Session.DetectWins([kit.Player], Now).Value.ShouldNotBeEmpty();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, 1, Now, claimId: 1).IsSuccess.ShouldBeTrue();
        kit.Session.ChangePattern("blackout", Now).IsSuccess.ShouldBeTrue();
        kit.Session.ChangeSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ShowLeaderboard(true, Now).IsSuccess.ShouldBeTrue();
        return kit;
    }
}
