using System.Collections.Immutable;
using System.Globalization;

namespace SyncBingo.Domain.Content;

/// <summary>
/// One entry as a host types it or a set file holds it: the ball (what is called), the square (what is
/// marked; <c>null</c> when it is the same as the ball) and, optionally, the group it belongs to.
/// </summary>
public sealed record ContentEntry(string Ball, string? Square = null, string? Group = null);

/// <summary>
/// A group as the host arranged it: its name and, optionally, a short label of up to 5 characters for the
/// card's column headers (e.g. "B" for "Biology"). Without a label, the name's first letter is used.
/// </summary>
public sealed record ContentGroup(string Name, string? Label = null)
{
    public static implicit operator ContentGroup(string name) => new(name);
}

/// <summary>One validated pair of a set: its call face and card face (ADR 0009), and its group, if any.</summary>
public sealed record ContentPair(string Ball, string Square, string? Group)
{
    /// <summary>The ball and the square show the same text, as in a word list.</summary>
    public bool IsMatched => Ball == Square;
}

/// <summary>
/// A host's custom content set (FR-44, ADR 0009): ball and square pairs of text, with optional named
/// groups. Item ids are positions in <see cref="Pairs"/> plus one, as balls are numbered from 1. Pairs are
/// kept grouped: each group's entries together, in group order, then any ungrouped ones, so every group is a
/// run of item ids.
/// <para>
/// Built only through <see cref="TryCreate"/>, which checks everything the server must before a set is
/// used: the limits, printable text, and that each side is unique, so a call can match only one square
/// and a card never shows the same square twice. Card size, the free space and patterns are session
/// settings, independent of the set; whether the set fits the current card is checked when it is used.
/// </para>
/// </summary>
public sealed class ContentSet
{
    public const int MinPairs = 24;
    public const int MaxPairs = 500;
    public const int MaxBallLength = 120;
    public const int MaxSquareLength = 15;
    /// <summary>Groups are card columns, so a set has at most as many as the widest card (ADR 0009).</summary>
    public const int MaxGroups = 5;
    public const int MaxGroupNameLength = 20;
    public const int MaxLabelLength = 5;
    public const int MaxNameLength = 60;

    /// <summary>The most problems one refusal lists; a file full of mistakes says how many more there are.</summary>
    private const int ProblemsShown = 3;

    private ContentSet(string name, ImmutableArray<ContentPair> pairs, ImmutableArray<string> groups, ImmutableArray<string> labels)
    {
        Name = name;
        Pairs = pairs;
        Groups = groups;
        Labels = labels;
    }

    /// <summary>What the host called the set, e.g. "Times tables 1–3".</summary>
    public string Name { get; }

    public ImmutableArray<ContentPair> Pairs { get; }

    /// <summary>The group names in the order they first appear; empty when the set has no groups.</summary>
    public ImmutableArray<string> Groups { get; }

    /// <summary>Each group's short label for card column headers, in the order of <see cref="Groups"/>.</summary>
    public ImmutableArray<string> Labels { get; }

    /// <summary>A group's short label.</summary>
    public string LabelOf(string group) => Labels[Groups.IndexOf(group)];

    /// <summary>The label a group gets when none is given: the first letter of its name, upper case ("Biology" → "B").</summary>
    public static string DefaultLabel(string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Length == 0 ? string.Empty : StringInfo.GetNextTextElement(group).ToUpperInvariant();
    }

    /// <summary>Every pair belongs to a group, so the set can be dealt by group (Group items).</summary>
    public bool IsFullyGrouped => !Groups.IsEmpty && Pairs.All(pair => pair.Group is not null);

    /// <summary>The pair for an item id (1-based, like ball numbers).</summary>
    public ContentPair this[int itemId] => Pairs[itemId - 1];

    /// <summary>
    /// Tidies and checks a whole set. On refusal the message names the first few problems, e.g.
    /// "Square 3 is longer than 15 characters; 'cat' is on two squares (entries 4 and 9)".
    /// <paramref name="groupOrder"/> lists the groups in the order the host arranged them (the set file's
    /// order), including any still empty; groups named only by entries follow, in the order they appear.
    /// </summary>
    public static Result<ContentSet> TryCreate(string? name, IEnumerable<ContentEntry> entries, IEnumerable<ContentGroup>? groupOrder = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var problems = new List<string>();
        var setName = ContentText.Tidy(name);
        if (setName.Length == 0)
        {
            setName = "Custom set";
        }
        else if (!ContentText.Fits(setName, MaxNameLength) || !ContentText.IsPrintable(setName))
        {
            problems.Add($"The set's name must be printable text of up to {MaxNameLength} characters");
        }

        var list = entries.ToList();
        if (list.Count is < MinPairs or > MaxPairs)
        {
            problems.Add($"A set needs {MinPairs}–{MaxPairs} entries, not {list.Count}");
        }

        var pairs = ImmutableArray.CreateBuilder<ContentPair>(list.Count);
        var groups = new List<string>();
        var groupKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var listed in groupOrder ?? [])
        {
            var group = ContentText.Tidy(listed.Name);
            var label = ContentText.Tidy(listed.Label);
            if (label.Length > 0 && (!ContentText.Fits(label, MaxLabelLength) || !ContentText.IsPrintable(label)))
            {
                problems.Add($"Group '{group}'s label must be printable text of up to {MaxLabelLength} characters");
            }
            else if (label.Length > 0)
            {
                labels[group] = label;
            }

            if (group.Length == 0 || !ContentText.Fits(group, MaxGroupNameLength) || !ContentText.IsPrintable(group))
            {
                problems.Add($"Group names must be printable text of 1–{MaxGroupNameLength} characters");
            }
            else if (!groupKeys.TryAdd(ContentText.Key(group), group))
            {
                problems.Add($"Two groups are called '{group}'");
            }
            else
            {
                groups.Add(group);
            }
        }

