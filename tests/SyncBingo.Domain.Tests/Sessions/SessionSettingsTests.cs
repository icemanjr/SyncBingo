using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

public sealed class SessionSettingsTests
{
    [Fact]
    public void Defaults_follow_the_documented_decisions()
    {
        var defaults = SessionSettings.Default;

        defaults.FreeMarking.ShouldBeTrue(); // FR-22
        defaults.Unmarking.ShouldBe(Unmarking.Any); // FR-24
        defaults.FreeSpace.ShouldBe(FreeSpace.AlwaysMarked); // FR-26, A8
        defaults.RequireMarksForClaims.ShouldBeFalse(); // FR-23
        defaults.AutoFillAvailable.ShouldBeFalse(); // FR-16: opt-in
    }

    [Fact]
    public void Between_rounds_every_setting_applies_at_once()
    {
        var session = NewSession();
        var changed = SessionSettings.Default with { FreeSpace = FreeSpace.Earned, ShowCallBoard = false };

        var settingsChanged = session.UpdateSettings(changed, Now).Single<SettingsChanged>();

        session.Settings.ShouldBe(changed);
        session.NextRoundSettings.ShouldBeNull();
        settingsChanged.NextRoundSettings.ShouldBeNull();
    }

    [Fact]
    public void During_a_round_round_scoped_settings_wait_for_the_next_round()
    {
        var session = InState(SessionState.Running);
        var changed = SessionSettings.Default with
        {
            FreeSpace = FreeSpace.Earned,
            RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw,
            ShowCallBoard = false,
        };

        session.UpdateSettings(changed, Now).IsSuccess.ShouldBeTrue();

        session.Settings.ShowCallBoard.ShouldBeFalse(); // applies now
        session.Settings.FreeSpace.ShouldBe(FreeSpace.AlwaysMarked); // waits
        session.Settings.RedrawPolicy.ShouldBe(RedrawPolicy.HostControls); // waits
        session.NextRoundSettings.ShouldBe(changed);

        session.StartRound(redrawCards: false, Now).IsSuccess.ShouldBeTrue();

        session.Settings.ShouldBe(changed);
        session.NextRoundSettings.ShouldBeNull();
    }

    [Fact]
    public void Nothing_waits_when_only_immediate_settings_change_mid_round()
    {
        var session = InState(SessionState.Running);

        session.UpdateSettings(SessionSettings.Default with { HighlightMatches = true }, Now).IsSuccess.ShouldBeTrue();

        session.NextRoundSettings.ShouldBeNull();
        session.Settings.HighlightMatches.ShouldBeTrue();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(121)]
    public void Rejects_an_auto_call_interval_outside_3_to_120_seconds(int seconds)
    {
        var session = NewSession();
        var settings = SessionSettings.Default with { AutoCallInterval = TimeSpan.FromSeconds(seconds) };

        session.UpdateSettings(settings, Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        session.Settings.ShouldBe(SessionSettings.Default);
    }

    [Fact]
    public void Rejects_unknown_enum_values()
    {
        var settings = SessionSettings.Default with { Unmarking = (Unmarking)42 };

        NewSession().UpdateSettings(settings, Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }
}
