using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncBingo.Domain.Content;

/// <summary>
/// The file a host keeps a set in (ADR 0009): versioned JSON, small enough to email or keep on a USB stick.
/// <code>
/// {
///   "format": 1,
///   "name": "Times tables 1–3",
///   "pairs": [
///     { "ball": "2 × 3", "square": "6", "group": "×2" },
///     { "ball": "cat" }
///   ]
/// }
/// </code>
/// A pair without <c>square</c> shows the same text on the ball and the square; <c>group</c> is optional,
/// and groups are ordered by where they first appear. Unknown properties are ignored, so a later format can
/// add some (images, say) without breaking older readers of the fields they know.
/// </summary>
public static class ContentSetFile
{
    public const int Format = 1;

    /// <summary>A set file is at most 64 KB (UTF-8): 500 long pairs fit with room to spare.</summary>
    public const int MaxBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        MaxDepth = 8,

        // Keep "×", "é" and emoji readable in the file instead of \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Reads and checks a set file, refusing anything that isn't a valid set of this format or older.</summary>
    public static Result<ContentSet> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            return Refuse($"A set file can be at most {MaxBytes / 1024} KB.");
        }

        FileModel? file;
        try
        {
            file = JsonSerializer.Deserialize<FileModel>(json, Options);
        }
        catch (JsonException)
        {
            return NotASetFile();
        }

        if (file?.Pairs is null || file.Format is null || file.Pairs.Any(pair => pair?.Ball is null))
        {
            return NotASetFile();
        }

        if (file.Format > Format)
        {
            return Refuse("This set file was made by a newer version of SyncBingo; reload the page and try again.");
        }

        if (file.Format < 1)
        {
            return NotASetFile();
        }

        return ContentSet.TryCreate(file.Name, file.Pairs.Select(pair => new ContentEntry(pair!.Ball!, pair.Square, pair.Group)));
    }

    /// <summary>Writes a set as its file, ready to download.</summary>
    public static string Write(ContentSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var file = new FileModel
        {
            Format = Format,
            Name = set.Name,
            Pairs = [.. set.ToEntries().Select(entry => new PairModel { Ball = entry.Ball, Square = entry.Square, Group = entry.Group })],
        };
        return JsonSerializer.Serialize(file, Options);
    }

    private static Result<ContentSet> NotASetFile() =>
        Refuse("This isn't a SyncBingo set file. Download one from the set editor to see the format.");

    private static Result<ContentSet> Refuse(string message) => Result.Failure<ContentSet>(DomainErrorCode.Invalid, message);

    private sealed class FileModel
    {
        public int? Format { get; set; }

        public string? Name { get; set; }

        public List<PairModel?>? Pairs { get; set; }
    }

    private sealed class PairModel
    {
        public string? Ball { get; set; }

        public string? Square { get; set; }

        public string? Group { get; set; }
    }
}
