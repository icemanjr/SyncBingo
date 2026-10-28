using System.Globalization;
using System.Text;

namespace SyncBingo.Domain.Content;

/// <summary>
/// How the text on a ball or square is tidied and measured (ADR 0009). The web editor applies the same
/// rules for instant feedback; the server's check is the one that counts.
/// </summary>
public static class ContentText
{
    /// <summary>
    /// Trimmed, single-spaced (tabs and line breaks become spaces) and Unicode-normalized (NFKC), so
    /// "７" and "7", or a composed and a decomposed "é", are the same text.
    /// </summary>
    public static string Tidy(string? value)
    {
        var text = (value ?? string.Empty).Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text.Trim())
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

    /// <summary>Characters as a reader counts them: "é" and "👍🏽" are one each.</summary>
    public static int Length(string text) => new StringInfo(text).LengthInTextElements;

    /// <summary>
    /// Whether tidied text fits <paramref name="maxLength"/> characters. Also capped at three UTF-16 code
    /// units a character, so stacked accent marks ("Zalgo" text) can't make text that counts as short but
    /// draws huge.
    /// </summary>
    public static bool Fits(string text, int maxLength) =>
        text.Length <= maxLength * 3 && Length(text) <= maxLength;

    /// <summary>
    /// Printable text only: no control or formatting characters (bar the zero-width joiner inside emoji),
    /// no private-use or unassigned code points. Letters, digits, punctuation, symbols such as × and ÷,
    /// and emoji are all fine.
    /// </summary>
    public static bool IsPrintable(string text) =>
        text.EnumerateRunes().All(rune => rune.Value == 0x200D || Rune.GetUnicodeCategory(rune) is not (
            UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.PrivateUse or
            UnicodeCategory.Surrogate or
            UnicodeCategory.OtherNotAssigned or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator));

    /// <summary>The form compared for uniqueness: "Cat" and "cat" are the same square.</summary>
    public static string Key(string text) => text.ToLowerInvariant();
}
