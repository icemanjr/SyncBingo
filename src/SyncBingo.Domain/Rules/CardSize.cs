namespace SyncBingo.Domain.Rules;

/// <summary>
/// The card a game is played on (FR-26a), chosen in the lobby: 5×5 (standard), 4×4 or 3×3. Each has its own
/// standard rules (<see cref="StandardVariants.For"/>); a custom set is dealt onto whichever is chosen.
/// </summary>
public enum CardSize
{
    FiveByFive,
    FourByFour,
    ThreeByThree,
}
