using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

public sealed class SettingsAndJoinInfoTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Changing_one_setting_keeps_the_others()
    {
        var code = await _app.NewGameAsync(SessionSettings.Default with { FreeMarking = false });

        (await _app.Host.ChangeSettingsAsync(code, Caller.Host, s => s with { ShowCallBoard = false }, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        var settings = (await _app.SessionAsync(code)).Settings;
        settings.ShowCallBoard.ShouldBeFalse();
        settings.FreeMarking.ShouldBeFalse("an untouched setting stays as it was");
    }

    [Fact]
    public async Task A_change_during_a_round_builds_on_settings_waiting_for_the_next_round()
    {
        var code = await _app.NewGameAsync();
        await _app.StartRoundAsync(code);
        (await _app.Host.ChangeSettingsAsync(code, Caller.Host, s => s with { FreeSpace = FreeSpace.Earned }, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Host.ChangeSettingsAsync(code, Caller.Host, s => s with { ShowCallBoard = false }, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.SessionAsync(code)).NextRoundSettings!.FreeSpace.ShouldBe(FreeSpace.Earned, "the earlier change still waits");
    }

    [Fact]
    public async Task Only_the_host_changes_settings()
    {
        var code = await _app.NewGameAsync();

        (await _app.Host.ChangeSettingsAsync(code, Caller.Participant("p1"), s => s, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }

    [Fact]
    public async Task Join_info_says_whether_joining_is_open_and_which_round_is_next()
    {
        var code = await _app.NewGameAsync();
        var query = new JoinInfoQuery(_app.Store);

        (await query.GetAsync(code.ToString(), TestApp.Ct)).Value.ShouldBe(new JoinInfo(true, 1));
        await _app.StartRoundAsync(code);
        (await query.GetAsync(code.ToString(), TestApp.Ct)).Value.ShouldBe(new JoinInfo(false, 1));
    }

    [Theory]
    [InlineData("not-a-code")]
    [InlineData("9ve4-shp3-k7mw-a2qx")]
    public async Task Join_info_for_an_unknown_game_is_not_found(string code)
    {
        (await new JoinInfoQuery(_app.Store).GetAsync(code, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task Join_info_for_an_ended_game_is_not_found()
    {
        var code = await _app.NewGameAsync();
        (await _app.Host.EndSessionAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await new JoinInfoQuery(_app.Store).GetAsync(code.ToString(), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }
}
