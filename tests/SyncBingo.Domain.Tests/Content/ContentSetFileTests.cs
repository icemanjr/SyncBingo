using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;
using static SyncBingo.Domain.Tests.Content.ContentTestKit;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>The set file a host downloads and uploads again (ADR 0009): groups in order, each with its pairs.</summary>
public sealed class ContentSetFileTests
{
    /// <summary>Standard Bingo written as a set: five groups, B to O, of fifteen numbers each.</summary>
    private static string NormalBingo()
    {
        var groups = "BINGO".Select((letter, column) =>
        {
            var pairs = string.Join(", ", Enumerable.Range((column * 15) + 1, 15).Select(number => $"[\"{number}\"]"));
            return $$"""{ "name": "{{letter}}", "pairs": [{{pairs}}] }""";
        });
        return $$"""{ "format": 1, "name": "Normal Bingo", "groups": [{{string.Join(", ", groups)}}] }""";
    }

    [Fact]
    public void Standard_Bingo_as_a_set_file_plays_like_standard_Bingo()
    {
        var set = ContentSetFile.Read(NormalBingo()).Value;

        set.Name.ShouldBe("Normal Bingo");
        set.Groups.ShouldBe(["B", "I", "N", "G", "O"]);
        set[37].ShouldBe(new ContentPair("37", "37", "N"));

        var variant = ContentVariant.For(StandardVariants.Classic75, set).Value;
        variant.Columns.ShouldBe(StandardVariants.Classic75.Columns);
    }

    [Fact]
    public void A_written_set_reads_back_the_same()
    {
        var set = Valid(TimesTables(), "Times tables 1–5");

        var back = ContentSetFile.Read(ContentSetFile.Write(set));

        back.IsSuccess.ShouldBeTrue(back.Error?.Message);
        back.Value.Name.ShouldBe("Times tables 1–5");
        back.Value.Pairs.ShouldBe(set.Pairs);
        back.Value.Groups.ShouldBe(set.Groups);
    }

