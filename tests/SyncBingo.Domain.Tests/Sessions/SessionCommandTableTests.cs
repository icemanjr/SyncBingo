using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>
/// Every session command in every state, checked against the table in docs/03 §2
/// (✔ = allowed). Ended is not in the doc table: nothing is allowed once a session ends.
/// If this test and the doc disagree, one of them is wrong.
/// </summary>
public sealed class SessionCommandTableTests
{
    private const SessionState Lobby = SessionState.BetweenRounds;
    private const SessionState Running = SessionState.Running;
    private const SessionState Paused = SessionState.Paused;
    private const SessionState Ended = SessionState.Ended;

    private static readonly SessionSettings AutoCaller = new() { CallerMode = CallerMode.Auto };

    private static readonly Dictionary<string, Func<Session, bool>> Commands = new()
    {
        ["Start round / Clear cards"] = s => s.StartRound(redrawCards: true, Now).IsSuccess,
        ["End round"] = s => s.EndRound(Now).IsSuccess,
        ["Pause"] = s => s.Pause(Now).IsSuccess,
        ["Resume"] = s => s.Resume(Now).IsSuccess,
        ["Draw ball (host)"] = s => s.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess,
        ["Auto draw (scheduler)"] = s => s.DrawBall(DrawSource.Auto, new SeededRandom(1), Now).IsSuccess,
        ["Change win condition"] = s => s.ChangeWinCondition("blackout", Now).IsSuccess,
        ["Change settings"] = s => s.UpdateSettings(AutoCaller with { ShowRecentCalls = false }, Now).IsSuccess,
        ["Join"] = s => s.CheckCanAdmit(currentParticipants: 0).IsSuccess,
        ["End session"] = s => s.End(Now).IsSuccess,
    };

    public static TheoryData<string, SessionState, bool> Table => new()
    {
        { "Start round / Clear cards", Lobby, true }, { "Start round / Clear cards", Running, true },
        { "Start round / Clear cards", Paused, true }, { "Start round / Clear cards", Ended, false },

        { "End round", Lobby, false }, { "End round", Running, true },
        { "End round", Paused, true }, { "End round", Ended, false },

        { "Pause", Lobby, false }, { "Pause", Running, true },
        { "Pause", Paused, false }, { "Pause", Ended, false },

        { "Resume", Lobby, false }, { "Resume", Running, false },
        { "Resume", Paused, true }, { "Resume", Ended, false },

        { "Draw ball (host)", Lobby, false }, { "Draw ball (host)", Running, true },
        { "Draw ball (host)", Paused, true }, { "Draw ball (host)", Ended, false },

        { "Auto draw (scheduler)", Lobby, false }, { "Auto draw (scheduler)", Running, true },
        { "Auto draw (scheduler)", Paused, false }, { "Auto draw (scheduler)", Ended, false },

        { "Change win condition", Lobby, true }, { "Change win condition", Running, true },
        { "Change win condition", Paused, true }, { "Change win condition", Ended, false },

        { "Change settings", Lobby, true }, { "Change settings", Running, true },
        { "Change settings", Paused, true }, { "Change settings", Ended, false },

        { "Join", Lobby, true }, { "Join", Running, false },
        { "Join", Paused, false }, { "Join", Ended, false },

        { "End session", Lobby, true }, { "End session", Running, true },
        { "End session", Paused, true }, { "End session", Ended, false },
    };

    [Fact]
    public void The_table_covers_every_command_in_every_state()
    {
        var covered = Table.Select(row => (row.Data.Item1, row.Data.Item2)).ToHashSet();

        covered.Count.ShouldBe(Commands.Count * Enum.GetValues<SessionState>().Length);
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void Command_is_allowed_exactly_where_the_design_says(string command, SessionState state, bool allowed)
    {
        var session = InState(state, AutoCaller);
        var versionBefore = session.Version;

        var succeeded = Commands[command](session);

        succeeded.ShouldBe(allowed, $"{command} while {state}");
        if (!allowed)
        {
            session.State.ShouldBe(state, "a refused command must not change the state");
            session.Version.ShouldBe(versionBefore, "a refused command must not change the session");
        }
    }
}
