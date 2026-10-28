namespace SyncBingo.Domain.Tests;

/// <summary>Builds masks from explicit cell indices, independently of the code under test.</summary>
internal static class Cells
{
    public const int FreeCenter = 12;

    public static uint Of(params int[] cells) => cells.Aggregate(0u, (mask, cell) => mask | (1u << cell));

    public static IEnumerable<int> In(uint mask) => Enumerable.Range(0, 32).Where(cell => (mask & (1u << cell)) != 0);
}
