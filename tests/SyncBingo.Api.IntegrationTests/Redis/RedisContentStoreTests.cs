using StackExchange.Redis;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>A session's custom set in Redis (ADR 0009): stored with the session, written once, read through a cache.</summary>
public sealed class RedisContentStoreTests(RedisFixture redis)
{
    private readonly ManualTime _time = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContentSet Words(string name = "Words") =>
        ContentSet.TryCreate(name, Enumerable.Range(1, 30).Select(i => new ContentEntry($"word {i}"))).Value;

    [Fact]
    public async Task A_session_comes_back_with_its_set_from_a_fresh_instance()
    {
        var store = redis.Store(_time);
        var session = RedisFixture.NewSession(_time.Now);
        await store.TryCreateAsync(session, "hashed", Ct);
        session.UseContent(Words(), groupItems: false, _time.Now).IsSuccess.ShouldBeTrue();

        await store.SaveAsync(session, Ct);
        var restored = (await redis.Store(_time).GetAsync(session.Code, Ct))!; // a new store: nothing cached

        restored.Content!.Name.ShouldBe("Words");
        restored.Content.Pairs.ShouldBe(session.Content!.Pairs);
        restored.Variant.BallMax.ShouldBe(30);
    }

    [Fact]
    public async Task The_same_instance_reads_the_set_from_its_cache()
    {
        var store = redis.Store(_time);
        var session = RedisFixture.NewSession(_time.Now);
        await store.TryCreateAsync(session, "hashed", Ct);
        session.UseContent(Words(), groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);

        var first = (await store.GetAsync(session.Code, Ct))!;
        var second = (await store.GetAsync(session.Code, Ct))!;

        second.Content.ShouldBeSameAs(first.Content);
        second.Content.ShouldBeSameAs(session.Content, "the set that was saved is the one cached");
    }

    [Fact]
    public async Task The_set_lives_in_the_sessions_meta_hash_and_goes_when_numbers_return()
    {
        var store = redis.Store(_time);
        var session = RedisFixture.NewSession(_time.Now);
        await store.TryCreateAsync(session, "hashed", Ct);
        var meta = (RedisKey)$"bingo:s:{{{session.Code.Value}}}:meta";
        var db = redis.Connection.GetDatabase();

        session.UseContent(Words(), groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);
        (await db.HashGetAsync(meta, "content")).ToString().ShouldContain("\"name\": \"Words\"");
        var id = (await db.HashGetAsync(meta, "content-id")).ToString();
        id.Length.ShouldBe(32);

        session.UseContent(null, groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);
        (await db.HashExistsAsync(meta, "content")).ShouldBeFalse();
        (await db.HashExistsAsync(meta, "content-id")).ShouldBeFalse();
    }

    [Fact]
    public async Task Saving_again_keeps_the_stored_set()
    {
        var store = redis.Store(_time);
        var session = RedisFixture.NewSession(_time.Now);
        await store.TryCreateAsync(session, "hashed", Ct);
        session.UseContent(Words(), groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);
        session.StartCountdown(startInAuto: false, _time.Now);
        session.StartRound(new SyncBingo.Domain.CryptoRandomSource(), _time.Now + Session.CountdownLength);

        await store.SaveAsync(session, Ct);
        var restored = (await redis.Store(_time).GetAsync(session.Code, Ct))!;

        restored.State.ShouldBe(SessionState.Manual);
        restored.Content!.Name.ShouldBe("Words");
    }

    [Fact]
    public async Task Changing_the_set_replaces_it()
    {
        var store = redis.Store(_time);
        var session = RedisFixture.NewSession(_time.Now);
        await store.TryCreateAsync(session, "hashed", Ct);
        session.UseContent(Words("First"), groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);

        session.UseContent(Words("Second"), groupItems: false, _time.Now);
        await store.SaveAsync(session, Ct);

        (await redis.Store(_time).GetAsync(session.Code, Ct))!.Content!.Name.ShouldBe("Second");
    }
}
