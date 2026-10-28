using SyncBingo.Application.Players;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>The host's commands about players: awards, dismissals and removal (FR-7, ADR 0008).</summary>
public sealed class PlayerManagementTests
{
    private readonly TestApp _app = new();

    [Theory]
    [InlineData(CallerRole.Participant)]
    [InlineData(CallerRole.Display)]
    public async Task Only_the_host_can_award_dismiss_or_remove(CallerRole role)
    {
        var (code, ava) = await RoundWithPlayerAsync();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var caller = new Caller(role, role == CallerRole.Participant ? ava : null);
        _app.Publisher.Sent.Clear();

        (await _app.Manage.AwardWinAsync(code, caller, new AwardWin(ava), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Manage.DismissClaimAsync(code, caller, 1, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Manage.RemoveParticipantAsync(code, caller, ava, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        _app.Publisher.Sent.ShouldBeEmpty();
        (await _app.SessionAsync(code)).Wins.ShouldBeEmpty();
    }

    [Fact]
    public async Task Awarding_a_claim_credits_the_win_and_closes_the_claim()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Manage.AwardWinAsync(code, Caller.Host, new AwardWin(ava, ClaimId: 1), TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var session = await _app.SessionAsync(code);
        session.Wins.ShouldHaveSingleItem().Source.ShouldBe(WinSource.Claimed);
        session.Claims.ShouldHaveSingleItem().Status.ShouldBe(ClaimStatus.Awarded);
        _app.Publisher.Events.OfType<WinAwarded>().ShouldHaveSingleItem().Win.ParticipantId.ShouldBe(ava);
    }

    [Fact]
    public async Task Everyone_hears_about_wins_and_removals()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { WinHighlight = false });
        var ava = await _app.JoinAsync(code, "Ava");
        var ben = await _app.JoinAsync(code, "Ben");
        await _app.StartRoundAsync(code);

        (await _app.Manage.AwardWinAsync(code, Caller.Host, new AwardWin(ava), TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ben, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Publisher.Events.OfType<WinAwarded>().Single().Audience.ShouldBe(Audience.Everyone, "\"Ava had Bingo!\"");
        _app.Publisher.Events.OfType<ParticipantRemoved>().Single().Audience.ShouldBe(Audience.Everyone, "\"Ben was removed from the game\"");
    }

    [Fact]
    public async Task A_player_can_leave_and_their_name_is_free_again()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");

        (await _app.Players.LeaveAsync(code, Caller.Participant(ava), TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Publisher.Events.OfType<ParticipantLeft>().Single().Name.ShouldBe("Ava");
        (await _app.Players.LeaveAsync(code, Caller.Participant(ava), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound, "already gone");
        await _app.JoinAsync(code, "Ava"); // the name is free for someone else, or for Ava between rounds
    }

    [Fact]
    public async Task Only_a_player_can_leave()
    {
        var code = await _app.NewGameAsync();

        (await _app.Players.LeaveAsync(code, Caller.Host, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }

    [Fact]
    public async Task Awarding_the_same_claim_twice_counts_once()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var award = new AwardWin(ava, ClaimId: 1);

        (await _app.Manage.AwardWinAsync(code, Caller.Host, award, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Manage.AwardWinAsync(code, Caller.Host, award, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).Leaderboard().ShouldHaveSingleItem().Wins.ShouldBe(1);
    }

    [Fact]
    public async Task Awarding_an_unknown_player_is_not_found()
    {
        var (code, _) = await RoundWithPlayerAsync();

        var result = await _app.Manage.AwardWinAsync(code, Caller.Host, new AwardWin("nobody"), TestApp.Ct);

        result.Error!.Message.ShouldBe("That player is not in this game.");
    }

    [Fact]
    public async Task Dismissing_a_claim_tells_the_host()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Manage.DismissClaimAsync(code, Caller.Host, 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).Claims.ShouldHaveSingleItem().Status.ShouldBe(ClaimStatus.Dismissed);
        _app.Publisher.Events.OfType<ClaimDismissed>().ShouldHaveSingleItem().ClaimId.ShouldBe(1);
    }

    [Fact]
    public async Task Removing_a_player_deletes_them_and_releases_their_name_card_and_tokens()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        var ben = await _app.JoinAsync(code, "Ben");
        var bensCard = (await _app.PlayerAsync(code, ben)).Cards[0].Card.Fingerprint;

        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Store.GetParticipantAsync(code, ava, TestApp.Ct)).ShouldBeNull();
        _app.Store.Tokens(code).ShouldNotContain(ava, "their token no longer signs anyone in");
        _app.Store.ReservedNames(code).ShouldBe(["ben"]);
        _app.Store.CardsInPlay(code).ShouldBe([bensCard]);
        _app.Publisher.Events.OfType<ParticipantRemoved>().ShouldHaveSingleItem().ParticipantId.ShouldBe(ava);
    }

    [Fact]
    public async Task A_removed_players_pending_claims_are_dismissed()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).Claims.ShouldHaveSingleItem().Status.ShouldBe(ClaimStatus.Dismissed);
    }

    [Fact]
    public async Task A_removed_player_can_do_nothing_more()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        var player = Caller.Participant(ava);
        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Players.MarkAsync(code, player, 1, 0, true, TestApp.Ct)).Error!.Message.ShouldBe("You are no longer in this game.");
        (await _app.Players.ClaimBingoAsync(code, player, 1, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task A_mark_racing_the_removal_is_not_saved()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        _app.Store.BeforeNextParticipantUpdate = () => _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct);

        var mark = await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, 0, true, TestApp.Ct);

        mark.Error!.Code.ShouldBe(DomainErrorCode.NotFound);
        (await _app.Store.GetParticipantAsync(code, ava, TestApp.Ct)).ShouldBeNull("the mark did not bring the player back");
    }

    [Fact]
    public async Task The_name_of_a_removed_player_can_be_used_again()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "ava"), TestApp.Ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task<(GameCode Code, string Ava)> RoundWithPlayerAsync()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        return (code, ava);
    }
}
