using SyncBingo.Domain.Participants;

namespace SyncBingo.Domain.Tests.Participants;

public sealed class ParticipantNameTests
{
    [Theory]
    [InlineData("Sam", "Sam")]
    [InlineData("  Sam  ", "Sam")]
    [InlineData("Mary   Kate", "Mary Kate")]
    [InlineData("Mary\tKate", "Mary Kate")]
    [InlineData("O'Brien", "O'Brien")]
    [InlineData("Mary-Kate", "Mary-Kate")]
    [InlineData("Dr. Who", "Dr. Who")]
    [InlineData("player_2", "player_2")]
    [InlineData("Zoë", "Zoë")]
    [InlineData("Søren", "Søren")]
    [InlineData("李雷", "李雷")]
    [InlineData("Ａｖａ", "Ava")]
    public void Accepts_and_tidies_ordinary_names(string typed, string display)
    {
        Create(typed).Display.ShouldBe(display);
    }

    [Theory]
    [InlineData("Sam", "sam")]
    [InlineData("SAM", " sam ")]
    [InlineData("Sam", "ＳＡＭ")]
    [InlineData("Mary Kate", "mary   kate")]
    [InlineData("José", "José")] // é composed vs. e + combining accent
    public void Names_that_look_the_same_share_a_key(string first, string second)
    {
        Create(second).Key.ShouldBe(Create(first).Key);
    }

    [Fact]
    public void Different_names_have_different_keys()
    {
        Create("Sam").Key.ShouldNotBe(Create("Sammy").Key);
    }

    [Fact]
    public void Allows_exactly_twenty_characters()
    {
        Create(new string('a', 20)).Display.Length.ShouldBe(20);
    }

    [Fact]
    public void Counts_characters_as_a_reader_sees_them()
    {
        // 20 letters, each written as base letter + combining accent (40 UTF-16 units).
        var accented = string.Concat(Enumerable.Repeat("é", 20)).Normalize(System.Text.NormalizationForm.FormD);

        ParticipantName.TryCreate(accented, out _, out var problem).ShouldBeTrue(problem.ToString());
    }

    [Theory]
    [InlineData(null, NameProblem.Empty)]
    [InlineData("", NameProblem.Empty)]
    [InlineData("   ", NameProblem.Empty)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa", NameProblem.TooLong)]
    [InlineData("Sam😀", NameProblem.InvalidCharacters)]
    [InlineData("Sam!", NameProblem.InvalidCharacters)]
    [InlineData("<script>", NameProblem.InvalidCharacters)]
    [InlineData("Sam​", NameProblem.InvalidCharacters)] // zero-width space
    [InlineData("Sam‮", NameProblem.InvalidCharacters)] // right-to-left override
    [InlineData("Sam\u0007", NameProblem.InvalidCharacters)] // control character
    [InlineData("...", NameProblem.NoLetterOrDigit)]
    [InlineData("- _ '", NameProblem.NoLetterOrDigit)]
    public void Refuses_names_that_break_the_rules(string? typed, NameProblem expected)
    {
        ParticipantName.TryCreate(typed, out var name, out var problem).ShouldBeFalse();
        name.ShouldBeNull();
        problem.ShouldBe(expected);
    }

    [Fact]
    public void Injection_looking_words_are_just_text()
    {
        // Harmless: names are never spliced into queries, commands or markup (docs/02 §7a).
        Create("DROP TABLE").Display.ShouldBe("DROP TABLE");
    }

    [Theory]
    [InlineData("Robert'); DROP TABLE Students;--")]
    [InlineData("\"; FLUSHALL")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("{{constructor}}")]
    [InlineData("=HYPERLINK(\"x\")")]
    public void Injection_syntax_is_refused_by_the_character_rules(string typed)
    {
        ParticipantName.TryCreate(typed, out _, out var problem).ShouldBeFalse();
        problem.ShouldBe(NameProblem.InvalidCharacters);
    }

    [Fact]
    public void Line_breaks_cannot_forge_extra_log_lines()
    {
        Create("Sam\nWARN Sam won\r\n").Display.ShouldBe("Sam WARN Sam won");
    }

    [Fact]
    public void Refuses_stacked_accent_marks_that_would_draw_huge()
    {
        var zalgo = "S" + new string('̶', 100) + "am";

        ParticipantName.TryCreate(zalgo, out _, out var problem).ShouldBeFalse();
        problem.ShouldBe(NameProblem.TooLong);
    }

    private static ParticipantName Create(string typed)
    {
        ParticipantName.TryCreate(typed, out var name, out var problem).ShouldBeTrue(problem.ToString());
        return name;
    }
}
