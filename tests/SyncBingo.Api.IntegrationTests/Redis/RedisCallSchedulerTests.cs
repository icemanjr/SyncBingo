using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;
using SyncBingo.Infrastructure.Redis;
using SyncBingo.Infrastructure.Scheduling;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>The schedule's claim and lease on a real Redis (docs/02 §5).</summary>
public sealed class RedisCallSchedulerTests(RedisFixture redis)
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(5);

    private readonly ManualTime _time = new();

    // The schedule is one key for the whole app, so each test gets its own prefix.
    private readonly RedisStoreOptions _options = new() { KeyPrefix = $"t{Guid.NewGuid():N}:" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_session_is_claimed_once_it_is_due_and_not_before()
    {
        var scheduler = Scheduler();
        var code = NewCode();
        await scheduler.ScheduleAsync(code, _time.Now.AddSeconds(10), Ct);

        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldBeEmpty();
        _time.Now += TimeSpan.FromSeconds(10);

        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldHaveSingleItem().Code.ShouldBe(code);
    }

    [Fact]
    public async Task A_claim_keeps_others_away_until_its_lease_ends()
    {
        var scheduler = Scheduler();
        await scheduler.ScheduleAsync(NewCode(), _time.Now, Ct);
        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldHaveSingleItem();

        _time.Now += Lease - TimeSpan.FromMilliseconds(1);
        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldBeEmpty("the claimer may still be working on it");

        _time.Now += TimeSpan.FromMilliseconds(1);
        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldHaveSingleItem("the claimer died: someone else picks it up");
    }

    [Fact]
    public async Task Racing_instances_claim_each_due_session_exactly_once()
    {
        var codes = Enumerable.Range(0, 30).Select(_ => NewCode()).ToList();
        foreach (var code in codes)
        {
            await Scheduler().ScheduleAsync(code, _time.Now, Ct);
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Scheduler().ClaimDueAsync(Lease, 5, Ct)));

        var claimed = claims.SelectMany(c => c).Select(c => c.Code).ToList();
        claimed.Count.ShouldBe(30, "10 instances taking up to 5 each cover all 30");
        claimed.Distinct().Count().ShouldBe(30, "and no session is claimed twice");
        claims.ShouldAllBe(c => c.Count <= 5);
    }

    [Fact]
    public async Task A_cancelled_session_is_never_claimed()
    {
        var scheduler = Scheduler();
        var code = NewCode();
        await scheduler.ScheduleAsync(code, _time.Now, Ct);

        await scheduler.CancelAsync(code, Ct);

        (await scheduler.ClaimDueAsync(Lease, 50, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dropping_a_claim_removes_the_session()
    {
        var scheduler = Scheduler();
        var code = NewCode();
        await scheduler.ScheduleAsync(code, _time.Now, Ct);
        var claim = (await scheduler.ClaimDueAsync(Lease, 50, Ct)).Single();

        (await scheduler.DropIfStillClaimedAsync(claim, Ct)).ShouldBeTrue();

        (await scheduler.DueAtAsync(code)).ShouldBeNull();
    }

    [Fact]
    public async Task Dropping_a_claim_leaves_a_session_rescheduled_since()
    {
        var scheduler = Scheduler();
        var code = NewCode();
        await scheduler.ScheduleAsync(code, _time.Now, Ct);
        var claim = (await scheduler.ClaimDueAsync(Lease, 50, Ct)).Single();
        var next = _time.Now.AddSeconds(8);
        await scheduler.ScheduleAsync(code, next, Ct); // e.g. the host pressed Auto mode meanwhile

        (await scheduler.DropIfStillClaimedAsync(claim, Ct)).ShouldBeFalse();

        (await scheduler.DueAtAsync(code)).ShouldBe(next);
    }

    private RedisCallScheduler Scheduler() => new(redis.Connection, _options, _time);

    private static GameCode NewCode() => GameCode.Generate(new CryptoRandomSource());
}
