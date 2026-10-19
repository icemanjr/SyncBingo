using SyncBingo.Domain.Content;
using static SyncBingo.Domain.Tests.Content.ContentTestKit;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>The set file a host downloads and uploads again (ADR 0009).</summary>
public sealed class ContentSetFileTests
{
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
    public void The_file_is_readable_and_leaves_out_what_it_can()
    {
        var entries = Words(23);
        entries.Add(new ContentEntry("2 × 3", "6"));

        var json = ContentSetFile.Write(Valid(entries, "Mixed"));

        json.ShouldContain("\"format\": 1");
        json.ShouldContain("\"ball\": \"2 × 3\""); // not ×
        json.ShouldContain("{\n      \"ball\": \"word 1\"\n    }"); // a matched entry has no square, and no group
    }

    [Fact]
    public void A_hand_written_file_works()
    {
        var pairs = string.Join(",", Enumerable.Range(1, 24).Select(i => $$"""{ "ball": "{{i}} + 1", "square": "{{i + 1}}" }"""));
        var json = $$"""{ "format": 1, "name": "Adding one", "pairs": [{{pairs}}], "comment": "ignored" }""";

        var set = ContentSetFile.Read(json);

        set.IsSuccess.ShouldBeTrue(set.Error?.Message);
        set.Value[3].ShouldBe(new ContentPair("3 + 1", "4", null));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "name": "no format", "pairs": [] }""")]
    [InlineData("""{ "format": 1, "name": "no pairs" }""")]
    [InlineData("""{ "format": 1, "pairs": [ { "square": "no ball" } ] }""")]
    [InlineData("""{ "format": 1, "pairs": [ null ] }""")]
    [InlineData("""{ "format": "1", "pairs": [] }""")]
    [InlineData("""{ "format": 0, "pairs": [] }""")]
    public void Anything_else_is_not_a_set_file(string json) =>
        ContentSetFile.Read(json).Error!.Message.ShouldStartWith("This isn't a SyncBingo set file.");

    [Fact]
    public void A_file_from_a_newer_version_says_so() =>
        ContentSetFile.Read("""{ "format": 2, "pairs": [] }""").Error!.Message.ShouldContain("newer version");

    [Fact]
    public void A_valid_file_still_has_to_be_a_valid_set() =>
        ContentSetFile.Read("""{ "format": 1, "pairs": [ { "ball": "only one" } ] }""")
            .Error!.Message.ShouldContain("A set needs 24–500 entries, not 1");

    [Fact]
    public void Files_over_64_KB_are_refused_before_they_are_read()
    {
        var json = """{ "format": 1, "pairs": [], "padding": " """ + new string(' ', ContentSetFile.MaxBytes) + "\" }";

        ContentSetFile.Read(json).Error!.Message.ShouldBe("A set file can be at most 64 KB.");
    }

    [Fact]
    public void The_largest_set_fits_in_a_file()
    {
        // 500 pairs with a 120-character ball, a 15-character square and a group each: the worst case.
        var entries = Enumerable.Range(1, 500).Select(i =>
            new ContentEntry($"{i:000} {new string('b', 116)}", $"{i:000} {new string('s', 11)}", $"group {i % 10}"));

        var json = ContentSetFile.Write(Valid(entries));

        System.Text.Encoding.UTF8.GetByteCount(json).ShouldBeLessThan(ContentSetFile.MaxBytes * 2);
        ContentSetFile.Read(json).IsSuccess.ShouldBeFalse("this worst case is over 64 KB, so the limit really binds");
    }
}
