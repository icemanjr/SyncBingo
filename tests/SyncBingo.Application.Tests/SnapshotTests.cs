using SyncBingo.Application.Queries;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>What each caller may see of a game (docs/05 §2): decided in one place, tested here.</summary>
public sealed class SnapshotTests
{
    private readonly TestApp _app = new();

    private GameQueries Queries => new(_app.Store);

    [Fact]
    public async Task A_player_sees_only_their_own_card()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.JoinAsync(code, "Ben");

        var snapshot = (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value;

        snapshot.You!.ParticipantId.ShouldBe(ava);
        snapshot.You.Cards.ShouldHaveSingleItem().Cells.ShouldBe((await _app.PlayerAsync(code, ava)).Cards[0].Card.Cells);
        snapshot.Host.ShouldBeNull("no player list, claims or host settings for players");
    }

    [Fact]
    public async Task Players_see_the_calls_only_if_a_setting_shows_them()
    {
        var hidden = SessionSettings.Default with { ShowBallToPlayers = false, HighlightMatches = false, ShowCallBoard = false };
        var code = await _app.NewGameAsync(hidden);
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value.Game.Calls.ShouldBeNull("serious mode: listen to the host");
        (await Queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Game.Calls!.Count.ShouldBe(1);
        (await Queries.SnapshotAsync(code, new Caller(CallerRole.Display), TestApp.Ct)).Value.Game.Calls!.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Any_display_setting_shows_players_the_calls(bool ball, bool highlight, bool board)
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { ShowBallToPlayers = ball, HighlightMatches = highlight, ShowCallBoard = board });
        var ava = await _app.JoinAsync(code, "Ava");

        (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value.Game.Calls.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_leaderboard_reaches_players_only_when_the_host_shows_it()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");

        (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value.Game.Leaderboard.ShouldBeNull();
        (await Queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Game.Leaderboard.ShouldNotBeNull();

        (await _app.Host.ShowLeaderboardAsync(code, Caller.Host, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value.Game.Leaderboard.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_host_sees_the_players_and_only_open_claims()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        var ben = await _app.JoinAsync(code, "Ben");
        await _app.StartRoundAsync(code);
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ben), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Manage.DismissClaimAsync(code, Caller.Host, 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var host = (await Queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Host!;

        host.Participants.Select(p => p.Name).ShouldBe(["Ava", "Ben"]);
        host.PendingClaims.ShouldHaveSingleItem().ParticipantId.ShouldBe(ben);
        host.Settings.ShouldBe(SessionSettings.Default);
    }

    [Fact]
    public async Task Patterns_and_the_board_come_as_data()
    {
        var code = await _app.NewGameAsync();

        var game = (await Queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Game;

        game.Patterns.Select(p => p.Id).ShouldBe(["normal", "corners", "B", "I", "N", "G", "O", "blackout"]);
        game.Patterns[0].Masks.Count.ShouldBe(12, "every row, column and diagonal");
        game.Patterns[1].Masks.ShouldBe([(1u << 0) | (1u << 4) | (1u << 20) | (1u << 24)]);
        game.Card.ShouldBe(new CardShape(5, 5, 12));
        game.Board.Select(g => g.Label).ShouldBe(["B", "I", "N", "G", "O"]);
        game.Board[4].Balls.ShouldBe(Enumerable.Range(61, 15));
    }

    [Fact]
    public async Task A_removed_player_sees_nothing()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        (await _app.Manage.RemoveParticipantAsync(code, Caller.Host, ava, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Error!.Message.ShouldBe("You're no longer in this game.");
    }

    [Fact]
    public async Task The_host_inspects_a_card_with_its_marks()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        var marks = (await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, 0, true, TestApp.Ct)).Value;

        var card = (await Queries.InspectAsync(code, Caller.Host, ava, TestApp.Ct)).Value.Cards.Single();

        card.Marks.ShouldBe(marks);
        (await Queries.InspectAsync(code, Caller.Host, "nobody", TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task Only_the_host_inspects_cards_or_reads_the_leaderboard()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        var ben = Caller.Participant(await _app.JoinAsync(code, "Ben"));

        (await Queries.InspectAsync(code, ben, ava, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await Queries.LeaderboardAsync(code, ben, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await Queries.LeaderboardAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Each_caller_gets_their_own_streams_position()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue(); // host only

        var host = (await Queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Game.Seq;
        var player = (await Queries.SnapshotAsync(code, Caller.Participant(ava), TestApp.Ct)).Value.Game.Seq;

        host.ShouldBe(player + 1, "the claim is in the host's stream only");
    }

    [Fact]
    public async Task An_unknown_game_is_not_found()
    {
        (await Queries.SnapshotAsync(GameCode.Parse("9ve4-shp3-k7mw-a2qx"), Caller.Host, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }
}
