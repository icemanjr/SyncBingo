using Microsoft.Extensions.Logging.Abstractions;
using SyncBingo.Application;
using SyncBingo.Application.Calling;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;
using SyncBingo.Infrastructure;
using SyncBingo.Infrastructure.Redis;
using SyncBingo.Infrastructure.Scheduling;
using SyncBingo.Infrastructure.Security;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>The scheduler service driving real handlers on Redis: countdowns end and auto mode draws on time.</summary>
public sealed class CallSchedulerServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(6);

    private readonly ManualTime _time = new();
    private readonly RedisSessionStore _store;
    private readonly RedisCallScheduler _scheduler;
    private readonly SessionCommandRunner _runner;

    public CallSchedulerServiceTests(RedisFixture redis)
    {
        var options = new RedisStoreOptions { KeyPrefix = $"t{Guid.NewGuid():N}:" };
        _store = redis.Store(_time, options);
        _scheduler = new RedisCallScheduler(redis.Connection, options, _time);
        _runner = new SessionCommandRunner(_store, new NoPublisher(), _scheduler, new SystemClock(_time));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_round_counted_down_in_auto_mode_starts_and_keeps_drawing_on_time()
    {
        var code = await NewGameAsync();
        (await new RoundHandler(_runner, new CryptoRandomSource()).StartCountdownAsync(code, Caller.Host, startInAuto: true, Ct))
            .IsSuccess.ShouldBeTrue();
        var service = Service();

        (await service.RunOnceAsync(Ct)).ShouldBe(0, "the countdown is still running");
        _time.Now += Session.CountdownLength;
        (await service.RunOnceAsync(Ct)).ShouldBe(1);

        var session = (await _store.GetAsync(code, Ct))!;
        session.State.ShouldBe(SessionState.Auto);
        session.Calls.Count.ShouldBe(1);
        (await _scheduler.DueAtAsync(code)).ShouldBe(_time.Now + Interval);

        _time.Now += Interval;
        (await service.RunOnceAsync(Ct)).ShouldBe(1);
        (await _store.GetAsync(code, Ct))!.Calls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_manual_round_leaves_the_schedule_once_it_starts()
    {
        var code = await NewGameAsync();
        (await new RoundHandler(_runner, new CryptoRandomSource()).StartCountdownAsync(code, Caller.Host, startInAuto: false, Ct))
            .IsSuccess.ShouldBeTrue();
        _time.Now += Session.CountdownLength;

        (await Service().RunOnceAsync(Ct)).ShouldBe(1);

        (await _store.GetAsync(code, Ct))!.State.ShouldBe(SessionState.Manual);
        (await _scheduler.DueAtAsync(code)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_that_is_gone_is_taken_off_the_schedule()
    {
        var code = GameCode.Generate(new CryptoRandomSource());
        await _scheduler.ScheduleAsync(code, _time.Now, Ct);

        (await Service().RunOnceAsync(Ct)).ShouldBe(1);

        (await _scheduler.DueAtAsync(code)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_instances_polling_together_draw_each_ball_once()
    {
        var code = await NewGameAsync();
        (await new RoundHandler(_runner, new CryptoRandomSource()).StartCountdownAsync(code, Caller.Host, startInAuto: true, Ct))
            .IsSuccess.ShouldBeTrue();
        _time.Now += Session.CountdownLength;

        var claimed = await Task.WhenAll(Service().RunOnceAsync(Ct), Service().RunOnceAsync(Ct));

        claimed.Sum().ShouldBe(1);
        (await _store.GetAsync(code, Ct))!.Calls.Count.ShouldBe(1);
    }

    private CallSchedulerService Service() => new(
        _scheduler,
        new DrawHandler(_runner, new CryptoRandomSource()),
        new CallSchedulerOptions(),
        _time,
        NullLogger<CallSchedulerService>.Instance);

    private async Task<GameCode> NewGameAsync()
    {
        var created = await new CreateSessionHandler(
                _store, new Pbkdf2PasswordHasher(iterations: 1), new RandomTokenService(), new CryptoRandomSource(), new SystemClock(_time))
            .HandleAsync(new CreateSession("secret", SessionSettings.Default with { AutoCallInterval = Interval }), Ct);
        return created.Value.Code;
    }

    private sealed class NoPublisher : Application.Ports.IEventPublisher
    {
        public Task PublishAsync(GameCode code, IReadOnlyList<SessionEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyAsync(
            GameCode code, Application.Ports.Recipient recipient, Application.Ports.Notification notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
