using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Sessions;

/// <summary>
/// Host commands that change only shared session state. Each runs through
/// <see cref="SessionCommandRunner"/> and maps to one domain method.
/// </summary>
public sealed class HostCommandHandler(SessionCommandRunner runner)
{
    public Task<Result<CommandOutcome>> EndRoundAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.EndRound(now), cancellationToken);

    public Task<Result<CommandOutcome>> PauseAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.Pause(now), cancellationToken);

    public Task<Result<CommandOutcome>> ChangeWinConditionAsync(
        GameCode code, Caller caller, string winConditionId, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.ChangeWinCondition(winConditionId, now), cancellationToken);

    public Task<Result<CommandOutcome>> UpdateSettingsAsync(
        GameCode code, Caller caller, SessionSettings settings, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.UpdateSettings(settings, now), cancellationToken);

    public Task<Result<CommandOutcome>> ShowLeaderboardAsync(GameCode code, Caller caller, bool visible, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.ShowLeaderboard(visible, now), cancellationToken);

    public Task<Result<CommandOutcome>> EndSessionAsync(GameCode code, Caller caller, CancellationToken cancellationToken) =>
        runner.RunAsync(code, caller, (session, now) => session.End(now), cancellationToken);
}

/// <summary>
/// "Start round" / "Clear cards" (FR-34): starts the round, then clears every player's
/// marks for it. With <c>redrawCards</c> every card is replaced by a new unique one
/// (FR-39): all fingerprints are released first, then each new card is claimed.
/// </summary>
public sealed class StartRoundHandler(SessionCommandRunner runner, IRandomSource random)
{
    public Task<Result<CommandOutcome>> HandleAsync(GameCode code, Caller caller, bool redrawCards, CancellationToken cancellationToken) =>
        runner.RunAsync(
            code,
            caller,
            async (session, now, ct) =>
            {
                var started = session.StartRound(redrawCards, now);
                if (!started.IsSuccess)
                {
                    return started.Error!;
                }

                var store = runner.Store;
                var participants = await store.GetParticipantsAsync(code, ct).ConfigureAwait(false);
                var notifications = new List<(Recipient, Notification)>();
                if (redrawCards)
                {
                    await store.ReleaseAllCardsAsync(code, ct).ConfigureAwait(false);
                }

                var generator = new ColumnCardGenerator(session.Variant);
                foreach (var participant in participants)
                {
                    var newCards = new Queue<Card>();
                    for (var i = 0; redrawCards && i < participant.Cards.Count; i++)
                    {
                        newCards.Enqueue(await generator
                            .DealUniqueAsync(random, (fingerprint, c) => store.TryClaimCardAsync(code, fingerprint, c), ct)
                            .ConfigureAwait(false));
                    }

                    var replaced = participant.ResetForNewRound(session, redrawCards ? newCards.Dequeue : null);
                    await store.SaveParticipantAsync(code, participant, ct).ConfigureAwait(false);
                    foreach (var replacement in replaced)
                    {
                        notifications.Add((
                            new Recipient.Participant(participant.Id),
                            new CardReplaced(replacement.CardId, replacement.NewCard.Cells)));
                    }
                }

                return Result.Success(new CommandOutcome(started.Value, notifications));
            },
            cancellationToken);
}
