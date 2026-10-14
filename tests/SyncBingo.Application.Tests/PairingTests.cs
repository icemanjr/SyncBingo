using SyncBingo.Application.Displays;
using SyncBingo.Application.Pairing;
using SyncBingo.Application.Ports;
using SyncBingo.Domain;

namespace SyncBingo.Application.Tests;

/// <summary>Connecting a TV with the short code it shows (ADR 0010).</summary>
public sealed class PairingTests
{
    private readonly TestApp _app = new();
    private readonly InMemoryPairingStore _pairings;
    private readonly RecordingPairingNotifier _notifier = new();
    private readonly DisplayHandler _displays;
    private readonly PairingHandler _handler;

    public PairingTests()
    {
        _pairings = new InMemoryPairingStore(_app.Clock);
        _displays = new DisplayHandler(_app.Store, _app.Tokens, _app.Publisher, _app.Clock);
        _handler = new PairingHandler(_pairings, _notifier, _displays, new SeededRandom(9), _app.Clock);
    }

    [Fact]
    public async Task A_tv_gets_a_six_character_code_for_ten_minutes()
    {
        var started = await _handler.StartAsync(TestApp.Ct);

        started.Code.Value.Length.ShouldBe(6);
        started.ExpiresAt.ShouldBe(_app.Clock.UtcNow + TimeSpan.FromMinutes(10));
        started.PairingId.Length.ShouldBe(64, "unguessable: only the TV knows it");
    }

    [Fact]
    public async Task The_host_approving_the_code_gives_the_tv_a_read_only_display_token()
    {
        var code = await _app.NewGameAsync();
        var tv = await _handler.StartAsync(TestApp.Ct);

        var displayId = (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString().ToLowerInvariant(), TestApp.Ct)).Value;

        var (pairingId, result) = _notifier.Paired.ShouldHaveSingleItem();
        pairingId.ShouldBe(tv.PairingId);
        result.SessionCode.ShouldBe(code.Value);
        (await _app.Store.FindTokenAsync(code, _app.Tokens.Hash(result.DisplayToken), TestApp.Ct)).ShouldBe(Caller.Display(displayId));
        (await _displays.ListAsync(code, Caller.Host, TestApp.Ct)).Value.ShouldHaveSingleItem().Kind.ShouldBe(DisplayKind.Tv);
    }

    [Fact]
    public async Task A_code_works_once()
    {
        var code = await _app.NewGameAsync();
        var tv = await _handler.StartAsync(TestApp.Ct);
        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task A_code_expires_after_ten_minutes()
    {
        var code = await _app.NewGameAsync();
        var tv = await _handler.StartAsync(TestApp.Ct);
        _app.Clock.UtcNow += TimeSpan.FromMinutes(10);

        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).Error!.Message
            .ShouldBe("That code isn't valid or has expired. Check the code on the TV.");
        _notifier.Paired.ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_the_host_can_approve_and_a_refusal_leaves_the_code_usable()
    {
        var code = await _app.NewGameAsync();
        var player = Caller.Participant(await _app.JoinAsync(code, "Ava"));
        var tv = await _handler.StartAsync(TestApp.Ct);

        (await _handler.ApproveAsync(code, player, tv.Code.ToString(), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);

        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_full_game_refuses_without_using_up_the_code()
    {
        var code = await _app.NewGameAsync();
        for (var i = 0; i < DisplayHandler.MaxDisplays; i++)
        {
            (await _displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        }

        var tv = await _handler.StartAsync(TestApp.Ct);

        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Conflict);
        (await _pairings.TryClaimAsync(tv.Code, TestApp.Ct)).ShouldBe(tv.PairingId, "still waiting for another try");
    }

    [Fact]
    public async Task A_tv_that_connects_late_still_collects_its_result_once()
    {
        var code = await _app.NewGameAsync();
        var tv = await _handler.StartAsync(TestApp.Ct);
        (await _handler.ApproveAsync(code, Caller.Host, tv.Code.ToString(), TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _pairings.TakeResultAsync(tv.PairingId, TestApp.Ct))!.SessionCode.ShouldBe(code.Value);
        (await _pairings.TakeResultAsync(tv.PairingId, TestApp.Ct)).ShouldBeNull();
    }
}
