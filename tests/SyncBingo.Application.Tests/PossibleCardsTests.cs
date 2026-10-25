using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Application.Tests;

/// <summary>
/// A small grouped set makes few different cards (FR-39): 3, 2 and 3 pairs on a 3 × 3 card make 72. Once
/// every one is held, no one else can join, and a redeal that would need more is refused before it starts.
/// </summary>
public sealed class PossibleCardsTests
{
    private readonly TestApp _app = new();

    /// <summary>Eight pairs in groups of 3, 2 and 3: one card column each, with the free space in the middle.</summary>
    private const string EightGrouped = """
        { "format": 1, "name": "Tiny", "groups": [
          { "name": "a", "pairs": [ ["a1"], ["a2"], ["a3"] ] },
          { "name": "b", "pairs": [ ["b1"], ["b2"] ] },
          { "name": "c", "pairs": [ ["c1"], ["c2"], ["c3"] ] } ] }
        """;

    [Fact]
    public async Task Once_all_72_cards_are_held_the_next_player_is_told_there_are_none_left()
    {
        var code = await _app.NewGameAsync();
        (await _app.Content.UseAsync(code, Caller.Host, EightGrouped, CardSize.ThreeByThree, TestApp.Ct)).IsSuccess.ShouldBeTrue();
        for (var i = 1; i <= 72; i++)
        {
            await _app.JoinAsync(code, $"Player {i}");
        }

        var refused = await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Late"), TestApp.Ct);

        refused.Error!.Code.ShouldBe(DomainErrorCode.SessionFull);
        refused.Error.Message.ShouldBe("No unique cards available: all 72 possible cards are in use. The host can add entries to the set.");
        (await _app.Join.HandleAsync(new JoinSession(code.ToString(), TestApp.Password, "Late"), TestApp.Ct)).Error!.Message
            .ShouldStartWith("No unique cards available", Case.Sensitive, "the name was released, so it isn't taken");
    }

    [Fact]
    public async Task A_set_that_makes_fewer_cards_than_the_players_need_changes_nothing()
    {
        var code = await _app.NewGameAsync();
        var first = await _app.JoinAsync(code, "Player 1");
        for (var i = 2; i <= 73; i++)
        {
            await _app.JoinAsync(code, $"Player {i}");
        }

        var card = (await _app.PlayerAsync(code, first)).Cards[0].Card;
        var saves = _app.Store.Saves;

        var refused = await _app.Content.UseAsync(code, Caller.Host, EightGrouped, CardSize.ThreeByThree, TestApp.Ct);

        refused.Error!.Message.ShouldBe("These rules make only 72 different cards, and the players need 73. Add entries to the set.");
        _app.Store.Saves.ShouldBe(saves, "the game is not saved with the set");
        (await _app.PlayerAsync(code, first)).Cards[0].Card.ShouldBe(card, "players keep their cards");
    }
}
