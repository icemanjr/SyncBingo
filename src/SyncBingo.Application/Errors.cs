using SyncBingo.Domain;

namespace SyncBingo.Application;

/// <summary>Refusals the application layer raises itself, worded to be shown to people (docs/05 §5).</summary>
public static class Errors
{
    /// <summary>An offensive name (<see cref="Domain.Participants.NameFilter"/>); the reason isn't spelled out.</summary>
    public static DomainError NameNotAllowed() =>
        new(DomainErrorCode.Invalid, "Please choose a different name.");

    public static DomainError SessionNotFound() =>
        new(DomainErrorCode.NotFound, "That game does not exist or has ended. Check the code with your host.");

    public static DomainError WrongPassword() =>
        new(DomainErrorCode.Unauthorized, "That password is not right. Check it with your host.");

    public static DomainError NotInGame() =>
        new(DomainErrorCode.NotFound, "You are no longer in this game.");

    public static DomainError PlayerNotFound() =>
        new(DomainErrorCode.NotFound, "That player is not in this game.");

    public static DomainError Busy() =>
        new(DomainErrorCode.Conflict, "A lot happened at once. Please try again.");

    public static DomainError NameTaken(string name) =>
        new(DomainErrorCode.Conflict, $"Someone is already playing as \"{name}\". Please pick another name.");
}
