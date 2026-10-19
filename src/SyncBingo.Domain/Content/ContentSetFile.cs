using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SyncBingo.Domain.Content;

/// <summary>
/// The file a host keeps a set in (ADR 0009): versioned JSON, small enough to email or keep on a USB stick,
/// and laid out like the card. Groups are listed in the order their columns are dealt, each with its pairs;
/// entries outside any group go in the top-level <c>pairs</c>. A pair is a short list: <c>["cat"]</c> when the
/// ball and the square match, <c>["2 + 5", "7"]</c> when the ball calls one thing and the square shows another.
/// <code>
/// {
///   "format": 1,
///   "name": "Normal Bingo",
///   "groups": [
///     { "name": "B", "pairs": [ ["1"], ["2"], … ] },
///     { "name": "I", "pairs": [ ["16"], … ] }
///   ],
///   "pairs": [
///     ["2 + 5", "7"]
///   ]
/// }
/// </code>
/// Unknown properties are ignored, so a later format can add some (images, say) without breaking older
/// readers of the fields they know.
/// </summary>
public static class ContentSetFile
{
    public const int Format = 1;

    /// <summary>A set file is at most 64 KB (UTF-8): 500 long pairs fit with room to spare.</summary>
    public const int MaxBytes = 64 * 1024;

    private static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = 8 };

    // Keep "×", "é" and emoji readable in the file instead of \u escapes.
    private static readonly JsonSerializerOptions StringOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Reads and checks a set file, refusing anything that isn't a valid set of this format or older.</summary>
    public static Result<ContentSet> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            return Refuse($"A set file can be at most {MaxBytes / 1024} KB.");
        }

        try
        {
            using var document = JsonDocument.Parse(json, ReadOptions);
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return NotASetFile();
        }
    }

    /// <summary>Writes a set as its file, ready to download: groups in order, then the ungrouped pairs.</summary>
    public static string Write(ContentSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var file = new StringBuilder();
        file.Append("{\n  \"format\": ").Append(Format).Append(",\n  \"name\": ").Append(Quote(set.Name));
        if (!set.Groups.IsEmpty)
        {
            file.Append(",\n  \"groups\": [");
            for (var i = 0; i < set.Groups.Length; i++)
            {
                var name = set.Groups[i];
                file.Append(i == 0 ? "\n" : ",\n").Append("    {\n      \"name\": ").Append(Quote(name)).Append(",\n      \"pairs\": ");
                AppendPairs(file, set.Pairs.Where(pair => pair.Group == name), "      ");
                file.Append("\n    }");
            }

            file.Append("\n  ]");
        }

        var ungrouped = set.Pairs.Where(pair => pair.Group is null).ToList();
        if (ungrouped.Count > 0)
        {
            file.Append(",\n  \"pairs\": ");
            AppendPairs(file, ungrouped, "  ");
        }

        return file.Append("\n}\n").ToString();
    }

    private static Result<ContentSet> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("format", out var format)
            || format.ValueKind != JsonValueKind.Number
            || !format.TryGetInt32(out var version)
            || version < 1)
        {
            return NotASetFile();
        }

        if (version > Format)
        {
            return Refuse("This set file was made by a newer version of SyncBingo; reload the page and try again.");
        }

        string? name = null;
        if (root.TryGetProperty("name", out var nameElement))
        {
            if (nameElement.ValueKind != JsonValueKind.String)
            {
                return NotASetFile();
            }

            name = nameElement.GetString();
        }

        var hasGroups = root.TryGetProperty("groups", out var groupsElement);
        var hasPairs = root.TryGetProperty("pairs", out var pairsElement);
        if (!hasGroups && !hasPairs)
        {
            return NotASetFile();
        }

        var entries = new List<ContentEntry>();
        var groupOrder = new List<string>();
        if (hasGroups)
        {
            if (groupsElement.ValueKind != JsonValueKind.Array)
            {
                return NotASetFile();
            }

            foreach (var group in groupsElement.EnumerateArray())
            {
                if (group.ValueKind != JsonValueKind.Object
                    || !group.TryGetProperty("name", out var groupName)
                    || groupName.ValueKind != JsonValueKind.String
                    || !group.TryGetProperty("pairs", out var groupPairs)
                    || !TryReadPairs(groupPairs, groupName.GetString(), entries))
                {
                    return NotASetFile();
                }

                groupOrder.Add(groupName.GetString()!);
            }
        }

        if (hasPairs && !TryReadPairs(pairsElement, group: null, entries))
        {
            return NotASetFile();
        }

        return ContentSet.TryCreate(name, entries, groupOrder);
    }

    /// <summary>A list of pairs, each <c>["ball"]</c> or <c>["ball", "square"]</c>.</summary>
    private static bool TryReadPairs(JsonElement pairs, string? group, List<ContentEntry> entries)
    {
        if (pairs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var pair in pairs.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array
                || pair.GetArrayLength() is < 1 or > 2
                || pair.EnumerateArray().Any(side => side.ValueKind != JsonValueKind.String))
            {
                return false;
            }

            var ball = pair[0].GetString()!;
            var square = pair.GetArrayLength() == 2 ? pair[1].GetString() : null;
            entries.Add(new ContentEntry(ball, square, group));
        }

        return true;
    }

    /// <summary>One pair to a line: <c>["cat"]</c> or <c>["2 + 5", "7"]</c>.</summary>
    private static void AppendPairs(StringBuilder file, IEnumerable<ContentPair> pairs, string indent)
    {
        var lines = pairs
            .Select(pair => pair.IsMatched ? $"[{Quote(pair.Ball)}]" : $"[{Quote(pair.Ball)}, {Quote(pair.Square)}]")
            .ToList();
        if (lines.Count == 0)
        {
            file.Append("[]");
            return;
        }

        file.Append("[\n").Append(string.Join(",\n", lines.Select(line => $"{indent}  {line}"))).Append('\n').Append(indent).Append(']');
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text, StringOptions);

    private static Result<ContentSet> NotASetFile() =>
        Refuse("This isn't a SyncBingo set file. Download one from the set editor to see the format.");

    private static Result<ContentSet> Refuse(string message) => Result.Failure<ContentSet>(DomainErrorCode.Invalid, message);
}
