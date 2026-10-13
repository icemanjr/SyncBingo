namespace SyncBingo.Domain.Cards;

/// <summary>
/// Deals cards for one rule set. Cards hold item ids (ball numbers in standard
/// Bingo), so marking, win patterns and uniqueness work the same for every
/// implementation; only the choice of ids per cell differs (ADR 0009).
/// </summary>
public interface ICardGenerator
{
    /// <summary>Deals one random card.</summary>
    Card Generate(IRandomSource random);
}

public static class CardGeneratorExtensions
{
    /// <summary>
    /// How many times <see cref="GenerateUnique"/> redeals before giving up. A clash
    /// is astronomically rare for real rule sets, so hitting this means the set has
    /// (almost) run out of distinct cards.
    /// </summary>
    public const int MaxAttempts = 1000;

    /// <summary>
    /// Deals a card that is not already in play (FR-39). <paramref name="tryClaim"/> is
    /// asked about each candidate and must return <c>true</c> only if the card is free
    /// and it has now reserved it (for example an atomic add to a set of fingerprints),
    /// so two concurrent deals can never both receive the same card.
    /// </summary>
    /// <exception cref="InvalidOperationException">No unclaimed card was found in <see cref="MaxAttempts"/> tries.</exception>
    public static Card GenerateUnique(this ICardGenerator generator, IRandomSource random, Func<Card, bool> tryClaim)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(tryClaim);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var card = generator.Generate(random);
            if (tryClaim(card))
            {
                return card;
            }
        }

        throw new InvalidOperationException(
            $"Could not deal a card that is not already in play after {MaxAttempts} attempts.");
    }
}
