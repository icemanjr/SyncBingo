using SyncBingo.Application.Calling;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

/// <summary>The application layer wired to in-memory fakes.</summary>
internal sealed class TestApp
{
    public const string Password = "secret";

    public TestApp()
    {
        Runner = new SessionCommandRunner(Store, Publisher, Scheduler, Clock);
        Create = new CreateSessionHandler(Store, new FakePasswordHasher(), Tokens, new SeededRandom(1), Clock);
        Join = new JoinSessionHandler(Store, new FakePasswordHasher(), Tokens, Publisher, new SeededRandom(2), Clock);
        Host = new HostCommandHandler(Runner);
        Round = new RoundHandler(Runner, new SeededRandom(3));
        Draws = new ScriptedDraws(this);
        Draw = new DrawHandler(Runner, Draws);
    }

    public InMemorySessionStore Store { get; } = new();

    public RecordingPublisher Publisher { get; } = new();

    public RecordingScheduler Scheduler { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeTokenService Tokens { get; } = new();

    public SessionCommandRunner Runner { get; }

    public CreateSessionHandler Create { get; }

    public JoinSessionHandler Join { get; }

    public HostCommandHandler Host { get; }

    public RoundHandler Round { get; }

    public DrawHandler Draw { get; }

    /// <summary>Decides which balls the draw handler picks; random once the script runs out.</summary>
    public ScriptedDraws Draws { get; }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GameCode? Current { get; private set; }

    public async Task<GameCode> NewGameAsync(SessionSettings? settings = null)
    {
        var created = await Create.HandleAsync(new CreateSession(Password, settings), Ct);
        created.IsSuccess.ShouldBeTrue(created.ToString());
        Current = created.Value.Code;
        return created.Value.Code;
    }

    public async Task<string> JoinAsync(GameCode code, string name)
    {
        var joined = await Join.HandleAsync(new JoinSession(code.ToString(), Password, name), Ct);
        joined.IsSuccess.ShouldBeTrue(joined.ToString());
        return joined.Value.ParticipantId;
    }

    /// <summary>"Start Round N", then ten seconds later the scheduler's tick starts it.</summary>
    public async Task StartRoundAsync(GameCode code, bool startInAuto = false)
    {
        (await Round.StartCountdownAsync(code, Caller.Host, startInAuto, Ct)).IsSuccess.ShouldBeTrue();
        Clock.UtcNow += Session.CountdownLength;
        (await Draw.TickAsync(code, Ct)).IsSuccess.ShouldBeTrue();
        (await SessionAsync(code)).IsInRound.ShouldBeTrue();
    }

    public async Task<Session> SessionAsync(GameCode code) => (await Store.GetAsync(code, Ct))!;

    public async Task<Participant> PlayerAsync(GameCode code, string id) => (await Store.GetParticipantAsync(code, id, Ct))!;

    /// <summary>Picks the scripted ball numbers in order, then falls back to a seeded random.</summary>
    internal sealed class ScriptedDraws(TestApp app) : IRandomSource
    {
        private readonly SeededRandom _fallback = new(4);

        public Queue<int> Next { get; } = new();

        public int NextInt(int maxExclusive)
        {
            if (Next.Count == 0 || app.Current is not { } code)
            {
                return _fallback.NextInt(maxExclusive);
            }

            var session = app.Store.GetAsync(code, CancellationToken.None).Result!;
            var number = Next.Dequeue();
            return Enumerable.Range(1, number - 1).Count(n => !session.HasBeenCalled(n));
        }
    }
}
