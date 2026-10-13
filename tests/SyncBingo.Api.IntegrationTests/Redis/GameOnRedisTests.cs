using System.Collections.Concurrent;
using SyncBingo.Application;
using SyncBingo.Application.Calling;
using SyncBingo.Application.Players;
using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>The application's handlers on the real Redis store, including real concurrency (docs/02 §6).</summary>
public sealed class GameOnRedisTests(RedisFixture redis)
{
    private readonly Clock _clock = new();
    private readonly Publisher _publisher = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Marks_made_while_balls_are_drawn_with_auto_fill_are_never_lost()
    {
        var store = redis.Store(TimeProvider.System);
        var random = new CryptoRandomSource();
        var runner = new SessionCommandRunner(store, _publisher, new NoScheduler(), _clock);
        var draw = new DrawHandler(runner, random);
        var players = new PlayerCommandHandler(runner, _publisher, random);
        var created = await new CreateSessionHandler(store, new PlainHasher(), new Tokens(), random, _clock)
            .HandleAsync(new CreateSession("secret", SessionSettings.Default with { AutoFillAvailable = true }), Ct);
        var code = created.Value.Code;
        var joined = await new JoinSessionHandler(store, new PlainHasher(), new Tokens(), _publisher, random, _clock)
            .HandleAsync(new JoinSession(code.ToString(), "secret", "Ava"), Ct);
        var ava = Caller.Participant(joined.Value.ParticipantId);
        (await players.SetAutoFillAsync(code, ava, true, Ct)).IsSuccess.ShouldBeTrue();
        (await new RoundHandler(runner, random).StartCountdownAsync(code, Caller.Host, startInAuto: false, Ct)).IsSuccess.ShouldBeTrue();
        _clock.UtcNow += Session.CountdownLength;
        (await draw.TickAsync(code, Ct)).IsSuccess.ShouldBeTrue();

        // Ava taps every square (Free marking is on) while the host grabs 30 balls.
        var marked = new ConcurrentBag<int>();
        var marking = Task.WhenAll(Enumerable.Range(0, 25).Select(async cell =>
        {
            var result = await players.MarkAsync(code, ava, 1, cell, marked: true, Ct);
            if (result.IsSuccess)
            {
                marked.Add(cell);
            }
            else
            {
                result.Error!.Code.ShouldBe(DomainErrorCode.Conflict, "the only acceptable refusal is 'please try again'");
            }
        }));
        var drawing = Task.Run(async () =>
        {
            for (var i = 0; i < 30; i++)
            {
                (await draw.GrabBallAsync(code, Caller.Host, Ct)).IsSuccess.ShouldBeTrue();
            }
        }, Ct);
        await Task.WhenAll(marking, drawing);

        var card = (await store.GetParticipantAsync(code, ava.ParticipantId!, Ct))!.Participant.Cards[0];
        marked.ShouldNotBeEmpty();
        marked.ShouldAllBe(cell => card.IsMarked(cell), "every mark the player was told succeeded is still there");
        _publisher.AutoFilled.ShouldAllBe(cell => card.IsMarked(cell), "every cell auto-fill reported is still marked");
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class Publisher : IEventPublisher
    {
        public ConcurrentBag<int> AutoFilled { get; } = [];

        public Task PublishAsync(GameCode code, IReadOnlyList<SessionEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyAsync(GameCode code, Recipient recipient, Notification notification, CancellationToken cancellationToken)
        {
            if (notification is AutoFillApplied filled)
            {
                foreach (var cell in filled.Cells)
                {
                    AutoFilled.Add(cell.Cell);
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NoScheduler : ICallScheduler
    {
        public Task ScheduleAsync(GameCode code, DateTimeOffset dueAt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelAsync(GameCode code, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PlainHasher : IPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string hash) => password == hash;
    }

    private sealed class Tokens : ITokenService
    {
        public IssuedToken Issue()
        {
            var token = Guid.NewGuid().ToString("N");
            return new IssuedToken(token, Hash(token));
        }

        public string Hash(string token) => "h:" + token;
    }
}
