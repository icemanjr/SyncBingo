using SyncBingo.Application.Ports;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Tests;

public sealed class CreateAndJoinTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Creating_a_game_stores_it_in_the_lobby_with_a_hashed_password_and_a_host_token()
    {
        var created = (await _app.Create.HandleAsync(new CreateSession(TestApp.Password), TestApp.Ct)).Value;

        (await _app.SessionAsync(created.Code)).State.ShouldBe(SessionState.BetweenRounds);
        (await _app.Store.GetPasswordHashAsync(created.Code, TestApp.Ct)).ShouldBe("hashed:secret");
        (await _app.Store.FindTokenAsync(created.Code, _app.Tokens.Hash(created.HostToken), TestApp.Ct)).ShouldBe(Caller.Host);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public async Task A_game_password_must_be_4_to_64_characters(string password)
    {
        var result = await _app.Create.HandleAsync(new CreateSession(password), TestApp.Ct);

        result.Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public async Task A_code_clash_just_means_trying_another_code()
    {
        var firstChoice = GameCode.Generate(new SeededRandom(1));
        _app.Store.TakenCodes.Add(firstChoice.Value);

        var created = (await _app.Create.HandleAsync(new CreateSession(TestApp.Password), TestApp.Ct)).Value;

        created.Code.ShouldNotBe(firstChoice);
    }

    [Fact]
    public async Task Joining_reserves_the_name_claims_a_unique_card_and_tells_the_host()
    {
        var code = await _app.NewGameAsync();

        var joined = (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "  Sam "), TestApp.Ct)).Value;

        var player = await _app.PlayerAsync(code, joined.ParticipantId);
        player.Name.Display.ShouldBe("Sam");
        _app.Store.ReservedNames(code).ShouldBe(["sam"]);
        _app.Store.CardsInPlay(code).ShouldBe([player.Cards[0].Card.Fingerprint]);
        (await _app.Store.FindTokenAsync(code, _app.Tokens.Hash(joined.Token), TestApp.Ct)).ShouldBe(Caller.Participant(joined.ParticipantId));
        _app.Publisher.Notifications.ShouldBe([(new Recipient.Host(), (Notification)new ParticipantJoined(joined.ParticipantId, "Sam"))]);
    }

    [Fact]
    public async Task An_offensive_name_is_refused_and_nothing_is_kept()
    {
        var code = await _app.NewGameAsync();

        var refused = await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Sh1t Head"), TestApp.Ct);

        refused.Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        refused.Error.Message.ShouldBe("Please choose a different name.");
        _app.Store.ReservedNames(code).ShouldBeEmpty("the name isn't reserved");
        _app.Publisher.Notifications.ShouldBeEmpty("and the host isn't told about it");
    }

    [Theory]
    [InlineData("9ve4-shp3-k7mw-a2qx")] // well-formed, but no such game
    [InlineData("not a code")]
    public async Task Unknown_games_are_not_found(string code)
    {
        var result = await _app.Join.HandleAsync(new JoinSession(code, TestApp.Password, "Sam"), TestApp.Ct);

        result.Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }

    [Fact]
    public async Task A_wrong_password_reserves_nothing()
    {
        var code = await _app.NewGameAsync();

        var result = await _app.Join.HandleAsync(new JoinSession(code.ToString(), "wrong", "Sam"), TestApp.Ct);

        result.Error!.Code.ShouldBe(DomainErrorCode.Unauthorized);
        _app.Store.ReservedNames(code).ShouldBeEmpty();
        _app.Store.CardsInPlay(code).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "Please enter a name.")]
    [InlineData("Sam!", "letters, numbers, spaces")]
    [InlineData("...", "at least one letter")]
    public async Task Bad_names_get_a_friendly_message(string name, string message)
    {
        var code = await _app.NewGameAsync();

        var error = (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, name), TestApp.Ct)).Error!;

        error.Code.ShouldBe(DomainErrorCode.Invalid);
        error.Message.ShouldContain(message);
    }

    [Fact]
    public async Task Names_are_unique_ignoring_case_and_spacing()
    {
        var code = await _app.NewGameAsync();
        await _app.JoinAsync(code, "Sam");

        var error = (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, " SAM "), TestApp.Ct)).Error!;

        error.Code.ShouldBe(DomainErrorCode.Conflict);
        error.Message.ShouldContain("SAM");
        _app.Store.CardsInPlay(code).Count.ShouldBe(1, "no card was claimed for the refused join");
    }

    [Fact]
    public async Task Two_players_racing_for_the_same_name_cannot_both_get_it()
    {
        var code = await _app.NewGameAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Sam"), TestApp.Ct)));

        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Where(r => !r.IsSuccess).ShouldAllBe(r => r.Error!.Code == DomainErrorCode.Conflict);
    }

    [Fact]
    public async Task Joining_mid_round_is_refused_and_reserves_nothing()
    {
        var code = await _app.NewGameAsync();
        await _app.StartRoundAsync(code);

        var result = await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Late"), TestApp.Ct);

        result.Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        _app.Store.ReservedNames(code).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_player_gets_a_different_card()
    {
        var code = await _app.NewGameAsync();

        foreach (var name in new[] { "Ava", "Ben", "Cal", "Dee", "Eli" })
        {
            await _app.JoinAsync(code, name);
        }

        _app.Store.CardsInPlay(code).Count.ShouldBe(5);
    }
}