    [Fact]
    public void The_file_is_laid_out_like_the_card_one_pair_to_a_line()
    {
        var entries = Words(22).Select(entry => entry with { Group = "words" }).ToList();
        entries.Add(new ContentEntry("2 × 3", "6"));
        entries.Add(new ContentEntry("loose"));

        var json = ContentSetFile.Write(Valid(entries, "Mixed"));

        json.ShouldStartWith("""
            {
              "format": 1,
              "name": "Mixed",
              "groups": [
                {
                  "name": "words",
                  "label": "W",
                  "pairs": [
                    ["word 1"],
                    ["word 2"],
            """.ReplaceLineEndings("\n"));
        json.ShouldEndWith("""
              ],
              "pairs": [
                ["2 × 3", "6"],
                ["loose"]
              ]
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Groups_keep_the_order_they_are_listed_in_including_empty_ones()
    {
        var pairs = string.Join(", ", Enumerable.Range(1, 24).Select(i => $"[\"w{i}\"]"));
        var json = $$"""
            { "format": 1, "groups": [
                { "name": "later", "pairs": [] },
                { "name": "words", "pairs": [{{pairs}}] }
            ] }
            """;

        var set = ContentSetFile.Read(json).Value;

        set.Groups.ShouldBe(["later", "words"]);
        ContentSetFile.Read(ContentSetFile.Write(set)).Value.Groups.ShouldBe(["later", "words"]);
    }

    [Fact]
    public void A_set_without_groups_is_just_its_pairs()
    {
        var pairs = string.Join(", ", Enumerable.Range(1, 24).Select(i => $"[\"{i} + 1\", \"{i + 1}\"]"));

        var set = ContentSetFile.Read($$"""{ "format": 1, "name": "Adding one", "pairs": [{{pairs}}], "comment": "ignored" }""");

        set.IsSuccess.ShouldBeTrue(set.Error?.Message);
        set.Value[3].ShouldBe(new ContentPair("3 + 1", "4", null));
        set.Value.Groups.ShouldBeEmpty();
        ContentSetFile.Write(set.Value).ShouldNotContain("\"groups\"");
    }

    [Fact]
    public void A_groups_short_label_is_kept_and_written_back()
    {
        var json = NormalBingo().Replace("\"name\": \"N\"", "\"name\": \"N\", \"label\": \"Mid\"", StringComparison.Ordinal);

        var set = ContentSetFile.Read(json).Value;

        set.Labels.ShouldBe(["B", "I", "Mid", "G", "O"]);
        ContentSetFile.Write(set).ShouldContain("\"label\": \"Mid\"");
        ContentSetFile.Read(ContentSetFile.Write(set)).Value.Labels.ShouldBe(set.Labels);
    }

    [Fact]
    public void The_free_spaces_text_is_kept_and_written_back_after_the_name()
    {
        var json = NormalBingo().Replace("\"name\": \"Normal Bingo\"", "\"name\": \"Normal Bingo\", \"free\": \"★ Week 5\"", StringComparison.Ordinal);

        var set = ContentSetFile.Read(json).Value;

        set.FreeText.ShouldBe("★ Week 5");
        ContentSetFile.Write(set).ShouldStartWith("{\n  \"format\": 1,\n  \"name\": \"Normal Bingo\",\n  \"free\": \"★ Week 5\",\n");
        ContentSetFile.Read(ContentSetFile.Write(set)).Value.FreeText.ShouldBe("★ Week 5");
        ContentSetFile.Write(ContentSetFile.Read(NormalBingo()).Value).ShouldNotContain("\"free\"");
    }

    [Fact]
    public void The_free_spaces_text_must_be_text() =>
        ContentSetFile.Read(NormalBingo().Replace("\"format\": 1", "\"format\": 1, \"free\": 7", StringComparison.Ordinal))
            .Error!.Message.ShouldStartWith("This is not a SyncBingo set file.");

    [Theory]
    [InlineData("\"label\": 5")]
    [InlineData("\"label\": null")]
    public void A_label_must_be_text(string label) =>
        ContentSetFile.Read(NormalBingo().Replace("\"name\": \"B\"", $"\"name\": \"B\", {label}", StringComparison.Ordinal))
            .Error!.Message.ShouldStartWith("This is not a SyncBingo set file.");

    [Fact]
    public void Two_groups_with_the_same_name_are_refused()
    {
        var json = NormalBingo().Replace("\"name\": \"I\"", "\"name\": \"b\"", StringComparison.Ordinal);

        ContentSetFile.Read(json).Error!.Message.ShouldContain("Two groups are called 'b'");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "name": "no format", "pairs": [] }""")]
    [InlineData("""{ "format": 1, "name": "no pairs or groups" }""")]
    [InlineData("""{ "format": 1, "pairs": [ { "ball": "the old format" } ] }""")]
    [InlineData("""{ "format": 1, "pairs": [ [] ] }""")]
    [InlineData("""{ "format": 1, "pairs": [ ["a", "b", "c"] ] }""")]
    [InlineData("""{ "format": 1, "pairs": [ [1] ] }""")]
    [InlineData("""{ "format": 1, "pairs": [ null ] }""")]
    [InlineData("""{ "format": 1, "groups": [ { "pairs": [] } ] }""")]
    [InlineData("""{ "format": 1, "groups": [ { "name": "no pairs" } ] }""")]
    [InlineData("""{ "format": 1, "groups": { "name": "not a list" } }""")]
    [InlineData("""{ "format": 1, "name": 7, "pairs": [] }""")]
    [InlineData("""{ "format": "1", "pairs": [] }""")]
    [InlineData("""{ "format": 1.5, "pairs": [] }""")]
    [InlineData("""{ "format": 0, "pairs": [] }""")]
    public void Anything_else_is_not_a_set_file(string json) =>
        ContentSetFile.Read(json).Error!.Message.ShouldStartWith("This is not a SyncBingo set file.");

    [Fact]
    public void A_file_from_a_newer_version_says_so() =>
        ContentSetFile.Read("""{ "format": 2, "pairs": [] }""").Error!.Message.ShouldContain("newer version");

    [Fact]
    public void A_valid_file_still_has_to_be_a_valid_set() =>
        ContentSetFile.Read("""{ "format": 1, "pairs": [ ["only one"] ] }""")
            .Error!.Message.ShouldContain("A set needs 8–500 entries, not 1");

    [Fact]
    public void Files_over_64_KB_are_refused_before_they_are_read()
    {
        var json = """{ "format": 1, "pairs": [], "padding": " """ + new string(' ', ContentSetFile.MaxBytes) + "\" }";

        ContentSetFile.Read(json).Error!.Message.ShouldBe("A set file can be at most 64 KB.");
    }

    [Fact]
    public void A_full_set_of_typical_entries_fits_in_a_file()
    {
        // 500 pairs with a 60-character definition on the ball and a short word on the square, in 5 groups.
        var entries = Enumerable.Range(1, 500).Select(i =>
            new ContentEntry($"{i:000} {new string('d', 56)}", $"word {i:000}", $"group {i % 5}"));

        var json = ContentSetFile.Write(Valid(entries));

        System.Text.Encoding.UTF8.GetByteCount(json).ShouldBeLessThan(ContentSetFile.MaxBytes);
        ContentSetFile.Read(json).IsSuccess.ShouldBeTrue();
    }
}
