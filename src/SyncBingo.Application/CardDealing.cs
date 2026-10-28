using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application;

internal static class CardDealing
{
    /// <summary>
    /// Deals a card no one else holds (FR-39), claiming its fingerprint atomically in the
    /// store; the async counterpart of <see cref="CardGeneratorExtensions.GenerateUnique"/>, which the domain tests use.
    /// When no free card turns up (a small set whose cards are nearly all held), "No unique cards available".
    /// </summary>
    public static async Task<Result<Card>> DealUniqueAsync(
        this ICardGenerator generator,
        IRandomSource random,
        Func<string, CancellationToken, Task<bool>> tryClaim,
        long possibleCards,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < CardGeneratorExtensions.MaxAttempts; attempt++)
        {
            var card = generator.Generate(random);
            if (await tryClaim(card.Fingerprint, cancellationToken).ConfigureAwait(false))
            {
                return Result.Success(card);
            }
        }

        return Session.NoUniqueCards(possibleCards);
    }
}
