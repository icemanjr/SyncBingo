using SyncBingo.Application.Displays;
using SyncBingo.Application.Ports;
using SyncBingo.Application.Queries;
using SyncBingo.Domain;

namespace SyncBingo.Application.Tests;

/// <summary>Display views (FR-28) and the join note (FR-49).</summary>
public sealed class DisplayTests
{
    private readonly TestApp _app = new();

    private DisplayHandler Displays => new(_app.Store, _app.Tokens, _app.Publisher, _app.Clock);

    [Fact]
    public async Task A_display_link_signs_in_as_a_read_only_display()
    {
        var code = await _app.NewGameAsync();

        var issued = (await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct)).Value;

        var caller = await _app.Store.FindTokenAsync(code, _app.Tokens.Hash(issued.Token), TestApp.Ct);
        caller.ShouldBe(Caller.Display(issued.DisplayId));
        (await _app.Draw.GrabBallAsync(code, caller!, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await _app.Players.MarkAsync(code, caller!, 1, 0, true, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }

    [Fact]
    public async Task Only_the_host_manages_displays()
    {
        var code = await _app.NewGameAsync();
        var player = Caller.Participant(await _app.JoinAsync(code, "Ava"));

        (await Displays.CreateLinkAsync(code, player, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await Displays.ListAsync(code, player, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
        (await Displays.CreateLinkAsync(code, Caller.Display("d1"), TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }

    [Fact]
    public async Task Disconnecting_a_display_revokes_its_token_and_tells_the_screen()
    {
        var code = await _app.NewGameAsync();
        var first = (await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct)).Value;
        var second = (await Displays.IssueAsync(code, Caller.Host, DisplayKind.Tv, TestApp.Ct)).Value;

        (await Displays.DisconnectAsync(code, Caller.Host, first.DisplayId, TestApp.Ct)).IsSuccess.ShouldBeTrue();

        (await _app.Store.FindTokenAsync(code, _app.Tokens.Hash(first.Token), TestApp.Ct)).ShouldBeNull();
        (await _app.Store.FindTokenAsync(code, _app.Tokens.Hash(second.Token), TestApp.Ct)).ShouldNotBeNull("the other display stays");
        _app.Publisher.Notifications.ShouldContain(n => n.Recipient == new Recipient.Display(first.DisplayId) && n.Notification is DisplayDisconnected);
        (await Displays.ListAsync(code, Caller.Host, TestApp.Ct)).Value.ShouldHaveSingleItem().Kind.ShouldBe(DisplayKind.Tv);
    }

    [Fact]
    public async Task Revoking_links_leaves_paired_tvs()
    {
        var code = await _app.NewGameAsync();
        await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct);
        await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct);
        await Displays.IssueAsync(code, Caller.Host, DisplayKind.Tv, TestApp.Ct);

        (await Displays.RevokeLinksAsync(code, Caller.Host, TestApp.Ct)).Value.ShouldBe(2);

        (await Displays.ListAsync(code, Caller.Host, TestApp.Ct)).Value.ShouldHaveSingleItem().Kind.ShouldBe(DisplayKind.Tv);
    }

    [Fact]
    public async Task A_game_has_at_most_ten_displays()
    {
        var code = await _app.NewGameAsync();
        for (var i = 0; i < DisplayHandler.MaxDisplays; i++)
        {
            (await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        }

        (await Displays.CreateLinkAsync(code, Caller.Host, TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Conflict);
    }

    [Fact]
    public async Task Disconnecting_an_unknown_display_is_not_found()
    {
        var code = await _app.NewGameAsync();

        (await Displays.DisconnectAsync(code, Caller.Host, "nope", TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task The_join_note_reaches_the_host_and_display_snapshots_but_not_players()
    {
        var code = await _app.NewGameAsync();
        var ava = Caller.Participant(await _app.JoinAsync(code, "Ava"));
        (await _app.Host.SetJoinNoteAsync(code, Caller.Host, "Password: harvest2026", TestApp.Ct)).IsSuccess.ShouldBeTrue();
        var queries = new GameQueries(_app.Store);

        (await queries.SnapshotAsync(code, Caller.Host, TestApp.Ct)).Value.Game.JoinNote.ShouldBe("Password: harvest2026");
        (await queries.SnapshotAsync(code, Caller.Display("d1"), TestApp.Ct)).Value.Game.JoinNote.ShouldBe("Password: harvest2026");
        (await queries.SnapshotAsync(code, ava, TestApp.Ct)).Value.Game.JoinNote.ShouldBeNull();
        (await _app.Host.SetJoinNoteAsync(code, ava, "Hacked", TestApp.Ct)).Error!.Code.ShouldBe(DomainErrorCode.Forbidden);
    }
}
