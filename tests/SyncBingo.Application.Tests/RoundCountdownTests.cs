using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>The round countdown through the application layer and the scheduler (FR-45).</summary>
public sealed class RoundCountdownTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Start_round_schedules_the_round_start_for_when_the_countdown_ends()
    {
        var code = await _app.NewGameAsync();

        (await _app.Round.StartCountdownAsync(code, Caller.Host, startInAuto: false, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Scheduler.Due[code.Value].ShouldBe(_app.Clock.UtcNow.AddSeconds(10));
        _app.Publisher.Events.ShouldHaveSingleItem().ShouldBeOfType<RoundCountdownStarted>().Round.ShouldBe(1);
    }

    [Fact]
    public async Task Someone_joining_during_the_countdown_gets_a_card_if_it_is_cancelled()
    {
        var code = await _app.NewGameAsync();
        (await _app.Round.StartCountdownAsync(code, Caller.Host, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var late = await _app.JoinAsync(code, "Late");
        (await _app.PlayerAsync(code, late)).IsWaiting.ShouldBeTrue();

        (await _app.Round.CancelCountdownAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.PlayerAsync(code, late)).Cards.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Cancelling_the_countdown_cancels_the_scheduled_start()
    {
        var code = await _app.NewGameAsync();
        (await _app.Round.StartCountdownAsync(code, Caller.Host, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Round.CancelCountdownAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
        (await _app.SessionAsync(code)).State.ShouldBe(SessionState.BetweenRounds);
    }

    [Fact]
    public async Task An_early_tick_changes_nothing()
    {
        var code = await _app.NewGameAsync();
        (await _app.Round.StartCountdownAsync(code, Caller.Host, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        _app.Clock.UtcNow = _app.Clock.UtcNow.AddSeconds(9);
        var published = _app.Publisher.Sent.Count;

        (await _app.Draw.TickAsync(code, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).State.ShouldBe(SessionState.Starting);
        _app.Publisher.Sent.Count.ShouldBe(published);
    }

    [Fact]
    public async Task At_zero_a_manual_round_starts_and_nothing_more_is_scheduled()
    {
        var code = await _app.NewGameAsync();

        await _app.StartRoundAsync(code);

        (await _app.SessionAsync(code)).State.ShouldBe(SessionState.Manual);
        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
    }

    [Fact]
    public async Task At_zero_an_auto_round_draws_its_first_ball_and_schedules_the_next()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { AutoCallInterval = TimeSpan.FromSeconds(6) });

        await _app.StartRoundAsync(code, startInAuto: true);

        (await _app.SessionAsync(code)).Calls.Count.ShouldBe(1);
        _app.Scheduler.Due[code.Value].ShouldBe(_app.Clock.UtcNow.AddSeconds(6));
    }

    [Fact]
    public async Task A_tick_after_a_cancel_changes_nothing()
    {
        var code = await _app.NewGameAsync();
        (await _app.Round.StartCountdownAsync(code, Caller.Host, false, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        (await _app.Round.CancelCountdownAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        _app.Clock.UtcNow = _app.Clock.UtcNow.AddSeconds(10);

        (await _app.Draw.TickAsync(code, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).State.ShouldBe(SessionState.BetweenRounds);
    }

    [Fact]
    public async Task Marks_follow_the_settings_the_round_actually_starts_with()
    {
        var code = await _app.NewGameAsync();
        var id = await _app.JoinAsync(code, "Ava");
        (await _app.Host.ApplyRulesAsync(code, Caller.Host, s => s with { FreeSpace = FreeSpace.Earned }, null, TestApp.Ct))
            .IsSuccess.ShouldBeTrue();

        await _app.StartRoundAsync(code);

        (await _app.PlayerAsync(code, id)).Cards[0].Marks.ShouldBe(0u, "the earned free space starts unmarked");
    }

    [Fact]
    public async Task Taking_the_free_space_away_deals_everyone_a_card_with_a_ball_in_the_centre()
    {
        var code = await _app.NewGameAsync();
        var id = await _app.JoinAsync(code, "Ava");
        (await _app.PlayerAsync(code, id)).Cards[0].Card.Cells[12].ShouldBe(Card.FreeCellNumber);

        (await _app.Host.ApplyRulesAsync(code, Caller.Host, s => s with { FreeSpace = FreeSpace.None }, null, TestApp.Ct))
            .IsSuccess.ShouldBeTrue();

        var card = (await _app.PlayerAsync(code, id)).Cards[0];
        card.Card.Cells.ShouldNotContain(Card.FreeCellNumber);
        card.Marks.ShouldBe(0u);
        _app.Publisher.Notifications.ShouldContain(sent => sent.Notification is CardReplaced);
    }

    [Fact]
    public async Task Clear_cards_in_auto_mode_cancels_the_schedule_and_reopens_joining()
    {
        var code = await _app.NewGameAsync();
        await _app.StartRoundAsync(code, startInAuto: true);

        (await _app.Round.ClearCardsAsync(code, Caller.Host, redrawCards: false, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
        (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Late"), TestApp.Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Only_the_host_can_start_or_cancel_a_round()
    {
        var code = await _app.NewGameAsync();
        var player = Caller.Participant("p1");

        (await _app.Round.StartCountdownAsync(code, player, false, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Round.CancelCountdownAsync(code, player, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Round.ClearCardsAsync(code, player, false, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }
}
