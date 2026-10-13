using StackExchange.Redis;
using SyncBingo.Application;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using SyncBingo.Infrastructure.Redis;

namespace SyncBingo.Api.IntegrationTests.Redis;

/// <summary>The Redis store keeps the <c>ISessionStore</c> contract against a real Redis (docs/04 §4).</summary>
public sealed class RedisSessionStoreTests(RedisFixture redis)
{
    private static readonly string[] KeySuffixes = ["meta", "participants", "names", "tokens", "token-owners", "cards"];

    private static readonly Card AnyCard = new(Enumerable.Range(1, 25).Select(i => i == 13 ? 0 : i));

    private readonly ManualTime _time = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RedisSessionStore Store => redis.Store(_time);

    [Fact]
    public async Task A_created_session_reads_back_the_same()
    {
        var session = RedisFixture.NewSession(_time.Now);

        (await Store.TryCreateAsync(session, "hashed", Ct)).ShouldBeTrue();

        (await Store.GetAsync(session.Code, Ct))!.ToSnapshot().ShouldBeEquivalentTo(session.ToSnapshot());
        (await Store.GetPasswordHashAsync(session.Code, Ct)).ShouldBe("hashed");
    }

    [Fact]
    public async Task A_code_in_use_cannot_be_created_again()
    {
        var session = RedisFixture.NewSession(_time.Now);
        (await Store.TryCreateAsync(session, "first", Ct)).ShouldBeTrue();

        (await Store.TryCreateAsync(session, "second", Ct)).ShouldBeFalse();

        (await Store.GetPasswordHashAsync(session.Code, Ct)).ShouldBe("first");
    }

    [Fact]
    public async Task A_saved_session_keeps_its_changes()
    {
        var session = await CreatedAsync();
        session.StartCountdown(startInAuto: true, _time.Now).IsSuccess.ShouldBeTrue();

        await Store.SaveAsync(session, Ct);

        var loaded = (await Store.GetAsync(session.Code, Ct))!;
        loaded.State.ShouldBe(SessionState.Starting);
        loaded.StartInAuto.ShouldBeTrue();
        loaded.Version.ShouldBe(session.Version);
    }

    [Fact]
    public async Task Every_key_of_a_session_expires_after_the_idle_time()
    {
        var session = await CreatedAsync();
        await Store.TryReserveNameAsync(session.Code, "ava", "p1", Ct);
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct);
        await Store.TryClaimCardAsync(session.Code, AnyCard.Fingerprint, Ct);
        await Store.SaveTokenAsync(session.Code, "token-hash", Caller.Participant("p1"), Ct);

