using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>
/// How many wins a card can earn in a round (FR-43). The known card (see
/// <see cref="PlayerTestKit"/>) completes the top row <b>and</b> column B when ball 1
/// is called last: a "double Bingo".
/// </summary>
public sealed class MultipleWinsTests
{
    private static readonly int[] RestOfTopRowAndColumnB = [16, 31, 46, 61, 2, 3, 4, 5];

    [Fact]
    public void One_ball_completing_two_lines_is_reported_as_two()
    {
        var kit = DoubleBingo(MultipleWins.OncePerPattern);

        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.Matches.ShouldBe(2);
        kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>().Matches.ShouldBe(2);
    }

    [Fact]
    public void Once_per_pattern_awards_one_win_until_the_pattern_changes()
    {
        var kit = DoubleBingo(MultipleWins.OncePerPattern);

        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, 1, Now).Value.ShouldBeEmpty("a second award for the same pattern changes nothing");
        kit.Session.ChangePattern("corners", Now).IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, 1, Now).Single<WinAwarded>();

        kit.Session.Leaderboard().Single().Wins.ShouldBe(2);
    }

    [Fact]
    public void Once_per_round_closes_the_card_until_the_cards_are_cleared()
    {
        var kit = DoubleBingo(MultipleWins.OncePerRound);
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ChangePattern("corners", Now).IsSuccess.ShouldBeTrue();

        kit.Session.AwardWin(kit.Player, 1, Now).Error!.Message.ShouldContain("already won this round");
        kit.Call(65); // with 1, 61 and 5 called, Four Corners is complete
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty("a card that has won is not checked again");

        kit.Session.NextRound(Now);
        kit.Player.ResetForNewRound(kit.Session, deal: null);
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue("a new round opens the card again");
    }

    [Fact]
    public void Every_declared_win_awards_what_the_player_declares()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);

        kit.Session.AwardWin(kit.Player, 1, Now, wins: 2).Single<WinAwarded>().Win.Wins.ShouldBe(2);

        kit.Session.Leaderboard().Single().Wins.ShouldBe(2);
    }

    [Fact]
    public void Every_declared_win_awards_later_lines_in_the_same_round_too()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue(); // "I got column B!"
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldNotBeEmpty();
        foreach (var ball in new[] { 17, 32, 47, 62 }) // second row
        {
            kit.Call(ball);
        }

        kit.Session.DetectWins([kit.Player], Now).Single<WinDetected>().Matches.ShouldBe(3, "alerts again when more lines complete");
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        kit.Session.Leaderboard().Single().Wins.ShouldBe(2);
    }

    [Fact]
    public void A_second_line_noticed_later_can_be_claimed_and_awarded()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);
        var first = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim; // "Column B!"
        kit.Session.AwardWin(kit.Player, 1, Now, claimId: first.Id).IsSuccess.ShouldBeTrue();
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldNotBeEmpty();
        kit.Call(6);
        kit.Call(7);
        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty("no new line, so no new alert");

        var second = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim; // "...and the top row!"

        (second.Matches, second.AlreadyAwarded, second.Awardable).ShouldBe((2, 1, 1), "the host sees 2 lines, 1 already paid, 1 to award");
        kit.Session.AwardWin(kit.Player, 1, Now, claimId: second.Id).IsSuccess.ShouldBeTrue();
        kit.Session.Leaderboard().Single().Wins.ShouldBe(2);
    }

    [Fact]
    public void A_press_after_every_line_is_paid_has_no_awardable_matches()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);
        var doubleBingo = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;
        doubleBingo.Awardable.ShouldBe(2);
        kit.Session.AwardWin(kit.Player, 1, Now, wins: 2, claimId: doubleBingo.Id).IsSuccess.ShouldBeTrue();
        kit.Call(6);
        kit.Call(7);

        var again = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;

        (again.Qualified, again.Matches, again.AlreadyAwarded, again.Awardable).ShouldBe((true, 2, 2, 0));
    }

    [Theory]
    [InlineData(MultipleWins.OncePerPattern)]
    [InlineData(MultipleWins.OncePerRound)]
    public void In_the_once_modes_a_card_that_won_has_nothing_more_to_award(MultipleWins mode)
    {
        var kit = DoubleBingo(mode);
        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.Awardable.ShouldBe(1, "two lines, but one win");
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();

        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.Awardable.ShouldBe(0);
    }

    [Fact]
    public void Once_per_round_still_has_nothing_to_award_after_the_pattern_changes()
    {
        var kit = DoubleBingo(MultipleWins.OncePerRound);
        kit.Session.AwardWin(kit.Player, 1, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ChangePattern("corners", Now).IsSuccess.ShouldBeTrue();
        kit.Call(65);

        kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim.Awardable.ShouldBe(0);
    }

    [Fact]
    public void Awarding_the_same_claim_twice_counts_once()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);
        var claim = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;

        kit.Session.AwardWin(kit.Player, 1, Now, wins: 2, claimId: claim.Id).IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, 1, Now, wins: 2, claimId: claim.Id).Value.ShouldBeEmpty("a double click");

        kit.Session.Leaderboard().Single().Wins.ShouldBe(2);
    }

    [Fact]
    public void Several_wins_in_one_award_need_every_declared_win()
    {
        var kit = DoubleBingo(MultipleWins.OncePerPattern);

        kit.Session.AwardWin(kit.Player, 1, Now, wins: 2).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        kit.Session.AwardWin(kit.Player, 1, Now, wins: 0).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public void A_dismissed_or_someone_elses_claim_cannot_be_awarded()
    {
        var kit = DoubleBingo(MultipleWins.EveryDeclaredWin);
        var claim = kit.Session.ClaimBingo(kit.Player, 1, Now).Single<WinClaimed>().Claim;
        kit.Session.DismissClaim(claim.Id, Now).IsSuccess.ShouldBeTrue();

        kit.Session.AwardWin(kit.Player, 1, Now, claimId: claim.Id).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        kit.Session.AwardWin(kit.Player, 1, Now, claimId: 99).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public void The_setting_changes_only_between_rounds()
    {
        var kit = DoubleBingo(MultipleWins.OncePerPattern);

        kit.Session.ChangeSettings(SessionSettings.Default with { MultipleWins = MultipleWins.EveryDeclaredWin }, Now).IsSuccess.ShouldBeTrue();

        kit.Session.Settings.MultipleWins.ShouldBe(MultipleWins.OncePerPattern);
        kit.Session.NextRoundSettings!.MultipleWins.ShouldBe(MultipleWins.EveryDeclaredWin);
    }

    /// <summary>Top row and column B both complete when ball 1 is called last.</summary>
    private static PlayerTestKit DoubleBingo(MultipleWins mode)
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { MultipleWins = mode });
        foreach (var ball in RestOfTopRowAndColumnB)
        {
            kit.Call(ball);
        }

        kit.Session.DetectWins([kit.Player], Now).Value.ShouldBeEmpty("no line is complete yet");
        kit.Call(1);
        return kit;
    }
}
