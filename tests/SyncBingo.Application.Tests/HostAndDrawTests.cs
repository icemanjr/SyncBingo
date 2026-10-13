using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

public sealed class HostAndDrawTests
{
    private readonly TestApp _app = new();

    [Theory]
    [InlineData(CallerRole.Participant)]
    [InlineData(CallerRole.Display)]
    public async Task Only_the_host_can_run_host_commands(CallerRole role)
    {
        var code = await _app.NewGameAsync();
        var caller = new Caller(role, role == CallerRole.Participant ? "p1" : null);

        (await _app.Round.StartCountdownAsync(code, caller, false, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Draw.GrabBallAsync(code, caller, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Host.EndSessionAsync(code, caller, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        _app.Publisher.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Commands_for_an_unknown_game_are_not_found()
    {
        var result = await _app.Host.EndSessionAsync(GameCode.Parse("9ve4-shp3-k7mw-a2qx"), Caller.Host, TestApp.Ct);

        result.Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task A_refused_command_saves_and_publishes_nothing()
    {
        var code = await _app.NewGameAsync();
        var saves = _app.Store.Saves;

        (await _app.Host.PauseAsync(code, Caller.Host, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.WrongState);

        _app.Store.Saves.ShouldBe(saves);
        _app.Publisher.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_successful_command_is_saved_then_published()
    {
        var code = await _app.NewGameAsync();

        (await _app.Host.ShowLeaderboardAsync(code, Caller.Host, true, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).LeaderboardVisible.ShouldBeTrue();
        _app.Publisher.Events.ShouldHaveSingleItem().ShouldBeOfType<LeaderboardVisibilityChanged>();
    }

    [Fact]
    public async Task Clear_cards_with_redraw_gives_everyone_a_new_unique_card()
    {
        var code = await _app.NewGameAsync();
        var ids = new[] { await _app.JoinAsync(code, "Ava"), await _app.JoinAsync(code, "Ben") };
        var before = _app.Store.CardsInPlay(code).ToHashSet();

        (await _app.Round.ClearCardsAsync(code, Caller.Host, redrawCards: true, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var after = _app.Store.CardsInPlay(code);
        after.Count.ShouldBe(2);
        after.ShouldAllBe(fingerprint => !before.Contains(fingerprint), "old cards were released and replaced");
        foreach (var id in ids)
        {
            var player = await _app.PlayerAsync(code, id);
            after.ShouldContain(player.Cards[0].Card.Fingerprint);
            _app.Publisher.Notifications.ShouldContain(n => n.Recipient == new Recipient.Participant(id) && n.Notification is CardReplaced);
        }
    }

    [Fact]
    public async Task Starting_a_round_keeps_cards_and_clears_marks()
    {
        var code = await _app.NewGameAsync();
        var id = await _app.JoinAsync(code, "Ava");
        var card = (await _app.PlayerAsync(code, id)).Cards[0].Card;

        await _app.StartRoundAsync(code);

        (await _app.PlayerAsync(code, id)).Cards[0].Card.ShouldBe(card);
        _app.Publisher.Notifications.ShouldNotContain(n => n.Notification is CardReplaced);
    }

    [Fact]
    public async Task Grabbing_a_ball_publishes_it()
    {
        var code = await StartedGameAsync();

        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Publisher.Events.OfType<BallCalled>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Auto_fill_reaches_the_player_before_the_next_ball_is_announced()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { AutoFillAvailable = true });
        var id = await _app.JoinAsync(code, "Ava");
        var player = await _app.PlayerAsync(code, id);
        player.SetAutoFill(true, await _app.SessionAsync(code)).IsSuccess.ShouldBeTrue();
        await StartRoundAsync(code);
        var onCard = (await _app.PlayerAsync(code, id)).Cards[0].Card.Cells[0];
        _app.Draws.Next.Enqueue(onCard);
        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        _app.Publisher.Sent.Clear();

        (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var (recipient, notification) = ((Recipient, Notification))_app.Publisher.Sent[0];
        recipient.ShouldBe(new Recipient.Participant(id));
        notification.ShouldBeOfType<AutoFillApplied>().Cells.ShouldBe([new(1, 0)]);
        _app.Publisher.Sent[1].ShouldBeOfType<BallCalled>();
        (await _app.PlayerAsync(code, id)).Cards[0].IsMarked(0).ShouldBeTrue();
    }

    [Fact]
    public async Task A_completed_line_is_detected_right_after_the_ball_that_completed_it()
    {
        var code = await _app.NewGameAsync();
        var id = await _app.JoinAsync(code, "Ava");
        await StartRoundAsync(code);
        var topRow = (await _app.PlayerAsync(code, id)).Cards[0].Card.Cells.Take(5);
        foreach (var ball in topRow)
        {
            _app.Draws.Next.Enqueue(ball);
            (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        }

        var events = _app.Publisher.Events.ToList();
        events[^1].ShouldBeOfType<WinDetected>().Name.ShouldBe("Ava");
        events[^2].ShouldBeOfType<BallCalled>();
    }

    [Fact]
    public async Task Auto_mode_draws_at_once_and_schedules_the_next_ball()
    {
        var code = await StartedGameAsync(SessionSettings.Default with { AutoCallInterval = TimeSpan.FromSeconds(8) });

        (await _app.Draw.StartAutoModeAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Publisher.Events.Select(e => e.GetType()).ShouldBe(
            [typeof(RoundCountdownStarted), typeof(RoundStarted), typeof(AutoCallingStarted), typeof(BallCalled)]);
        _app.Scheduler.Due[code.Value].ShouldBe(_app.Clock.UtcNow.AddSeconds(8));
    }

    [Fact]
    public async Task Each_scheduler_tick_draws_and_schedules_the_next()
    {
        var code = await StartedGameAsync();
        (await _app.Draw.StartAutoModeAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        _app.Clock.UtcNow = _app.Clock.UtcNow.AddSeconds(10);

        (await _app.Draw.TickAsync(code, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).Calls.Count.ShouldBe(2);
        _app.Scheduler.Due[code.Value].ShouldBe(_app.Clock.UtcNow.AddSeconds(10));
    }

    [Fact]
    public async Task Pause_cancels_the_schedule_and_a_late_tick_changes_nothing()
    {
        var code = await StartedGameAsync();
        (await _app.Draw.StartAutoModeAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Host.PauseAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
        var published = _app.Publisher.Sent.Count;

        (await _app.Draw.TickAsync(code, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).Calls.Count.ShouldBe(1);
        _app.Publisher.Sent.Count.ShouldBe(published);
    }

    [Fact]
    public async Task Alert_and_pause_stops_the_schedule_when_a_win_is_detected()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { WinDetection = WinDetection.AlertHostAndPause });
        var id = await _app.JoinAsync(code, "Ava");
        await StartRoundAsync(code);
        var topRow = (await _app.PlayerAsync(code, id)).Cards[0].Card.Cells.Take(5).ToList();
        foreach (var ball in topRow.Take(4))
        {
            _app.Draws.Next.Enqueue(ball);
            (await _app.Draw.GrabBallAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        }

        _app.Draws.Next.Enqueue(topRow[4]);
        (await _app.Draw.StartAutoModeAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).State.ShouldBe(SessionState.Manual);
        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
        _app.Publisher.Events.TakeLast(2).Select(e => e.GetType()).ShouldBe([typeof(WinDetected), typeof(AutoCallingPaused)]);
    }

    [Fact]
    public async Task Ending_a_round_in_auto_mode_cancels_the_schedule()
    {
        var code = await StartedGameAsync();
        (await _app.Draw.StartAutoModeAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Host.EndRoundAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        _app.Scheduler.Due.ShouldNotContainKey(code.Value);
    }

    private async Task<GameCode> StartedGameAsync(SessionSettings? settings = null)
    {
        var code = await _app.NewGameAsync(settings);
        await StartRoundAsync(code);
        return code;
    }

    private Task StartRoundAsync(GameCode code) => _app.StartRoundAsync(code);
}