        var balls = new Dictionary<string, int>(StringComparer.Ordinal);
        var squares = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < list.Count; i++)
        {
            var number = i + 1;
            var ball = ContentText.Tidy(list[i].Ball);
            var square = list[i].Square is null ? ball : ContentText.Tidy(list[i].Square);
            if (square.Length == 0)
            {
                square = ball;
            }

            var group = list[i].Group is null ? null : ContentText.Tidy(list[i].Group);
            if (group?.Length == 0)
            {
                group = null;
            }

            Check(ball, "Ball", number, MaxBallLength, problems);
            Check(square, "Square", number, MaxSquareLength, problems);
            Unique(balls, ball, number, "ball", problems);
            Unique(squares, square, number, "square", problems);

            if (group is not null)
            {
                // Group names match regardless of case, and keep the spelling they first appear with.
                if (groupKeys.TryGetValue(ContentText.Key(group), out var existing))
                {
                    group = existing;
                }
                else if (!ContentText.Fits(group, MaxGroupNameLength) || !ContentText.IsPrintable(group))
                {
                    problems.Add($"Entry {number}'s group must be printable text of up to {MaxGroupNameLength} characters");
                }
                else
                {
                    groupKeys.Add(ContentText.Key(group), group);
                    groups.Add(group);
                }
            }

            pairs.Add(new ContentPair(ball, square, group));
        }

        if (groups.Count > MaxGroups)
        {
            problems.Add($"A set can have at most {MaxGroups} groups, not {groups.Count}");
        }

        if (problems.Count > 0)
        {
            var shown = string.Join("; ", problems.Take(ProblemsShown));
            var more = problems.Count > ProblemsShown ? $" (and {problems.Count - ProblemsShown} more)" : string.Empty;
            return Result.Failure<ContentSet>(DomainErrorCode.Invalid, $"{shown}{more}.");
        }

        // Each group's entries together, groups in order, ungrouped entries last; within that, the host's order.
        // A group is then a run of item ids, as a B-I-N-G-O column is a range of numbers (ADR 0009).
        var order = groups.Select((group, index) => (group, index)).ToDictionary(g => g.group, g => g.index, StringComparer.Ordinal);
        var ordered = pairs.OrderBy(pair => pair.Group is null ? int.MaxValue : order[pair.Group]);
        return Result.Success(new ContentSet(
            setName, [.. ordered], [.. groups], [.. groups.Select(group => labels.GetValueOrDefault(group) ?? DefaultLabel(group))]));
    }

    /// <summary>
    /// The same pairs, in the same order (so item ids are unchanged), without their groups: dealt free-form. For
    /// sessions saved before a set's groups always meant dealing by group (ADR 0009, amended 2026-10-22).
    /// </summary>
    public ContentSet WithoutGroups() =>
        Groups.IsEmpty ? this : new(Name, [.. Pairs.Select(pair => pair with { Group = null })], [], []);

    /// <summary>The set as entries again, e.g. to write its file.</summary>
    public IEnumerable<ContentEntry> ToEntries() =>
        Pairs.Select(pair => new ContentEntry(pair.Ball, pair.IsMatched ? null : pair.Square, pair.Group));

    private static void Check(string text, string side, int number, int maxLength, List<string> problems)
    {
        if (text.Length == 0)
        {
            problems.Add($"{side} {number} is empty");
        }
        else if (!ContentText.Fits(text, maxLength))
        {
            problems.Add($"{side} {number} is longer than {maxLength} characters");
        }
        else if (!ContentText.IsPrintable(text))
        {
            problems.Add($"{side} {number} has characters that can't be shown");
        }
    }

    private static void Unique(Dictionary<string, int> seen, string text, int number, string side, List<string> problems)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (seen.TryGetValue(ContentText.Key(text), out var first))
        {
            problems.Add($"'{text}' is on two {side}s (entries {first} and {number})");
        }
        else
        {
            seen.Add(ContentText.Key(text), number);
        }
    }
}
