using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;
using static SyncBingo.Domain.Tests.Content.ContentTestKit;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>Playing a custom set on the standard card (ADR 0009): by group when it has groups, otherwise free-form.</summary>
public sealed class ContentVariantTests
{
    private static readonly BingoVariant Standard = StandardVariants.Classic75;

    [Fact]
    public void A_word_list_is_dealt_free_form_with_one_ball_per_entry()
    {
        var variant = ContentVariant.For(Standard, Valid(Words(40))).Value;

        variant.Id.ShouldBe("custom");
        variant.IsFreeForm.ShouldBeTrue();
        variant.BallMax.ShouldBe(40);
        variant.LabelOf(17).ShouldBe(string.Empty);
        CardGenerators.For(variant).ShouldBeOfType<FreeFormCardGenerator>();
    }

    [Fact]
    public void The_card_and_patterns_stay_the_standard_ones()
    {
        var variant = ContentVariant.For(Standard, Valid(Words(40))).Value;

        variant.Layout.ShouldBe(Standard.Layout);
        variant.Patterns.ShouldBe(Standard.Patterns);
        variant.Presets.ShouldBe(Standard.Presets);
    }

    [Fact]
    public void Grouped_items_deal_each_column_from_one_group()
    {
        var variant = ContentVariant.For(Standard, Valid(TimesTables())).Value;

        variant.IsFreeForm.ShouldBeFalse();
        variant.Columns.ShouldBe([new("×1", 1, 5), new("×2", 6, 10), new("×3", 11, 15), new("×4", 16, 20), new("×5", 21, 25)]);
        variant.LabelOf(7).ShouldBe("×2");
        CardGenerators.For(variant).ShouldBeOfType<ColumnCardGenerator>();
    }

    [Fact]
    public void A_set_keeps_each_groups_entries_together_whatever_order_they_were_typed_in()
    {
        var shuffled = TimesTables().OrderBy(entry => entry.Ball.Length).ThenByDescending(entry => entry.Ball).ToList();

        var set = Valid(shuffled);

        set.Pairs.Select(pair => pair.Group).Distinct().ShouldBe(set.Groups);
        set.Pairs.Select(pair => pair.Group).ShouldBe(set.Groups.SelectMany(group => Enumerable.Repeat(group, 5)));
    }

    [Fact]
    public void Ungrouped_entries_come_after_the_groups()
    {
        var entries = TimesTables();
        entries.Insert(0, new ContentEntry("extra"));

        var set = Valid(entries);

        set.Pairs[^1].Ball.ShouldBe("extra");
    }

    [Fact]
    public void The_same_set_without_its_groups_is_dealt_free_form_with_the_same_item_ids()
    {
        var set = Valid(TimesTables());

        var plain = set.WithoutGroups();

        ContentVariant.For(Standard, plain).Value.IsFreeForm.ShouldBeTrue();
        plain.Groups.ShouldBeEmpty();
        plain.Pairs.Select(pair => pair.Ball).ShouldBe(set.Pairs.Select(pair => pair.Ball));
    }

    [Fact]
    public void A_set_with_groups_needs_every_entry_in_a_group()
    {
        var entries = TimesTables();
        entries.Add(new ContentEntry("loose"));

        Refusal(Valid(entries)).ShouldBe("A set with groups needs every entry in a group.");
    }

    [Fact]
    public void A_set_with_groups_needs_one_group_per_column()
    {
        var entries = TimesTables().Select(entry => entry with { Group = entry.Group == "×5" ? "×4" : entry.Group });

        Refusal(Valid(entries)).ShouldBe("A set with groups needs 5 groups, one for each card column; this set has 4.");
    }

    [Fact]
    public void Each_group_must_fill_its_column()
    {
        // ×1 has 4 entries: enough only for the middle column, which holds the free space.
        var entries = TimesTables();
        entries.RemoveAt(0);
        entries.Add(new ContentEntry("spare", null, "×5"));

        Refusal(Valid(entries)).ShouldBe("Group '×1' has 4 entries; its column needs at least 5.");
    }

    [Fact]
    public void The_middle_column_needs_one_fewer_for_the_free_space()
    {
        var entries = TimesTables();
        entries.RemoveAll(entry => entry.Ball == "3 × 3"); // ×3 is the middle column
        entries.Add(new ContentEntry("spare", null, "×5"));

        ContentVariant.For(Standard, Valid(entries)).IsSuccess.ShouldBeTrue();
    }

    private static string Refusal(ContentSet set)
    {
        var result = ContentVariant.For(Standard, set);
        result.IsSuccess.ShouldBeFalse();
        return result.Error!.Message;
    }
}
