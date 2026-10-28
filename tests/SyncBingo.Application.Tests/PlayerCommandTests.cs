using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>A player's own commands: marks, Undo, auto-fill, redraws and the Bingo button (docs/05 §2).</summary>
public sealed class PlayerCommandTests
{
    private readonly TestApp _app = new();

    [Theory]
    [InlineData(CallerRole.Host)]
    [InlineData(CallerRole.Display)]
    [InlineData(CallerRole.System)]
    public async Task Only_a_player_can_run_player_commands(CallerRole role)
    {
        var code = await _app.NewGameAsync();
        await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        var caller = new Caller(role);
        _app.Publisher.Sent.Clear();

        (await _app.Players.MarkAsync(code, caller, 1, 0, true, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Players.UndoAsync(code, caller, 1, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Players.SetAutoFillAsync(code, caller, true, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Players.RedrawCardAsync(code, caller, 1, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Players.ClaimBingoAsync(code, caller, 1, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        _app.Publisher.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_mark_is_saved_on_the_players_own_card()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        var cell = await CallFirstCellAsync(code, ava);

        var marks = (await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, cell, true, TestApp.Ct)).Value;

        (await _app.PlayerAsync(code, ava)).Cards[0].Marks.ShouldBe(marks);
        (await _app.PlayerAsync(code, ava)).Cards[0].IsMarked(cell).ShouldBeTrue();
    }

    [Fact]
    public async Task A_player_cannot_reach_anyone_elses_card()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");
        var ben = await _app.JoinAsync(code, "Ben");
        await _app.StartRoundAsync(code);
        var bensMarks = (await _app.PlayerAsync(code, ben)).Cards[0].Marks;

        // Card ids only ever name the caller's own cards: Ava's card 1 is not Ben's card 1.
        (await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, 0, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Players.MarkAsync(code, Caller.Participant(ava), 2, 0, true, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Invalid);

        (await _app.PlayerAsync(code, ben)).Cards[0].Marks.ShouldBe(bensMarks);
    }

    [Fact]
    public async Task A_refused_mark_is_not_saved()
    {
        var (code, ava) = await RoundWithPlayerAsync(SessionSettings.Default with { FreeMarking = false });
        var before = (await _app.PlayerAsync(code, ava)).Cards[0].Marks;

        var refused = await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, 0, true, TestApp.Ct);

        refused.Error!.Message.ShouldBe("Only called squares can be marked in this game.");
        (await _app.PlayerAsync(code, ava)).Cards[0].Marks.ShouldBe(before);
    }

    [Fact]
    public async Task Unmark_and_undo_go_through_the_players_card()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        var player = Caller.Participant(ava);
        var start = (await _app.PlayerAsync(code, ava)).Cards[0].Marks;
        (await _app.Players.MarkAsync(code, player, 1, 0, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Players.MarkAsync(code, player, 1, 1, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Players.MarkAsync(code, player, 1, 1, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Players.UndoAsync(code, player, 1, TestApp.Ct)).Value.ShouldBe(start);

        (await _app.PlayerAsync(code, ava)).Cards[0].Marks.ShouldBe(start);
    }

    [Fact]
    public async Task Auto_fill_can_be_switched_on_only_when_the_host_offers_it()
    {
        var code = await _app.NewGameAsync();
        var ava = await _app.JoinAsync(code, "Ava");

        var refused = await _app.Players.SetAutoFillAsync(code, Caller.Participant(ava), true, TestApp.Ct);

        refused.Error!.Message.ShouldBe("The host has not made auto-fill available.");
        (await _app.PlayerAsync(code, ava)).AutoFillOn.ShouldBeFalse();
    }

    [Fact]
    public async Task A_mark_made_during_a_draw_survives_that_draws_auto_fill()
    {
        var (code, ava) = await RoundWithPlayerAsync(SessionSettings.Default with { AutoFillAvailable = true });
        var player = Caller.Participant(ava);
        (await _app.Players.SetAutoFillAsync(code, player, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var card = (await _app.PlayerAsync(code, ava)).Cards[0].Card;
        _app.Draws.Next.Enqueue(card.Cells[0]);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        // The player marks cell 1 after the next draw loaded them but before it saves the fill.
        _app.Store.BeforeNextParticipantUpdate = () => _app.Players.MarkAsync(code, player, 1, 1, true, TestApp.Ct);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var marks = (await _app.PlayerAsync(code, ava)).Cards[0];
        marks.IsMarked(0).ShouldBeTrue("auto-fill marked the previous ball");
        marks.IsMarked(1).ShouldBeTrue("the player's own mark was not overwritten");
    }

    [Fact]
    public async Task An_auto_fill_during_a_players_mark_survives_that_mark()
    {
        var (code, ava) = await RoundWithPlayerAsync(SessionSettings.Default with { AutoFillAvailable = true });
        var player = Caller.Participant(ava);
        (await _app.Players.SetAutoFillAsync(code, player, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var card = (await _app.PlayerAsync(code, ava)).Cards[0].Card;
        _app.Draws.Next.Enqueue(card.Cells[0]);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        // The next ball (and its auto-fill of cell 0) lands between the player's load and save.
        _app.Store.BeforeNextParticipantUpdate = () => _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct);
        (await _app.Players.MarkAsync(code, player, 1, 1, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var marks = (await _app.PlayerAsync(code, ava)).Cards[0];
        marks.IsMarked(0).ShouldBeTrue("the auto-fill was not overwritten");
        marks.IsMarked(1).ShouldBeTrue();
    }

    [Fact]
    public async Task A_mark_that_races_clear_cards_is_judged_again_and_refused()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        _app.Store.BeforeNextParticipantUpdate = () => _app.Round.ClearCardsAsync(code, Caller.Host, false, TestApp.Ct);

        var mark = await _app.Players.MarkAsync(code, Caller.Participant(ava), 1, 0, true, TestApp.Ct);

        mark.Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        (await _app.PlayerAsync(code, ava)).Cards[0].IsMarked(0).ShouldBeFalse("the cleared card stays cleared");
    }

    [Fact]
    public async Task A_player_who_keeps_losing_the_race_is_asked_to_try_again()
    {
        var (code, ava) = await RoundWithPlayerAsync(SessionSettings.Default with { AutoFillAvailable = true });
        var player = Caller.Participant(ava);
        var interferences = 0;
        async Task Interfere()
        {
            interferences++;
            (await _app.Players.SetAutoFillAsync(code, player, interferences % 2 == 0, TestApp.Ct)).IsSuccess.ShouldBeTrue();
            _app.Store.BeforeNextParticipantUpdate = Interfere;
        }

        _app.Store.BeforeNextParticipantUpdate = Interfere;

        var mark = await _app.Players.MarkAsync(code, player, 1, 0, true, TestApp.Ct);

        _app.Store.BeforeNextParticipantUpdate = null;
        mark.Error!.Code.ShouldBe(DomainErrorCode.Conflict);
        interferences.ShouldBe(5, "one lost race per try");
    }

    [Fact]
    public async Task A_redraw_swaps_in_a_new_unique_card_and_releases_the_old_one()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        var ava = await _app.JoinAsync(code, "Ava");
        var old = (await _app.PlayerAsync(code, ava)).Cards[0].Card;

        var shown = (await _app.Players.RedrawCardAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).Value;

        var card = (await _app.PlayerAsync(code, ava)).Cards[0].Card;
        card.ShouldNotBe(old);
        shown.Cells.ShouldBe(card.Cells, "with numbers, the player sees the numbers");
        _app.Store.CardsInPlay(code).ShouldBe([card.Fingerprint]);
        _app.Publisher.Notifications.ShouldContain(n => n.Recipient == new Recipient.Participant(ava) && n.Notification == shown);
    }

    [Fact]
    public async Task A_redraw_in_a_custom_set_game_shows_the_player_placeholders_never_item_numbers()
    {
        // Item numbers with the board's ball text would pair every clue with its word: the answer key.
        const string Words = """
            { "format": 1, "name": "Words", "pairs": [ ["a"], ["b"], ["c"], ["d"], ["e"], ["f"], ["g"], ["h"], ["i"], ["j"] ] }
            """;
        var code = await _app.NewGameAsync(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        (await _app.Content.UseAsync(code, Caller.Host, Words, CardSize.ThreeByThree, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var ava = await _app.JoinAsync(code, "Ava");

        var shown = (await _app.Players.RedrawCardAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).Value;

        shown.Cells.ShouldBe([1, 2, 3, 4, 0, 6, 7, 8, 9], "cell positions, with the free space as 0");
    }

    [Fact]
    public async Task A_refused_redraw_claims_no_card()
    {
        var code = await _app.NewGameAsync(); // the host controls redraws
        var ava = await _app.JoinAsync(code, "Ava");
        var inPlay = _app.Store.CardsInPlay(code);
        var claims = _app.Store.CardClaims;

        var refused = await _app.Players.RedrawCardAsync(code, Caller.Participant(ava), 1, TestApp.Ct);

        refused.Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        _app.Store.CardsInPlay(code).ShouldBe(inPlay);
        _app.Store.CardClaims.ShouldBe(claims, "the check comes before any card is dealt");
    }

    [Fact]
    public async Task Cards_are_locked_once_the_countdown_starts()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        var ava = await _app.JoinAsync(code, "Ava");
        (await _app.Round.StartCountdownAsync(code, Caller.Host, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var refused = await _app.Players.RedrawCardAsync(code, Caller.Participant(ava), 1, TestApp.Ct);

        refused.Error!.Message.ShouldBe("Cards are locked once the round countdown starts.");
    }

    [Fact]
    public async Task Every_bingo_press_reaches_the_host()
    {
        var (code, ava) = await RoundWithPlayerAsync();
        _app.Publisher.Sent.Clear();

        (await _app.Players.ClaimBingoAsync(code, Caller.Participant(ava), 1, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var claim = _app.Publisher.Events.ShouldHaveSingleItem().ShouldBeOfType<WinClaimed>().Claim;
        claim.ParticipantId.ShouldBe(ava);
        claim.Qualified.ShouldBeFalse("nothing has been called; the host still hears about it");
        (await _app.SessionAsync(code)).Claims.ShouldHaveSingleItem();
    }

    private async Task<(GameCode Code, string Ava)> RoundWithPlayerAsync(SessionSettings? settings = null)
    {
        var code = await _app.NewGameAsync(settings);
        var ava = await _app.JoinAsync(code, "Ava");
        await _app.StartRoundAsync(code);
        return (code, ava);
    }

    /// <summary>Calls the number in the player's first cell and returns that cell.</summary>
    private async Task<int> CallFirstCellAsync(GameCode code, string participantId)
    {
        _app.Draws.Next.Enqueue((await _app.PlayerAsync(code, participantId)).Cards[0].Card.Cells[0]);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        return 0;
    }
}
