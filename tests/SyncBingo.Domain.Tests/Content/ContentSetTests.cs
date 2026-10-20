using SyncBingo.Domain.Content;
using static SyncBingo.Domain.Tests.Content.ContentTestKit;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>What the server checks before a host's set is used (FR-44, ADR 0009).</summary>
public sealed class ContentSetTests
{
    [Fact]
    public void A_word_list_shows_each_word_on_ball_and_square()
    {
        var set = Valid(Words(24));

        set.Pairs.Length.ShouldBe(24);
        set[1].ShouldBe(new ContentPair("word 1", "word 1", null));
        set[1].IsMatched.ShouldBeTrue();
        set.Groups.ShouldBeEmpty();
        set.IsFullyGrouped.ShouldBeFalse();
    }

    [Fact]
    public void A_pair_can_call_one_thing_and_mark_another()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("2 + 5", "7"));

        var pair = Valid(entries)[24];

        pair.Ball.ShouldBe("2 + 5");
        pair.Square.ShouldBe("7");
        pair.IsMatched.ShouldBeFalse();
    }

    [Fact]
    public void An_empty_square_means_the_ball_text_is_on_the_square_too()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("tree", "  "));

        Valid(entries)[24].Square.ShouldBe("tree");
    }

    [Fact]
    public void Text_is_tidied_before_it_is_checked()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("  carbon\tdioxide ", null, " gases "));

        var pair = Valid(entries)[1]; // the only grouped entry comes first

        pair.Ball.ShouldBe("carbon dioxide");
        pair.Group.ShouldBe("gases");
    }

    [Theory]
    [InlineData(23)]
    [InlineData(501)]
    [InlineData(0)]
    public void A_set_has_24_to_500_entries(int count) =>
        Refusal(Words(count)).ShouldContain($"A set needs 24–500 entries, not {count}");

    [Theory]
    [InlineData(24)]
    [InlineData(500)]
    public void The_limits_themselves_are_allowed(int count) =>
        Valid(Words(count)).Pairs.Length.ShouldBe(count);

    [Fact]
    public void Square_text_is_at_most_15_characters()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("a long definition is fine on the ball", "fifteen letters"));
        Valid(entries)[24].Square.Length.ShouldBe(15);

        entries[23] = new ContentEntry("too long", "sixteen letters!");
        Refusal(entries).ShouldContain("Square 24 is longer than 15 characters");
    }

    [Fact]
    public void A_matched_entry_must_fit_a_square()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("Mississippi River"));

        Refusal(entries).ShouldContain("Square 24 is longer than 15 characters");
    }

    [Fact]
    public void Ball_text_is_at_most_120_characters()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry(new string('d', 120), "ok"));
        Valid(entries);

        entries[23] = new ContentEntry(new string('d', 121), "ok");
        Refusal(entries).ShouldContain("Ball 24 is longer than 120 characters");
    }

    [Fact]
    public void No_two_squares_show_the_same_text_whatever_the_case()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("Something else", "WORD 3"));

        Refusal(entries).ShouldContain("'WORD 3' is on two squares (entries 3 and 24)");
    }

    [Fact]
    public void No_two_balls_call_the_same_text()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("word 5", "another"));

        Refusal(entries).ShouldContain("'word 5' is on two balls (entries 5 and 24)");
    }

    [Fact]
    public void Text_that_cant_be_shown_is_refused()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("ring\u0007", "ok"));

        Refusal(entries).ShouldContain("Ball 24 has characters that can't be shown");
    }

    [Fact]
    public void Empty_balls_are_refused()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("   ", "seven"));

        Refusal(entries).ShouldContain("Ball 24 is empty");
    }

    [Fact]
    public void Groups_keep_the_order_they_first_appear_in_and_match_regardless_of_case()
    {
        var entries = TimesTables();
        entries[1] = entries[1] with { Group = "×1".ToUpperInvariant() };

        var set = Valid(entries);

        set.Groups.ShouldBe(["×1", "×2", "×3", "×4", "×5"]);
        set.IsFullyGrouped.ShouldBeTrue();
    }

    [Fact]
    public void The_hosts_group_order_wins_and_groups_named_only_by_entries_follow()
    {
        var set = ContentSet.TryCreate("ordered", TimesTables(), ["×5", "×4", "empty"]).Value;

        set.Groups.ShouldBe(["×5", "×4", "empty", "×1", "×2", "×3"]);
        set.Pairs[0].Group.ShouldBe("×5");
        set.Pairs[^1].Group.ShouldBe("×3");
    }

    [Theory]
    [InlineData("Biology", "B")]
    [InlineData("inventions & tools", "I")]
    [InlineData("×2", "×")]
    [InlineData("élan", "É")]
    [InlineData("🎉 party", "🎉")]
    public void A_group_without_a_label_is_labelled_with_its_first_letter(string group, string label) =>
        ContentSet.TryCreate("labels", Words(24), [group]).Value.LabelOf(group).ShouldBe(label);

    [Fact]
    public void A_host_can_choose_a_label_of_up_to_five_characters()
    {
        var set = ContentSet.TryCreate("labels", TimesTables(), [new ContentGroup("×1", "1×"), new ContentGroup("×2", " Twos ")]).Value;

        set.LabelOf("×1").ShouldBe("1×");
        set.LabelOf("×2").ShouldBe("Twos");
        set.LabelOf("×3").ShouldBe("×", "a group named only by its entries gets the default");
    }

    [Fact]
    public void A_label_longer_than_five_characters_is_refused() =>
        ContentSet.TryCreate("labels", TimesTables(), [new ContentGroup("×1", "Ones!!")])
            .Error!.Message.ShouldContain("Group '×1's label must be printable text of up to 5 characters");

    [Fact]
    public void A_blank_label_means_the_default() =>
        ContentSet.TryCreate("labels", TimesTables(), [new ContentGroup("×1", "  ")]).Value.LabelOf("×1").ShouldBe("×");

    [Fact]
    public void Listed_group_names_are_checked_too()
    {
        ContentSet.TryCreate("dupes", Words(24), ["Week 1", "week 1"]).Error!.Message.ShouldContain("Two groups are called 'week 1'");
        ContentSet.TryCreate("blank", Words(24), ["  "]).Error!.Message.ShouldContain("Group names must be printable text of 1–20 characters");
    }

    [Fact]
    public void A_set_with_some_entries_ungrouped_is_not_fully_grouped()
    {
        var entries = TimesTables();
        entries[0] = entries[0] with { Group = null };

        Valid(entries).IsFullyGrouped.ShouldBeFalse();
    }

    [Fact]
    public void At_most_ten_groups()
    {
        var entries = Words(24).Select((entry, i) => entry with { Group = $"g{i % 11}" });

        Refusal(entries).ShouldContain("A set can have at most 10 groups, not 11");
    }

    [Fact]
    public void Group_names_are_at_most_20_characters()
    {
        var entries = Words(24);
        entries[0] = entries[0] with { Group = new string('g', 21) };

        Refusal(entries).ShouldContain("Entry 1's group must be printable text of up to 20 characters");
    }

    [Fact]
    public void A_set_without_a_name_is_called_Custom_set() =>
        Valid(Words(24), name: "  ").Name.ShouldBe("Custom set");

    [Fact]
    public void A_set_name_is_at_most_60_characters() =>
        Refusal(Words(24), name: new string('n', 61)).ShouldContain("up to 60 characters");

    [Fact]
    public void A_refusal_names_the_first_three_problems_and_counts_the_rest()
    {
        var entries = Words(24);
        for (var i = 0; i < 5; i++)
        {
            entries[i] = new ContentEntry($"ball {i}", new string('x', 16 + i));
        }

        var message = Refusal(entries);

        message.ShouldStartWith("Square 1 is longer than 15 characters; Square 2");
        message.ShouldEndWith("(and 2 more).");
    }

    [Fact]
    public void Entries_round_trip_with_matched_squares_left_out()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("2 + 5", "7", "sums"));

        var back = Valid(entries).ToEntries().ToList();

        back[0].ShouldBe(new ContentEntry("2 + 5", "7", "sums")); // grouped entries come first
        back[1].ShouldBe(new ContentEntry("word 1", null, null));
    }
}
