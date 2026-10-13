using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace SyncBingo.Domain.Participants;

/// <summary>Why a name was refused.</summary>
public enum NameProblem
{
    None,
    Empty,
    TooLong,
    InvalidCharacters,
    NoLetterOrDigit,
}

/// <summary>
/// A participant's display name and the key used to keep names unique within a
/// session (FR-4). "Sam", " sam " and "ＳＡＭ" are the same name.
/// </summary>
public sealed record ParticipantName
{
    public const int MaxLength = 20;

    /// <summary>
    /// Cap on UTF-16 code units, so stacked accent marks ("Zalgo" text) cannot make a
    /// name that counts as short but draws huge.
    /// </summary>
    private const int MaxCodeUnits = MaxLength * 3;

    private const string AllowedPunctuation = "'-_.";

    private ParticipantName(string display, string key)
    {
        Display = display;
        Key = key;
    }

    /// <summary>The name as shown to everyone: trimmed, single-spaced, Unicode-normalised.</summary>
    public string Display { get; }

    /// <summary>The lowercase form compared for uniqueness.</summary>
    public string Key { get; }

    /// <summary>
    /// Validates and normalises a name. Allowed: letters in any script (with accents),
    /// digits, spaces and <c>' - _ .</c>, with at least one letter or digit; 1–20
    /// characters as a reader would count them.
    /// </summary>
    public static bool TryCreate(
        string? input,
        [NotNullWhen(true)] out ParticipantName? name,
        out NameProblem problem)
    {
        name = null;

        // NFKC folds look-alike forms (full-width letters, ligatures, composed vs. decomposed accents) together.
        var display = CollapseWhitespace((input ?? string.Empty).Normalize(NormalizationForm.FormKC));

        problem = display switch
        {
            "" => NameProblem.Empty,
            _ when !display.EnumerateRunes().All(IsAllowed) => NameProblem.InvalidCharacters,
            _ when !display.EnumerateRunes().Any(Rune.IsLetterOrDigit) => NameProblem.NoLetterOrDigit,
            _ when display.Length > MaxCodeUnits => NameProblem.TooLong,
            _ when new StringInfo(display).LengthInTextElements > MaxLength => NameProblem.TooLong,
            _ => NameProblem.None,
        };

        if (problem != NameProblem.None)
        {
            return false;
        }

        name = new ParticipantName(display, display.ToLowerInvariant());
        return true;
    }

    public override string ToString() => Display;

    // Checked per code point (Rune), not per char, so emoji and other surrogate pairs are judged whole.
    private static bool IsAllowed(Rune rune) =>
        rune.Value == ' '
        || (rune.IsBmp && AllowedPunctuation.Contains((char)rune.Value, StringComparison.Ordinal))
        || Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.DecimalDigitNumber;

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
