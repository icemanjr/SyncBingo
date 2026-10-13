using SyncBingo.Domain;
using SyncBingo.Domain.Cards;

namespace SyncBingo.Application;

internal static class Errors
{
    public static DomainError SessionNotFound() =>
        new(DomainErrorCode.NotFound, "That game doesn't exist or has ended. Check the code with your host.");

    public static DomainError WrongPassword() =>
        new(DomainErrorCode.Unauthorized, "That password isn't right. Check it with your host.");

    public static DomainError NotInGame() =>
        new(DomainErrorCode.NotFound, "You're no longer in this game.");

    public static DomainError PlayerNotFound() =>
        new(DomainErrorCode.NotFound, "That player isn't in this game.");

    public static DomainError Busy() =>
        new(DomainErrorCode.Conflict, "A lot happened at once. Please try again.");

    public static DomainError NameTaken(string name) =>
        new(DomainErrorCode.Conflict, $"Someone is already playing as \"{name}\". Please pick another name.");
}

internal static class CardDealing
{
    /// <summary>
    /// Deals a card no one else holds (FR-39), claiming its fingerprint atomically in the
    /// store; the async counterpart of <see cref="CardGeneratorExtensions.GenerateUnique"/>.
    /// </summary>
    public static async Task<Card> DealUniqueAsync(
        this ICardGenerator generator,
        IRandomSource random,
        Func<string, CancellationToken, Task<bool>> tryClaim,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < CardGeneratorExtensions.MaxAttempts; attempt++)
        {
            var card = generator.Generate(random);
            if (await tryClaim(card.Fingerprint, cancellationToken).ConfigureAwait(false))
            {
                return card;
            }
        }

        throw new InvalidOperationException(
            $"Could not deal a card that is not already in play after {CardGeneratorExtensions.MaxAttempts} attempts.");
    }
}
