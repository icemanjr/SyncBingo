using System.Text.RegularExpressions;
using FsCheck.Xunit;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Domain.Tests.Sessions;

public sealed partial class GameCodeTests
{
    private static readonly GameCode Sample = GameCode.Parse("9ve4shp3k7mwa2qx");

    [Fact]
    public void Alphabet_has_31_symbols_and_no_look_alikes()
    {
        GameCode.Alphabet.Length.ShouldBe(31);
        GameCode.Alphabet.ShouldBeUnique();
        foreach (var lookAlike in "01ilo")
        {
            GameCode.Alphabet.ShouldNotContain(lookAlike);
        }
    }

    [Property(MaxTest = 500)]
    public void Generated_codes_use_16_alphabet_characters_and_display_in_four_groups(int seed)
    {
        var code = GameCode.Generate(new SeededRandom(seed));

        code.Value.Length.ShouldBe(GameCode.Length);
        code.Value.ShouldAllBe(c => GameCode.Alphabet.Contains(c));
        DisplayFormat().IsMatch(code.ToString()).ShouldBeTrue(code.ToString());
    }

    [Property(MaxTest = 500)]
    public void The_display_form_parses_back_to_the_same_code(int seed)
    {
        var code = GameCode.Generate(new SeededRandom(seed));

        GameCode.Parse(code.ToString()).ShouldBe(code);
    }

    [Fact]
    public void Codes_do_not_repeat_in_a_large_sample()
    {
        var random = new SeededRandom(2026);

        var codes = Enumerable.Range(0, 100_000).Select(_ => GameCode.Generate(random).Value);

        codes.ShouldBeUnique();
    }

    [Fact]
    public void Every_alphabet_character_is_equally_likely()
    {
        var random = new SeededRandom(17);
        var counts = new Dictionary<char, int>();
        for (var i = 0; i < 20_000; i++)
        {
            foreach (var c in GameCode.Generate(random).Value)
            {
                counts[c] = counts.GetValueOrDefault(c) + 1;
            }
        }

        var expected = 20_000 * GameCode.Length / GameCode.Alphabet.Length;
        counts.Count.ShouldBe(GameCode.Alphabet.Length);
        counts.Values.ShouldAllBe(count => count > expected * 0.9 && count < expected * 1.1);
    }

    [Theory]
    [InlineData("9ve4-shp3-k7mw-a2qx")]
    [InlineData("9ve4shp3k7mwa2qx")]
    [InlineData("9VE4-SHP3-K7MW-A2QX")]
    [InlineData(" 9ve4 shp3 k7mw a2qx ")]
    [InlineData("9ve4-shp3 k7mw-A2qx")]
    public void Accepts_codes_as_people_type_them(string typed)
    {
        GameCode.TryParse(typed, out var code).ShouldBeTrue();
        code.ShouldBe(Sample);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9ve4-shp3-k7mw-a2q")]
    [InlineData("9ve4-shp3-k7mw-a2qxx")]
    [InlineData("9ve4-shp3-k7mw-a2q0")]
    [InlineData("9ve4-shp3-k7mw-a2qo")]
    [InlineData("9ve4-shp3-k7mw-a2q1")]
    [InlineData("9ve4-shp3-k7mw-a2ql")]
    [InlineData("9ve4-shp3-k7mw-a2q!")]
    public void Rejects_anything_else(string? typed)
    {
        GameCode.TryParse(typed, out var code).ShouldBeFalse();
        code.ShouldBeNull();
    }

    [Fact]
    public void Parse_explains_an_invalid_code()
    {
        Should.Throw<FormatException>(() => GameCode.Parse("nope"));
    }

    [Fact]
    public void Value_is_the_compact_lowercase_form()
    {
        GameCode.Parse("9VE4-SHP3-K7MW-A2QX").Value.ShouldBe("9ve4shp3k7mwa2qx");
    }

    [GeneratedRegex("^[2-9a-hjkmnp-z]{4}(-[2-9a-hjkmnp-z]{4}){3}$")]
    private static partial Regex DisplayFormat();
}
