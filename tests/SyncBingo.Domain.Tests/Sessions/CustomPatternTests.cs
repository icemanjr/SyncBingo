using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>Patterns the host draws (FR-21a): added for the game, never deleted, chosen like any other.</summary>
public sealed class CustomPatternTests
{
    /// <summary>A plus sign on a 5 × 5 card: the middle row and the middle column.</summary>
    private static readonly int[] Plus = [2, 7, 10, 11, 12, 13, 14, 17, 22];

    [Fact]
    public void A_drawn_pattern_is_offered_and_can_be_chosen()
    {
        var session = NewSession();

        session.AddPattern(" Plus  sign ", Plus, Now).Value.ShouldHaveSingleItem().ShouldBeOfType<PatternsChanged>();

        var plus = session.Variant.GetPattern("custom-1");
        plus.Name.ShouldBe("Plus sign");
        plus.Masks.ShouldBe([StandardVariants.Classic75.Layout.ParseShape("..X..", "..X..", "XXXXX", "..X..", "..X..")]);
        session.ApplyRules(session.Settings, "custom-1+corners", Now).IsSuccess.ShouldBeTrue();
        session.Selection.Patterns.Select(pattern => pattern.Name).ShouldBe(["Four Corners", "Plus sign"]);
    }

    [Fact]
    public void A_pattern_can_be_added_mid_round_without_changing_what_wins()
    {
        var session = InState(SessionState.Manual);

        session.AddPattern("Plus", Plus, Now).IsSuccess.ShouldBeTrue();

        session.WinConditionId.ShouldBe(Normal);
    }

    [Theory]
    [InlineData("", "Give the pattern a name of up to 20 characters.")]
    [InlineData("a name far too long for it", "Give the pattern a name of up to 20 characters.")]
    [InlineData("four corners", "There is already a pattern called 'four corners'.")]
    public void A_pattern_needs_its_own_name(string name, string problem) =>
        NewSession().AddPattern(name, Plus, Now).Error!.Message.ShouldBe(problem);

    [Fact]
    public void A_pattern_needs_squares_on_the_card_besides_the_free_space()
    {
        var session = NewSession();

        session.AddPattern("Nothing", [], Now).Error!.Message.ShouldBe("Pick at least one square besides the free space.");
        session.AddPattern("Center", [12], Now).Error!.Message.ShouldBe("Pick at least one square besides the free space.");
        session.AddPattern("Off the card", [24, 25], Now).Error!.Message.ShouldBe("Every square of the pattern must be on the card.");
    }

    [Fact]
    public void A_shape_the_game_already_has_is_refused()
    {
        var session = NewSession();

        session.AddPattern("Corners again", [0, 4, 20, 24], Now).Error!.Message.ShouldBe("That is the same shape as 'Four Corners'.");
        session.AddPattern("Plus", Plus, Now);
        session.AddPattern("Plus again", Plus, Now).Error!.Message.ShouldBe("That is the same shape as 'Plus'.");
    }

    [Fact]
    public void A_game_has_at_most_five_of_its_own()
    {
        var session = NewSession();
        for (var i = 0; i < 5; i++)
        {
            session.AddPattern($"Pattern {i}", [i, i + 5], Now).IsSuccess.ShouldBeTrue();
        }

        session.AddPattern("One more", [0, 1], Now).Error!.Message.ShouldBe("A game can have up to 5 patterns of its own.");
    }

    [Fact]
    public void A_pattern_belongs_to_the_card_size_it_was_drawn_on()
    {
        var session = NewSession(SessionSettings.Default with { CardSize = CardSize.ThreeByThree });
        session.AddPattern("Top and middle", [0, 1, 2, 3, 5], Now).IsSuccess.ShouldBeTrue();
        session.ApplyRules(session.Settings, "custom-1", Now).IsSuccess.ShouldBeTrue();

        session.ChangeSettings(SessionSettings.Default, Now).Error!.Message
            .ShouldBe("A 5 × 5 card has no Top and middle: choose another pattern too.");
        session.ApplyRules(SessionSettings.Default, "normal", Now).IsSuccess.ShouldBeTrue();
        session.Variant.TryGetPattern("custom-1", out _).ShouldBeFalse();

        session.ApplyRules(SessionSettings.Default with { CardSize = CardSize.ThreeByThree }, "custom-1", Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_stored_game_keeps_its_patterns_and_selection()
    {
        var session = NewSession();
        session.AddPattern("Plus", Plus, Now);
        session.ApplyRules(session.Settings, "custom-1", Now);

        var restored = Session.Restore(session.ToSnapshot());

        restored.CustomPatterns.ShouldHaveSingleItem().Name.ShouldBe("Plus");
        restored.Selection.Patterns.ShouldHaveSingleItem().Id.ShouldBe("custom-1");
    }

    [Fact]
    public void An_ended_game_takes_no_more_patterns() =>
        InState(SessionState.Ended).AddPattern("Plus", Plus, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);

    [Fact]
    public void A_mistake_is_renamed_rather_than_deleted_and_keeps_its_id()
    {
        var session = NewSession();
        session.AddPattern("Plus", Plus, Now);
        session.ApplyRules(session.Settings, "custom-1", Now);

        session.RenamePattern("custom-1", "OLD - Plus", Now).Value.ShouldHaveSingleItem().ShouldBeOfType<PatternsChanged>();

        session.Variant.GetPattern("custom-1").Name.ShouldBe("OLD - Plus");
        session.WinConditionId.ShouldBe("custom-1");
        session.AddPattern("Plus", [2, 7, 12, 17, 22, 10, 11], Now).IsSuccess.ShouldBeTrue("the old name is free again");
    }

    [Fact]
    public void A_rename_follows_the_naming_rules_and_only_the_hosts_own_patterns_can_be_renamed()
    {
        var session = NewSession();
        session.AddPattern("Plus", Plus, Now);

        session.RenamePattern("custom-1", "PLUS", Now).IsSuccess.ShouldBeTrue("its own name, in other letters");
        session.RenamePattern("custom-1", "Blackout", Now).Error!.Message.ShouldBe("There is already a pattern called 'Blackout'.");
        session.RenamePattern("custom-1", " ", Now).Error!.Message.ShouldBe("Give the pattern a name of up to 20 characters.");
        session.RenamePattern("corners", "Edges", Now).Error!.Code.ShouldBe(DomainErrorCode.NotFound);
    }
}
