using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>The host's Apply in Options: settings and pattern change together, and only when it's quiet (FR-32, docs/06).</summary>
public sealed class ApplyRulesTests
{
    private static readonly SessionSettings NoBoard = SessionSettings.Default with { ShowCallBoard = false };

    [Fact]
    public void Settings_and_pattern_change_in_one_step()
    {
        var session = InState(SessionState.Manual);
        var version = session.Version;

        var events = session.ApplyRules(NoBoard, "blackout", Now).Value;

        events.Select(e => e.GetType()).ShouldBe([typeof(SettingsChanged), typeof(WinConditionChanged), typeof(RulesUpdated)]);
        session.Settings.ShowCallBoard.ShouldBeFalse();
        session.WinConditionId.ShouldBe("blackout");
        session.Version.ShouldBe(version + 1, "one change, however many rules");
    }

    [Fact]
    public void Only_what_changed_is_announced()
    {
        var session = InState(SessionState.Manual);

        session.ApplyRules(session.Settings, "blackout", Now).Value.Select(e => e.GetType())
            .ShouldBe([typeof(WinConditionChanged), typeof(RulesUpdated)]);
        session.ApplyRules(NoBoard, "blackout", Now).Value.Select(e => e.GetType())
            .ShouldBe([typeof(SettingsChanged), typeof(RulesUpdated)]);
    }

    [Fact]
    public void Applying_the_rules_in_force_changes_nothing()
    {
        var session = InState(SessionState.Manual);
        var version = session.Version;

        session.ApplyRules(session.Settings, session.WinConditionId, Now).Value.ShouldBeEmpty();

        session.Version.ShouldBe(version);
    }

    [Fact]
    public void The_rules_cannot_change_in_auto_mode()
    {
        var session = InState(SessionState.Auto);

        session.ApplyRules(NoBoard, "blackout", Now).Error!.Message.ShouldBe("Pause before changing the rules.");

        session.Settings.ShouldBe(SessionSettings.Default);
        session.WinConditionId.ShouldBe("normal");
    }

    [Fact]
    public void An_unknown_pattern_leaves_the_settings_alone()
    {
        var session = InState(SessionState.Manual);

        session.ApplyRules(NoBoard, "zigzag", Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);

        session.Settings.ShouldBe(SessionSettings.Default);
    }

    [Fact]
    public void Invalid_settings_leave_the_pattern_alone()
    {
        var session = InState(SessionState.Manual);

        session.ApplyRules(SessionSettings.Default with { AutoCallInterval = TimeSpan.Zero }, "blackout", Now)
            .Error!.Code.ShouldBe(DomainErrorCode.Invalid);

        session.WinConditionId.ShouldBe("normal");
    }

    [Fact]
    public void Round_scoped_settings_change_only_in_the_lobby()
    {
        var session = InState(SessionState.Manual);

        session.ApplyRules(NoBoard with { FreeSpace = FreeSpace.Earned }, "blackout", Now)
            .Error!.Message.ShouldBe("Free space can only be changed between rounds.");

        session.Settings.ShouldBe(SessionSettings.Default);
        session.WinConditionId.ShouldBe("normal", "nothing else in the same Apply changes either");
        NewSession().ApplyRules(NoBoard with { FreeSpace = FreeSpace.Earned }, "normal", Now).IsSuccess.ShouldBeTrue();
    }
}
