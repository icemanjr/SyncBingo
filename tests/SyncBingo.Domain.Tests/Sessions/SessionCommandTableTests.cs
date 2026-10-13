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
    private const SessionState Starting = SessionState.Starting;
    private const SessionState Manual = SessionState.Manual;
    private const SessionState Auto = SessionState.Auto;
    private const SessionState Ended = SessionState.Ended;

    private static readonly Dictionary<string, Func<Session, bool>> Commands = new()
    {
        ["Start Round N (countdown)"] = s => s.StartCountdown(startInAuto: false, Now).IsSuccess,
        ["Cancel countdown"] = s => s.CancelCountdown(Now).IsSuccess,
        ["Start round (at zero)"] = s => s.StartRound(new SeededRandom(1), Now + Session.CountdownLength).IsSuccess,
        ["Clear cards"] = s => s.ClearCards(redrawCards: true, Now).IsSuccess,
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

    /// <summary>Allowed states per command; every other state must refuse it.</summary>
    private static readonly Dictionary<string, SessionState[]> AllowedIn = new()
    {
        ["Start Round N (countdown)"] = [Lobby],
        ["Cancel countdown"] = [Starting],
        ["Start round (at zero)"] = [Starting],
        ["Clear cards"] = [Lobby, Manual, Auto],
        ["End round"] = [Manual, Auto],
        ["Auto mode"] = [Manual],
        ["Pause"] = [Auto],
        ["Grab ball (host)"] = [Manual],
        ["Auto draw (scheduler)"] = [Auto],
        ["Change win condition"] = [Lobby, Starting, Manual, Auto],
        ["Change settings"] = [Lobby, Starting, Manual, Auto],
        ["Join"] = [Lobby],
        ["Claim Bingo"] = [Manual, Auto],
        ["Award win"] = [Manual, Auto],
        ["Show leaderboard"] = [Lobby, Starting, Manual, Auto],
        ["Remove participant"] = [Lobby, Starting, Manual, Auto],
        ["End session"] = [Lobby, Starting, Manual, Auto],
    };

    public static TheoryData<string, SessionState, bool> Table
    {
        get
        {
            var table = new TheoryData<string, SessionState, bool>();
            foreach (var (command, allowed) in AllowedIn)
            {
                foreach (var state in Enum.GetValues<SessionState>())
                {
                    table.Add(command, state, allowed.Contains(state));
                }
            }

            return table;
        }
    }

    [Fact]
    public void The_table_covers_every_command()
    {
        AllowedIn.Keys.ShouldBe(Commands.Keys, ignoreOrder: true);
        AllowedIn.Values.ShouldAllBe(states => !states.Contains(Ended));
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

    private static Participant Player(Session session) =>
        Participant.Join("p1", PlayerTestKit.Name("Sam"), PlayerTestKit.KnownCard, session, Now);
}
