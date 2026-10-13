using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
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
    private const SessionState Manual = SessionState.Manual;
    private const SessionState Auto = SessionState.Auto;
    private const SessionState Ended = SessionState.Ended;

    private static readonly Dictionary<string, Func<Session, bool>> Commands = new()
    {
        ["Start round / Clear cards"] = s => s.StartRound(redrawCards: true, Now).IsSuccess,
        ["End round"] = s => s.EndRound(Now).IsSuccess,
        ["Auto mode"] = s => s.StartAutoCalling(new SeededRandom(1), Now).IsSuccess,
        ["Pause"] = s => s.Pause(Now).IsSuccess,
        ["Grab ball (host)"] = s => s.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).IsSuccess,
        ["Auto draw (scheduler)"] = s => s.DrawBall(DrawSource.Auto, new SeededRandom(1), Now).IsSuccess,
        ["Change win condition"] = s => s.ChangeWinCondition("blackout", Now).IsSuccess,
        ["Change settings"] = s => s.UpdateSettings(SessionSettings.Default with { ShowCallBoard = false }, Now).IsSuccess,
        ["Join"] = s => s.CheckCanAdmit(currentParticipants: 0).IsSuccess,
        ["Claim Bingo"] = s => s.ClaimBingo(Player(s), 1, Now).IsSuccess,
        ["Award win"] = s => s.AwardWin(Player(s), 1, Now).IsSuccess,
        ["Show leaderboard"] = s => s.ShowLeaderboard(true, Now).IsSuccess,
        ["Remove participant"] = s => s.RemoveParticipant(Player(s), Now).IsSuccess,
        ["End session"] = s => s.End(Now).IsSuccess,
    };

    public static TheoryData<string, SessionState, bool> Table => new()
    {
        { "Start round / Clear cards", Lobby, true }, { "Start round / Clear cards", Manual, true },
        { "Start round / Clear cards", Auto, true }, { "Start round / Clear cards", Ended, false },

        { "End round", Lobby, false }, { "End round", Manual, true },
        { "End round", Auto, true }, { "End round", Ended, false },

        { "Auto mode", Lobby, false }, { "Auto mode", Manual, true },
        { "Auto mode", Auto, false }, { "Auto mode", Ended, false },

        { "Pause", Lobby, false }, { "Pause", Manual, false },
        { "Pause", Auto, true }, { "Pause", Ended, false },

        { "Grab ball (host)", Lobby, false }, { "Grab ball (host)", Manual, true },
        { "Grab ball (host)", Auto, false }, { "Grab ball (host)", Ended, false },

        { "Auto draw (scheduler)", Lobby, false }, { "Auto draw (scheduler)", Manual, false },
        { "Auto draw (scheduler)", Auto, true }, { "Auto draw (scheduler)", Ended, false },

        { "Change win condition", Lobby, true }, { "Change win condition", Manual, true },
        { "Change win condition", Auto, true }, { "Change win condition", Ended, false },

        { "Change settings", Lobby, true }, { "Change settings", Manual, true },
        { "Change settings", Auto, true }, { "Change settings", Ended, false },

        { "Join", Lobby, true }, { "Join", Manual, false },
        { "Join", Auto, false }, { "Join", Ended, false },

        { "Claim Bingo", Lobby, false }, { "Claim Bingo", Manual, true },
        { "Claim Bingo", Auto, true }, { "Claim Bingo", Ended, false },

        { "Award win", Lobby, false }, { "Award win", Manual, true },
        { "Award win", Auto, true }, { "Award win", Ended, false },

        { "Show leaderboard", Lobby, true }, { "Show leaderboard", Manual, true },
        { "Show leaderboard", Auto, true }, { "Show leaderboard", Ended, false },

        { "Remove participant", Lobby, true }, { "Remove participant", Manual, true },
        { "Remove participant", Auto, true }, { "Remove participant", Ended, false },

        { "End session", Lobby, true }, { "End session", Manual, true },
        { "End session", Auto, true }, { "End session", Ended, false },
    };

    private static Participant Player(Session session) =>
        Participant.Join("p1", PlayerTestKit.Name("Sam"), PlayerTestKit.KnownCard, session, Now);

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
        var session = InState(state);
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
