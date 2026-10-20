using SyncBingo.Domain.Content;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>The sample sets in <c>samples/</c> (for tests and screenshots) stay valid sets.</summary>
public sealed class SampleSetTests
{
    private static string Samples()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "SyncBingo.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir.FullName, "samples");
    }

    public static TheoryData<string> Files()
    {
        var files = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Samples(), "*.json"))
        {
            files.Add(Path.GetFileName(path));
        }

        return files;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_sample_is_a_valid_set(string file)
    {
        var set = ContentSetFile.Read(File.ReadAllText(Path.Combine(Samples(), file)));

        set.IsSuccess.ShouldBeTrue(set.Error?.Message);
    }

    [Fact]
    public void Science_vocabulary_plays_like_BINGO_by_group()
    {
        var set = ContentSetFile.Read(File.ReadAllText(Path.Combine(Samples(), "science-vocabulary.json"))).Value;

        set.Groups.ShouldBe(["Biology", "Inventions & Tools", "Numbers & Shapes", "Geology & Earth", "Objects & Forces"]);
        set.Labels.ShouldBe(["B", "I", "N", "G", "O"], "each group's first letter, so the card's columns read B-I-N-G-O");
        set.Pairs.ShouldAllBe(pair => !pair.IsMatched, "a clue on the ball, the word on the square");
        foreach (var group in set.Groups)
        {
            set.Pairs.Count(pair => pair.Group == group).ShouldBe(15, group);
        }

        ContentVariant.For(StandardVariants.Classic75, set, groupItems: true).IsSuccess.ShouldBeTrue();
    }
}
