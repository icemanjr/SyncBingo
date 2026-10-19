using SyncBingo.Domain.Content;

namespace SyncBingo.Domain.Tests.Content;

internal static class ContentTestKit
{
    /// <summary><paramref name="count"/> matched entries: "word 1", "word 2", …</summary>
    public static List<ContentEntry> Words(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new ContentEntry($"word {i}"))];

    /// <summary>Times tables: "2 × 3" called, "6" marked; 5 groups (×1 … ×5) of 5 pairs.</summary>
    public static List<ContentEntry> TimesTables() =>
        [.. (from table in Enumerable.Range(1, 5)
             from factor in Enumerable.Range(table, 5)
             select new ContentEntry($"{table} × {factor}", $"{table}·{factor}={table * factor}", $"×{table}"))];

    public static ContentSet Valid(IEnumerable<ContentEntry> entries, string? name = "Test set")
    {
        var result = ContentSet.TryCreate(name, entries);
        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        return result.Value;
    }

    public static string Refusal(IEnumerable<ContentEntry> entries, string? name = "Test set")
    {
        var result = ContentSet.TryCreate(name, entries);
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        return result.Error.Message;
    }
}