        foreach (var key in Keys(session))
        {
            var ttl = await redis.Db.KeyTimeToLiveAsync(key);
            ttl.ShouldNotBeNull($"{key} must expire");
            ttl.Value.ShouldBeInRange(TimeSpan.FromHours(4) - TimeSpan.FromMinutes(1), TimeSpan.FromHours(4));
        }
    }

    [Fact]
    public async Task A_session_past_its_lifetime_is_gone_however_busy()
    {
        var session = await CreatedAsync();
        _time.Now += TimeSpan.FromHours(12);

        (await Store.GetAsync(session.Code, Ct)).ShouldBeNull();

        (await redis.Db.KeyExistsAsync(Keys(session).ToArray())).ShouldBe(0, "its keys are deleted");
    }

    [Fact]
    public async Task An_expired_session_is_not_brought_back_by_a_save()
    {
        var session = RedisFixture.NewSession(_time.Now);

        await Store.SaveAsync(session, Ct);

        (await Store.GetAsync(session.Code, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Participants_load_as_fresh_copies_with_a_version()
    {
        var session = await CreatedAsync();
        (await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct)).ShouldBeTrue();

        var loaded = (await Store.GetParticipantAsync(session.Code, "p1", Ct))!;
        loaded.Participant.ResetForNewRound(session, () => new Card(AnyCard.Cells.Reverse())); // changed, never saved

        var again = (await Store.GetParticipantAsync(session.Code, "p1", Ct))!;

        loaded.Version.ShouldBe(1);
        again.Participant.Cards[0].Card.ShouldBe(AnyCard);
    }

    [Fact]
    public async Task A_participant_id_can_only_be_added_once()
    {
        var session = await CreatedAsync();
        (await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct)).ShouldBeTrue();

        (await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ben"), Ct)).ShouldBeFalse();

        (await Store.GetParticipantAsync(session.Code, "p1", Ct))!.Participant.Name.Display.ShouldBe("Ava");
    }

    [Fact]
    public async Task A_save_succeeds_only_at_the_loaded_version()
    {
        var session = await CreatedAsync();
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct);
        var first = (await Store.GetParticipantAsync(session.Code, "p1", Ct))!;
        var second = (await Store.GetParticipantAsync(session.Code, "p1", Ct))!;
        first.Participant.SetAutoFill(false, session).IsSuccess.ShouldBeTrue();

        (await Store.TryUpdateParticipantAsync(session.Code, first.Participant, first.Version, Ct)).ShouldBeTrue();
        (await Store.TryUpdateParticipantAsync(session.Code, second.Participant, second.Version, Ct)).ShouldBeFalse("it was loaded before the first save");

        (await Store.GetParticipantAsync(session.Code, "p1", Ct))!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Of_many_racing_saves_from_one_version_exactly_one_wins()
    {
        var session = await CreatedAsync();
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct);
        var loads = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Store.GetParticipantAsync(session.Code, "p1", Ct)));

        var saves = await Task.WhenAll(loads.Select(l => Store.TryUpdateParticipantAsync(session.Code, l!.Participant, l.Version, Ct)));

        saves.Count(saved => saved).ShouldBe(1);
    }

    [Fact]
    public async Task A_removed_participant_cannot_be_saved_back()
    {
        var session = await CreatedAsync();
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct);
        var loaded = (await Store.GetParticipantAsync(session.Code, "p1", Ct))!;

        await Store.DeleteParticipantAsync(session.Code, "p1", Ct);

        (await Store.TryUpdateParticipantAsync(session.Code, loaded.Participant, loaded.Version, Ct)).ShouldBeFalse();
        (await Store.GetParticipantAsync(session.Code, "p1", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Participants_list_in_joining_order()
    {
        var session = await CreatedAsync();
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p2", "Ben", minutes: 2), Ct);
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava", minutes: 1), Ct);

        (await Store.GetParticipantsAsync(session.Code, Ct)).Select(p => p.Participant.Id).ShouldBe(["p1", "p2"]);
        (await Store.CountParticipantsAsync(session.Code, Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task Removing_a_participant_revokes_only_their_token()
    {
        var session = await CreatedAsync();
        await Store.TryAddParticipantAsync(session.Code, Player(session, "p1", "Ava"), Ct);
        await Store.SaveTokenAsync(session.Code, "ava-token", Caller.Participant("p1"), Ct);
        await Store.SaveTokenAsync(session.Code, "ben-token", Caller.Participant("p2"), Ct);
        await Store.SaveTokenAsync(session.Code, "host-token", Caller.Host, Ct);

        await Store.DeleteParticipantAsync(session.Code, "p1", Ct);

        (await Store.FindTokenAsync(session.Code, "ava-token", Ct)).ShouldBeNull();
        (await Store.FindTokenAsync(session.Code, "ben-token", Ct)).ShouldBe(Caller.Participant("p2"));
        (await Store.FindTokenAsync(session.Code, "host-token", Ct)).ShouldBe(Caller.Host);
    }

    [Theory]
    [InlineData(CallerRole.Host, null)]
    [InlineData(CallerRole.Display, null)]
    [InlineData(CallerRole.Participant, "p1")]
    public async Task Tokens_resolve_to_their_caller(CallerRole role, string? participantId)
    {
        var session = await CreatedAsync();
        var caller = new Caller(role, participantId);

        await Store.SaveTokenAsync(session.Code, "hash", caller, Ct);

        (await Store.FindTokenAsync(session.Code, "hash", Ct)).ShouldBe(caller);
        (await Store.FindTokenAsync(session.Code, "unknown", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Of_many_joins_with_one_name_exactly_one_reserves_it()
    {
        var session = await CreatedAsync();

        var reserved = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Store.TryReserveNameAsync(session.Code, "ava", $"p{i}", Ct)));

        reserved.Count(r => r).ShouldBe(1);
        await Store.ReleaseNameAsync(session.Code, "ava", Ct);
        (await Store.TryReserveNameAsync(session.Code, "ava", "late", Ct)).ShouldBeTrue("a released name is free again");
    }

    [Fact]
    public async Task Of_many_deals_of_one_card_exactly_one_claims_it()
    {
        var session = await CreatedAsync();

        var claimed = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Store.TryClaimCardAsync(session.Code, AnyCard.Fingerprint, Ct)));

        claimed.Count(c => c).ShouldBe(1);
        await Store.ReleaseCardAsync(session.Code, AnyCard.Fingerprint, Ct);
        (await Store.TryClaimCardAsync(session.Code, AnyCard.Fingerprint, Ct)).ShouldBeTrue();
        await Store.ReleaseAllCardsAsync(session.Code, Ct);
        (await Store.TryClaimCardAsync(session.Code, AnyCard.Fingerprint, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_lock_lets_one_holder_in_at_a_time()
    {
        var store = redis.Store(TimeProvider.System);
        var session = RedisFixture.NewSession(_time.Now);
        var inside = 0;
        var most = 0;

        await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var held = await store.LockAsync(session.Code, Ct);
            most = Math.Max(most, Interlocked.Increment(ref inside));
            await Task.Delay(10, Ct);
            Interlocked.Decrement(ref inside);
        }));

        most.ShouldBe(1);
    }

    [Fact]
    public async Task Waiting_too_long_for_the_lock_times_out()
    {
        var store = redis.Store(TimeProvider.System, new RedisStoreOptions { LockWait = TimeSpan.FromMilliseconds(200) });
        var session = RedisFixture.NewSession(_time.Now);
        await using var held = await store.LockAsync(session.Code, Ct);

        await Should.ThrowAsync<TimeoutException>(() => store.LockAsync(session.Code, Ct));
    }

    [Fact]
    public async Task A_lock_whose_holder_died_frees_itself_and_the_late_release_leaves_the_new_holder_alone()
    {
        var store = redis.Store(TimeProvider.System, new RedisStoreOptions { LockLease = TimeSpan.FromMilliseconds(200) });
        var session = RedisFixture.NewSession(_time.Now);
        var dead = await store.LockAsync(session.Code, Ct); // never released in time

        var next = await store.LockAsync(session.Code, Ct);
        await dead.DisposeAsync();

        (await redis.Db.KeyExistsAsync($"bingo:s:{{{session.Code.Value}}}:lock")).ShouldBeTrue("the stale release must not free the new holder's lock");
        await next.DisposeAsync();
    }

    private async Task<Session> CreatedAsync()
    {
        var session = RedisFixture.NewSession(_time.Now);
        (await Store.TryCreateAsync(session, "hashed", Ct)).ShouldBeTrue();
        return session;
    }

    private Participant Player(Session session, string id, string name, int minutes = 0)
    {
        ParticipantName.TryCreate(name, out var parsed, out _).ShouldBeTrue();
        return Participant.Join(id, parsed, AnyCard, session, _time.Now.AddMinutes(minutes));
    }

    private static IEnumerable<RedisKey> Keys(Session session) =>
        KeySuffixes.Select(suffix => (RedisKey)$"bingo:s:{{{session.Code.Value}}}:{suffix}");
}
