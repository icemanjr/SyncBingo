using System.Globalization;
using System.Text;

namespace SyncBingo.Domain.Participants;

/// <summary>
/// Keeps offensive player names out of a game (roadmap Phase 2): a name that uses one is refused with
/// "Please choose a different name.", and nothing is stored or shown. Names are compared after undoing the
/// usual disguises: case, accents, look-alike digits and symbols ("sh1t", "$hit"), separators ("f.u.c.k",
/// "f u c k") and stretched letters ("fuuuck").
/// <para>
/// Two lists, to avoid blocking innocent names (the "Scunthorpe problem"): <see cref="Anywhere"/> words are
/// unmistakable wherever they appear in a name; <see cref="WholeWord"/> words also occur inside ordinary
/// names (the "ass" in Cassandra, "cock" in Hancock), so they count only as a word of their own. A starting
/// list, not a complete one; it's data, so it can grow without changing the logic.
/// </para>
/// </summary>
public static class NameFilter
{
    private static readonly string[] Anywhere =
    [
        "fuck", "shit", "cunt", "nigger", "nigga", "faggot", "whore", "slut", "bitch", "pussy", "bastard",
        "asshole", "retard", "wank", "twat", "kike", "tranny", "hitler", "porn", "penis", "vagina", "dildo",
        "jizz", "rapist", "motherf", "bollock", "cocksuck", "blowjob", "handjob", "nazis",
    ];

    private static readonly string[] WholeWord =
    [
        "ass", "arse", "dick", "cock", "fag", "rape", "spic", "chink", "dyke", "nazi", "cum", "anal", "sex",
        "tit", "tits", "boob", "boobs", "piss", "crap", "damn", "hell", "homo", "coon", "gook", "wop", "slag",
    ];

    /// <summary>False when the name contains a word the filter blocks.</summary>
    public static bool IsAllowed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // The whole name with separators removed catches "f.u.c.k" and "a s s" as well as plain words.
        var squashed = Squash(name);
        var squashedCollapsed = Collapse(squashed);
        if (Anywhere.Any(word => squashed.Contains(word, StringComparison.Ordinal) || squashedCollapsed.Contains(Collapse(word), StringComparison.Ordinal)))
        {
            return false;
        }

        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Squash).Append(squashed);
        return !words.Any(word => WholeWord.Any(blocked => Matches(word, blocked)));
    }

    /// <summary>A word matches as written, or with stretched letters collapsed ("asss"), unless that would make it too short to mean anything.</summary>
    private static bool Matches(string word, string blocked) =>
        word == blocked || (Collapse(blocked).Length >= 3 && Collapse(word) == Collapse(blocked));

    /// <summary>Lower case, accents removed, look-alikes turned back into letters, everything else dropped.</summary>
    private static string Squash(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var letter = char.ToLowerInvariant(c) switch
            {
                '0' => 'o',
                '1' or '!' or '|' => 'i',
                '3' => 'e',
                '4' or '@' => 'a',
                '5' or '$' => 's',
                '7' or '+' => 't',
                '8' => 'b',
                var other => other,
            };
            if (letter is >= 'a' and <= 'z')
            {
                builder.Append(letter);
            }
        }

        return builder.ToString();
    }

    /// <summary>Runs of one letter become one: "fuuuck" → "fuck", "asss" → "as".</summary>
    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (builder.Length == 0 || builder[^1] != c)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
