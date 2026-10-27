using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>A player joins with the game code, the game password and a name (FR-3, FR-4).</summary>
public sealed record JoinSession(string Code, string Password, string Name);

/// <summary>The new participant's id and token (returned once; only its hash is stored).</summary>
public sealed record SessionJoined(GameCode Code, string ParticipantId, string Token);

/// <summary>
/// Join runs under the session lock so the "only between rounds" check (FR-5) cannot
/// race a round starting. The name and the card are claimed atomically in the store,
/// and released again if a later step fails.
/// </summary>
public sealed class JoinSessionHandler(
    ISessionStore store,
    IPasswordHasher passwords,
    ITokenService tokens,
    IEventPublisher publisher,
    IRandomSource random,
    IClock clock)
{
    public async Task<Result<SessionJoined>> HandleAsync(JoinSession command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!GameCode.TryParse(command.Code, out var code))
        {
            return Errors.SessionNotFound();
        }

        var passwordHash = await store.GetPasswordHashAsync(code, cancellationToken).ConfigureAwait(false);
        if (passwordHash is null)
        {
            return Errors.SessionNotFound();
        }

        if (!passwords.Verify(command.Password ?? string.Empty, passwordHash))
        {
            return Errors.WrongPassword();
        }

        if (!ParticipantName.TryCreate(command.Name, out var name, out var problem))
        {
            return new DomainError(DomainErrorCode.Invalid, Describe(problem));
        }

        if (!NameFilter.IsAllowed(name.Display))
        {
            return Errors.NameNotAllowed();
        }

        await using var sessionLock = (await store.LockAsync(code, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        var count = await store.CountParticipantsAsync(code, cancellationToken).ConfigureAwait(false);
        var admit = session.CheckCanAdmit(count);
        if (!admit.IsSuccess)
        {
            return admit.Error!;
        }

        var participantId = Guid.NewGuid().ToString("N");
        if (!await store.TryReserveNameAsync(code, name.Key, participantId, cancellationToken).ConfigureAwait(false))
        {
            return Errors.NameTaken(name.Display);
        }

        // During a round or its countdown they wait, with no card yet (A3); the round's end admits them.
        if (session.JoinersWait)
        {
            return await AddAsync(Participant.Wait(participantId, name, clock.UtcNow)).ConfigureAwait(false);
        }

        Result<Card> dealt;
        try
        {
            dealt = await CardGenerators.For(session.Variant)
                .DealUniqueAsync(random, (fingerprint, ct) => store.TryClaimCardAsync(code, fingerprint, ct), session.Variant.PossibleCards, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await store.ReleaseNameAsync(code, name.Key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (!dealt.IsSuccess)
        {
            await store.ReleaseNameAsync(code, name.Key, CancellationToken.None).ConfigureAwait(false);
            return dealt.Error!;
        }

        var card = dealt.Value;

        return await AddAsync(Participant.Join(participantId, name, card, session, clock.UtcNow)).ConfigureAwait(false);

        async Task<Result<SessionJoined>> AddAsync(Participant participant)
        {
            if (!await store.TryAddParticipantAsync(code, participant, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Participant id {participantId} is already in use.");
            }

            var token = tokens.Issue();
            await store.SaveTokenAsync(code, token.Hash, Caller.Participant(participantId), cancellationToken).ConfigureAwait(false);
            await publisher.NotifyAsync(code, new Recipient.Host(), new ParticipantJoined(participantId, name.Display), cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new SessionJoined(code, participantId, token.Token));
        }
    }

    private static string Describe(NameProblem problem) => problem switch
    {
        NameProblem.Empty => "Please enter a name.",
        NameProblem.TooLong => $"Names can be up to {ParticipantName.MaxLength} characters.",
        NameProblem.NoLetterOrDigit => "Names need at least one letter or number.",
        _ => "Names can use letters, numbers, spaces and ' - _ . only.",
    };
}
