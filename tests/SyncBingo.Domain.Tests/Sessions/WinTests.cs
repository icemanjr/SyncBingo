using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>Claims (FR-37a), detection (FR-19), awards (ADR 0008), the leaderboard and removal.</summary>
public sealed class WinTests
{
    private static readonly int[] TopRow = [1, 16, 31, 46, 61];

    /// <summary>A second card sharing no numbers with <see cref="PlayerTestKit.KnownCard"/>.</summary>
    private static readonly Card OtherCard = new(Enumerable.Range(0, 25).Select(i => i == 12 ? 0 : KnownCard.Cells[i] + 5));

    [Fact]
    public void Every_bingo_press_reaches_the_host_valid_or_not()
    {
        var kit = new PlayerTestKit();
        kit.Call(2);

        var early = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;
        CallAll(kit, TopRow);
        var real = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;

        early.Qualified.ShouldBeFalse();
        real.Qualified.ShouldBeTrue();
        kit.Session.Claims.Select(claim => (claim.Id, claim.Status)).ShouldBe([(1, ClaimStatus.Pending), (2, ClaimStatus.Pending)]);
        real.Name.ShouldBe("Sam");
        real.WinConditionId.ShouldBe(Normal);
    }

    [Fact]
    public void Claims_need_a_round()
    {
        var kit = new PlayerTestKit();
        kit.Session.EndRound(Now).IsSuccess.ShouldBeTrue();
        kit.Session.ClaimBingo(kit.Player, 1, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        kit.Session.Claims.ShouldBeEmpty();
    }

    [Fact]
    public void The_host_can_dismiss_a_pending_claim_once()
    {
        var kit = new PlayerTestKit();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        kit.Session.DismissClaim(1, Now).Single<ClaimDismissed>().ClaimId.ShouldBe(1);

        kit.Session.Claims[0].Status.ShouldBe(ClaimStatus.Dismissed);
        kit.Session.DismissClaim(1, Now).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        kit.Session.DismissClaim(9, Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public void Awarding_a_claim_records_the_win_and_closes_the_claim()
    {
        var kit = new PlayerTestKit();
        CallAll(kit, TopRow);
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        var win = kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Win;

        win.ShouldBe(new WinRecord(1, "p1", "Sam", 1, Normal, Now, WinSource.Claimed));
        kit.Session.Claims[0].Status.ShouldBe(ClaimStatus.Awarded);
        kit.Session.Rounds[0].Wins.ShouldBe([win]);
    }

    [Fact]
    public void Awarding_twice_for_the_same_condition_changes_nothing()
    {
        var kit = new PlayerTestKit();
        kit.Session.AwardWin(kit.Player, null, Now).IsSuccess.ShouldBeTrue();
        var version = kit.Session.Version;

        kit.Session.AwardWin(kit.Player, null, Now).Value.ShouldBeEmpty();

        kit.Session.Wins.Count.ShouldBe(1);
        kit.Session.Version.ShouldBe(version);
    }

    [Fact]
    public void A_player_can_win_again_after_the_host_changes_the_condition()
    {
        var kit = new PlayerTestKit();
        kit.Session.AwardWin(kit.Player, null, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ChangePattern("blackout", Now).IsSuccess.ShouldBeTrue();

        kit.Session.AwardWin(kit.Player, null, Now).Single<WinAwarded>().Win.WinConditionId.ShouldBe("blackout");

        kit.Session.Rounds[0].WinConditionsUsed.ShouldBe([Normal, "blackout"]);
        kit.Session.Leaderboard().ShouldBe([new LeaderboardEntry("p1", "Sam", 2)]);
    }

    [Fact]
    public void The_host_can_award_a_shouted_bingo_without_any_alert()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinDetection = WinDetection.Off });

        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Win.Source.ShouldBe(WinSource.Manual);
        kit.Session.Claims.ShouldBeEmpty("nobody pressed Bingo; the host heard a shout");
    }

    [Fact]
    public void Detection_alerts_once_per_card_on_called_numbers_not_marks()
    {
        var kit = new PlayerTestKit();
        CallAll(kit, TopRow);

        var detected = kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>();

        detected.ShouldBe(new WinDetected("p1", "Sam", 1, Normal, Matches: 1, Completed: detected.Completed) { Seq = detected.Seq, Audience = Audience.Host, Streams = detected.Streams });
        detected.Completed.ShouldBe(["rows"], "the host sees what is complete");
        kit.Marks.ShouldBe(Bits(Free), "the player never marked anything");
        kit.Call(2);
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty("no repeat alerts");
        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Win.Source.ShouldBe(WinSource.Detected);
    }

    [Fact]
    public void Mismarks_never_trigger_detection()
    {
        var kit = new PlayerTestKit();
        foreach (var cell in new[] { 0, 1, 2, 3, 4 })
        {
            kit.Mark(cell).IsSuccess.ShouldBeTrue();
        }

        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty();
    }

    [Fact]
    public void A_new_condition_or_a_new_round_can_alert_again()
    {
        var kit = new PlayerTestKit();
        CallAll(kit, TopRow);
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldNotBeEmpty();

        kit.Session.ChangePattern("corners", Now).IsSuccess.ShouldBeTrue();
        CallAll(kit, [5, 65]); // bottom corners; the top two came with the top row
        kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>().WinConditionId.ShouldBe("corners");
    }

    [Fact]
    public void Detection_off_raises_nothing()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinDetection = WinDetection.Off });
        CallAll(kit, TopRow);

        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty();
    }

    [Fact]
    public void Alert_and_pause_switches_auto_mode_back_to_manual()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinDetection = WinDetection.AlertHostAndPause });
        CallAll(kit, [1, 16, 31, 46]);
        kit.Session.StartAutoCalling(kit.Picks(61), Now).IsSuccess.ShouldBeTrue(); // completes the top row

        var events = kit.Session.DetectWins([kit.Player], Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(WinDetected), typeof(AutoCallingPaused)]);
        kit.Session.State.ShouldBe(SessionState.Manual);
    }

    [Fact]
    public void A_plain_alert_never_pauses_auto_mode()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinDetection = WinDetection.AlertHost });
        CallAll(kit, [1, 16, 31, 46]);
        kit.Session.StartAutoCalling(kit.Picks(61), Now).IsSuccess.ShouldBeTrue();

        kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>();

        kit.Session.State.ShouldBe(SessionState.Auto, "FR-31: the game never stops by itself unless the host chose to");
    }

    [Fact]
    public void Alert_and_pause_in_manual_mode_just_alerts()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { WinDetection = WinDetection.AlertHostAndPause });
        CallAll(kit, TopRow);

        kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>();
        kit.Session.State.ShouldBe(SessionState.Manual);
    }

    [Fact]
    public void Detection_checks_every_player()
    {
        var kit = new PlayerTestKit();
        var ava = Participant.Join("p2", Name("Ava"), OtherCard, kit.Session, Now);
        CallAll(kit, TopRow);

        kit.Session.DetectWins([kit.Player, ava], Now).Value.OfType<WinDetected>().Select(d => d.Name).ShouldBe(["Sam"]);
    }

    [Fact]
    public void The_leaderboard_ranks_by_wins_across_rounds()
    {
        var kit = new PlayerTestKit();
        var ava = Participant.Join("p2", Name("Ava"), OtherCard, kit.Session, Now);
        kit.Session.AwardWin(ava, null, Now).IsSuccess.ShouldBeTrue();
        kit.Session.NextRound(Now);
        kit.Session.AwardWin(ava, null, Now).IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, null, Now).IsSuccess.ShouldBeTrue();

        kit.Session.Leaderboard().ShouldBe([new LeaderboardEntry("p2", "Ava", 2), new LeaderboardEntry("p1", "Sam", 1)]);
        kit.Session.Rounds.Select(round => (round.Number, round.Wins.Count, round.EndedAt is not null))
            .ShouldBe([(1, 1, true), (2, 2, false)]);
    }

    [Fact]
    public void The_host_decides_when_everyone_sees_the_leaderboard()
    {
        var kit = new PlayerTestKit();

        kit.Session.ShowLeaderboard(true, Now).Single<LeaderboardVisibilityChanged>().Visible.ShouldBeTrue();
        kit.Session.LeaderboardVisible.ShouldBeTrue();
    }

    [Fact]
    public void Removing_a_false_bingo_caller_dismisses_their_pending_claims()
    {
        var kit = new PlayerTestKit();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        var events = kit.Session.RemoveParticipant(kit.Player, Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(ClaimDismissed), typeof(ClaimDismissed), typeof(ParticipantRemoved)]);
        kit.Session.Claims.ShouldAllBe(claim => claim.Status == ClaimStatus.Dismissed);
    }

    [Fact]
    public void A_player_who_leaves_is_announced_as_leaving_and_their_claims_close()
    {
        var kit = new PlayerTestKit();
        kit.Session.ClaimBingo(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        var events = kit.Session.Leave(kit.Player, Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(ClaimDismissed), typeof(ParticipantLeft)]);
        events.OfType<ParticipantLeft>().Single().Audience.ShouldBe(Audience.Everyone, "\"Ava left the game\"");
        kit.Session.Claims.ShouldAllBe(claim => claim.Status == ClaimStatus.Dismissed);
    }

    [Fact]
    public void With_several_patterns_any_of_them_wins_and_a_shape_alone_is_announced_by_name()
    {
        var kit = new PlayerTestKit();
        kit.Session.ChangePattern("normal+corners", Now).IsSuccess.ShouldBeTrue();
        CallAll(kit, [1, 61, 5, 65]); // the four corners, and no line

        var claim = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;
        claim.Matches.ShouldBe(1);
        claim.Completed.ShouldBe(["corners"]);

        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Win.Announced.ShouldBe("corners", "\"Sam had Four Corners!\"");
    }

    [Fact]
    public void A_line_win_is_announced_as_a_Bingo_even_with_shapes_selected()
    {
        var kit = new PlayerTestKit();
        kit.Session.ChangePattern("rows+corners", Now).IsSuccess.ShouldBeTrue();
        CallAll(kit, TopRow);

        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>().Win.Announced.ShouldBeNull("\"Sam had a Bingo!\"");
    }

    [Fact]
    public void Once_per_pattern_counts_the_whole_selection_as_one()
    {
        var kit = new PlayerTestKit();
        kit.Session.ChangePattern("rows+corners", Now).IsSuccess.ShouldBeTrue();
        CallAll(kit, TopRow);
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        CallAll(kit, [5, 65]); // the corners now complete too

        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.AlreadyWon.ShouldBeTrue("one win per selection");
        kit.Session.ChangePattern("blackout", Now).IsSuccess.ShouldBeTrue();
        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.AlreadyWon.ShouldBeFalse("a new selection allows another win");
    }

    private static void CallAll(PlayerTestKit kit, IEnumerable<int> balls)
    {
        foreach (var ball in balls)
        {
            kit.Call(ball);
        }
    }
}
