using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Participants;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>An award sent twice with the same Idempotency-Key (a double click) counts once.</summary>
public sealed class AwardKeyTests
{
    private static readonly SessionSettings EveryWin = SessionSettings.Default with { MultipleWins = MultipleWins.EveryDeclaredWin };

    [Fact]
    public void The_same_key_awards_once_even_where_repeats_would_count()
    {
        var kit = new PlayerTestKit(EveryWin);

        kit.Session.AwardWin(kit.Player, 1, Now, requestKey: "click-1").Single<WinAwarded>();
        kit.Session.AwardWin(kit.Player, 1, Now, requestKey: "click-1").Value.ShouldBeEmpty();

        kit.Session.Leaderboard().ShouldHaveSingleItem().Wins.ShouldBe(1);
    }

    [Fact]
    public void A_new_key_is_a_new_award()
    {
        var kit = new PlayerTestKit(EveryWin);

        kit.Session.AwardWin(kit.Player, 1, Now, requestKey: "click-1").IsSuccess.ShouldBeTrue();
        kit.Session.AwardWin(kit.Player, 1, Now, requestKey: "click-2").IsSuccess.ShouldBeTrue();

        kit.Session.Leaderboard().ShouldHaveSingleItem().Wins.ShouldBe(2);
    }

    [Fact]
    public void Keys_survive_a_restore()
    {
        var kit = new PlayerTestKit(EveryWin);
        kit.Session.AwardWin(kit.Player, 1, Now, requestKey: "click-1").IsSuccess.ShouldBeTrue();

        var restored = Session.Restore(kit.Session.ToSnapshot());

        restored.AwardWin(kit.Player, 1, Now, requestKey: "click-1").Value.ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_most_recent_keys_are_kept()
    {
        var kit = new PlayerTestKit(EveryWin);
        for (var i = 0; i <= Session.RememberedRequestKeys; i++)
        {
            kit.Session.AwardWin(kit.Player, 1, Now, requestKey: $"click-{i}").IsSuccess.ShouldBeTrue();
        }

        var keys = kit.Session.ToSnapshot().AwardKeys!;
        keys.Count.ShouldBe(Session.RememberedRequestKeys);
        keys.ShouldNotContain("click-0");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_key_must_have_some_length_to_count(string? key)
    {
        var kit = new PlayerTestKit();

        var result = kit.Session.AwardWin(kit.Player, 1, Now, requestKey: key);

        (key is null ? result.IsSuccess : result.Error!.Code == DomainErrorCode.Invalid).ShouldBeTrue();
    }
}
