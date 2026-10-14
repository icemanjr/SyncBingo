using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;
namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>Builds sessions in a given state for tests.</summary>
internal static class SessionTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 19, 0, 0, TimeSpan.Zero);

    public static Session NewSession(SessionSettings? settings = null, string winCondition = "normal") =>
        Session.Create(
            GameCode.Parse("9ve4-shp3-k7mw-a2qx"),
            StandardVariants.Classic75,
            settings ?? SessionSettings.Default,
            winCondition,
            Now).Value;

    public static Session InState(SessionState state, SessionSettings? settings = null)
    {
        var session = NewSession(settings);
        if (state == SessionState.BetweenRounds)
        {
            return session;
        }

        if (state == SessionState.Starting)
        {
            session.StartCountdown(startInAuto: false, Now).IsSuccess.ShouldBeTrue();
            return session;
        }

        session.NextRound(Now).ShouldNotBeEmpty();
        switch (state)
        {
            case SessionState.Auto:
                session.StartAutoCalling(new SeededRandom(0), Now).IsSuccess.ShouldBeTrue();
                break;
            case SessionState.Ended:
                session.End(Now).IsSuccess.ShouldBeTrue();
                break;
        }

        session.State.ShouldBe(state);
        return session;
    }

    /// <summary>
    /// Clear cards (if a round is running), "Start Round N", and the start at zero: what a
    /// host does to move on to the next round. Returns every event, in order.
    /// </summary>
    public static IReadOnlyList<SessionEvent> NextRound(this Session session, DateTimeOffset now, bool startInAuto = false)
    {
        var events = new List<SessionEvent>();
        if (session.IsInRound)
        {
            events.AddRange(session.ClearCards(redrawCards: false, now).Value);
        }

        events.AddRange(session.StartCountdown(startInAuto, now).Value);
        events.AddRange(session.StartRound(new SeededRandom(0), now + Session.CountdownLength).Value);
        return events;
    }

    /// <summary>The host applying a new pattern and nothing else.</summary>
    public static Result<IReadOnlyList<SessionEvent>> ChangePattern(this Session session, string winConditionId, DateTimeOffset now) =>
        session.ApplyRules(session.NextRoundSettings ?? session.Settings, winConditionId, now);

    /// <summary>The host applying new settings and keeping the pattern.</summary>
    public static Result<IReadOnlyList<SessionEvent>> ChangeSettings(this Session session, SessionSettings settings, DateTimeOffset now) =>
        session.ApplyRules(settings, session.WinConditionId, now);

    public static T Single<T>(this Result<IReadOnlyList<SessionEvent>> result)
        where T : SessionEvent
    {
        result.IsSuccess.ShouldBeTrue(result.ToString());
        return result.Value.ShouldHaveSingleItem().ShouldBeOfType<T>();
    }
}
