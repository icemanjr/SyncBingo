using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>
/// The "Rules updated" notice (FR-47): everyone is told what changed mid-round, in words a deaf
/// player can read, and the next ball waits long enough to read it.
/// </summary>
public sealed class RulesUpdatedTests
{
    [Fact]
    public void A_mid_round_change_lists_what_changed()
    {
        var session = InState(SessionState.Manual);

        var notice = session.ApplyRules(SessionSettings.Default with { FreeMarking = false }, "blackout", Now)
            .Value.OfType<RulesUpdated>().ShouldHaveSingleItem();

        notice.Changes.ShouldBe([new RuleChange("winCondition", "blackout"), new RuleChange("freeMarking", "false")]);
        notice.NextBallFrom.ShouldBe(Now + Session.RulesReadingTime);
    }

    [Fact]
    public void A_refused_lobby_only_change_raises_no_notice_and_no_wait()
    {
        var session = InState(SessionState.Manual);

        session.ChangeSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now).IsSuccess.ShouldBeFalse();

        session.NextBallFrom.ShouldBeNull();
    }

    [Fact]
    public void Changes_in_the_lobby_need_no_notice()
    {
        var session = NewSession();

        session.ApplyRules(SessionSettings.Default with { FreeMarking = false }, "blackout", Now).Value.OfType<RulesUpdated>().ShouldBeEmpty();

        session.NextBallFrom.ShouldBeNull();
    }

    [Fact]
    public void Host_only_settings_need_no_notice_and_no_wait()
    {
        var session = InState(SessionState.Manual);
        var hostOnly = SessionSettings.Default with
        {
            AutoCallInterval = TimeSpan.FromSeconds(20),
            WinDetection = WinDetection.Off,
            WinHighlight = false,
            ShowRoundNumbers = false,
        };

        session.ChangeSettings(hostOnly, Now).Value.OfType<RulesUpdated>().ShouldBeEmpty();

        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_next_ball_waits_for_the_reading_time()
    {
        var session = InState(SessionState.Manual);
        session.ChangePattern("blackout", Now).IsSuccess.ShouldBeTrue();
        var justBefore = Now + Session.RulesReadingTime - TimeSpan.FromMilliseconds(1);

        session.DrawBall(DrawSource.Manual, new SeededRandom(1), justBefore).Error!.Message.ShouldBe("Give everyone a moment to read the new rules.");
        session.StartAutoCalling(new SeededRandom(1), justBefore).Error!.Code.ShouldBe(DomainErrorCode.WrongState);

        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now + Session.RulesReadingTime).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Clearing_the_cards_ends_the_wait()
    {
        var session = InState(SessionState.Manual);
        session.ChangePattern("blackout", Now).IsSuccess.ShouldBeTrue();

        session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();

        session.NextBallFrom.ShouldBeNull();
    }

    [Fact]
    public void The_countdown_tells_everyone_the_rounds_pattern()
    {
        var session = NewSession(winCondition: "corners");

        session.StartCountdown(startInAuto: false, Now).Single<RoundCountdownStarted>().WinConditionId.ShouldBe("corners");
    }
}
