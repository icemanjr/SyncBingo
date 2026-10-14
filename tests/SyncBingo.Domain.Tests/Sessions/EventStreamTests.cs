using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>
/// Each audience numbers only the events it receives (ADR 0002, amended), so a client sees an
/// unbroken 1, 2, 3… and a gap always means a missed event.
/// </summary>
public sealed class EventStreamTests
{
    [Fact]
    public void A_bingo_press_counts_only_in_the_host_stream()
    {
        var kit = new PlayerTestKit();
        var before = kit.Session.StreamSeqs;

        var claimed = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>();

        claimed.Audience.ShouldBe(Audience.Host);
        claimed.Streams.ShouldBe(new StreamSeqs(0, before.Host + 1, 0));
        kit.Session.StreamSeqs.ShouldBe(before with { Host = before.Host + 1 });
    }

    [Fact]
    public void Players_hear_the_balls_only_when_a_setting_shows_them()
    {
        var hidden = new PlayerTestKit(SessionSettings.Default with { ShowBallToPlayers = false, HighlightMatches = false, ShowCallBoard = false });
        var shown = new PlayerTestKit();

        hidden.Session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Single<BallCalled>().Audience.ShouldBe(Audience.Host | Audience.Display);
        shown.Session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Single<BallCalled>().Audience.ShouldBe(Audience.Everyone);
    }

    [Fact]
    public void Wins_and_removals_reach_everyone()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinHighlight = false });

        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Audience.ShouldBe(Audience.Everyone);
        kit.Session.RemoveParticipant(kit.Player, Now).Single<ParticipantRemoved>().Audience.ShouldBe(Audience.Everyone);
    }

    [Fact]
    public void The_auto_manual_switch_is_the_hosts_business()
    {
        var session = InState(SessionState.Manual);

        session.StartAutoCalling(new SeededRandom(1), Now).Value.OfType<AutoCallingStarted>().Single().Audience.ShouldBe(Audience.Host);
        session.Pause(Now).Single<AutoCallingPaused>().Audience.ShouldBe(Audience.Host);
    }

    [Fact]
    public void Every_stream_counts_up_without_gaps_whatever_happens()
    {
        var kit = new PlayerTestKit();
        var random = new SeededRandom(3);
        List<SessionEvent> all = [];
        for (var i = 0; i < 5; i++)
        {
            all.AddRange(kit.Session.DrawBall(DrawSource.Manual, random, Now).Value);
            all.AddRange(kit.Session.ClaimBingo(kit.Player, 1, Now).Value);
        }

        all.AddRange(kit.Session.ShowLeaderboard(true, Now).Value);

        foreach (var stream in new[] { Audience.Players, Audience.Host, Audience.Display })
        {
            var seqs = all.Where(e => e.Audience.HasFlag(stream)).Select(e => e.Streams.For(stream)).ToList();
            seqs.ShouldBe(Enumerable.Range((int)seqs[0], seqs.Count).Select(n => (long)n), $"{stream} counts 1, 2, 3…");
            all.Where(e => !e.Audience.HasFlag(stream)).ShouldAllBe(e => e.Streams.For(stream) == 0);
        }
    }

    [Fact]
    public void Stream_positions_survive_a_restore()
    {
        var kit = new PlayerTestKit();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        Session.Restore(kit.Session.ToSnapshot()).StreamSeqs.ShouldBe(kit.Session.StreamSeqs);
    }
}
