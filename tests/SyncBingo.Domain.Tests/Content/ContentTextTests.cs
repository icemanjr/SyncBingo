using SyncBingo.Domain.Content;

namespace SyncBingo.Domain.Tests.Content;

public sealed class ContentTextTests
{
    [Theory]
    [InlineData("  cat  ", "cat")]
    [InlineData("carbon\t\tdioxide", "carbon dioxide")]
    [InlineData("line\nbreak", "line break")]
    [InlineData("７", "7")] // full-width digit
    [InlineData("José", "José")] // e + combining accent becomes é
    [InlineData(null, "")]
    public void Tidies_spacing_and_lookalike_forms(string? typed, string tidy) =>
        ContentText.Tidy(typed).ShouldBe(tidy);

    [Theory]
    [InlineData("cat", 3)]
    [InlineData("Zoë", 3)]
    [InlineData("👍🏽", 1)] // an emoji with a skin tone is one character
    [InlineData("👨‍👩‍👧", 1)] // so is a family joined by zero-width joiners
    public void Counts_characters_as_a_reader_sees_them(string text, int length) =>
        ContentText.Length(text).ShouldBe(length);

    [Fact]
    public void Stacked_accent_marks_dont_sneak_past_a_limit()
    {
        var zalgo = "a" + new string('\u0301', 60); // one "character" carrying 60 accents

        ContentText.Length(zalgo).ShouldBe(1);
        ContentText.Fits(zalgo, 15).ShouldBeFalse();
    }

    [Theory]
    [InlineData("2 × 3 ÷ 4 = ½")]
    [InlineData("¿Qué?")]
    [InlineData("李雷")]
    [InlineData("🎉 party")]
    [InlineData("👨‍👩‍👧")]
    public void Symbols_scripts_and_emoji_are_printable(string text) =>
        ContentText.IsPrintable(text).ShouldBeTrue();

    [Theory]
    [InlineData("bell\u0007")]
    [InlineData("right‮left")] // a bidi override, which can disguise text
    [InlineData("hidden​space")] // zero-width space
    [InlineData("privateuse")]
    public void Control_and_formatting_characters_are_not(string text) =>
        ContentText.IsPrintable(text).ShouldBeFalse();
}
