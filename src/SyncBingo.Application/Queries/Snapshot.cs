using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Queries;

/// <summary>
/// The state of a game as one caller may see it (docs/05 §2): on load, on reconnect and after a
/// missed event. <see cref="You"/> is set for a participant, <see cref="Host"/> for the host.
/// </summary>
public sealed record GameSnapshot(GameView Game, PlayerView? You, HostView? Host);

/// <summary>
/// What everyone in the game may see (the display view sees exactly this). <see cref="Seq"/> is the
/// caller's own stream position (ADR 0002): events in that stream with a higher seq come after this snapshot.
/// </summary>
public sealed record GameView(
    string Code,
    SessionState State,
    int Round,
    int UpcomingRound,
    DateTimeOffset? CountdownEndsAt,
    string WinCondition,
    IReadOnlyList<PatternView> Patterns,
    CardShape Card,
    IReadOnlyList<BoardGroup> Board,
    IReadOnlyList<BallCall>? Calls,
    int BallsRemaining,
    DateTimeOffset? NextBallFrom,
    VisibleSettings Settings,
    IReadOnlyList<LeaderboardEntry>? Leaderboard,
    long Version,
    long Seq);

/// <summary>A win pattern as data, so clients draw it and never hard-code shapes (FR-48): bit <c>i</c> of a mask is cell <c>i</c>.</summary>
public sealed record PatternView(string Id, string Name, IReadOnlyList<uint> Masks);

/// <summary>The card's grid: cells are numbered row by row; <see cref="FreeCell"/> is the free space, if any.</summary>
public sealed record CardShape(int Rows, int Columns, int? FreeCell);

/// <summary>One group of the call board (docs/06): a label such as "B" and the balls under it.</summary>
public sealed record BoardGroup(string Label, IReadOnlyList<int> Balls);

/// <summary>The settings players notice (as in the rules notices, FR-47) plus the cosmetic ones their screens follow.</summary>
public sealed record VisibleSettings(
    bool ShowBallToPlayers,
    bool HighlightMatches,
    bool ShowCallBoard,
    bool AutoFillAvailable,
    bool WinHighlight,
    bool FreeMarking,
    bool RequireMarksForClaims,
    Unmarking Unmarking,
    FreeSpace FreeSpace,
    RedrawPolicy RedrawPolicy,
    MultipleWins MultipleWins,
    bool ShowRoundNumbers)
{
    public static VisibleSettings From(SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new VisibleSettings(
            settings.ShowBallToPlayers,
            settings.HighlightMatches,
            settings.ShowCallBoard,
            settings.AutoFillAvailable,
            settings.WinHighlight,
            settings.FreeMarking,
            settings.RequireMarksForClaims,
            settings.Unmarking,
            settings.FreeSpace,
            settings.RedrawPolicy,
            settings.MultipleWins,
            settings.ShowRoundNumbers);
    }
}

/// <summary>A participant's own view: only ever their own cards (ADR 0001, CLAUDE.md rule 7).</summary>
public sealed record PlayerView(string ParticipantId, string Name, bool AutoFillOn, IReadOnlyList<CardView> Cards);

public sealed record CardView(int CardId, IReadOnlyList<int> Cells, uint Marks);

/// <summary>What only the host sees: every setting, the players, open claims and the round history.</summary>
public sealed record HostView(
    SessionSettings Settings,
    IReadOnlyList<ParticipantSummary> Participants,
    IReadOnlyList<Claim> PendingClaims,
    IReadOnlyList<RoundSummary> Rounds);

public sealed record ParticipantSummary(string ParticipantId, string Name, DateTimeOffset JoinedAt);

/// <summary>The leaderboard and the round summaries (FR-40, FR-41).</summary>
public sealed record LeaderboardView(IReadOnlyList<LeaderboardEntry> Entries, IReadOnlyList<RoundSummary> Rounds);

/// <summary>
/// Read-only queries. Each builds the caller's view from the store; which parts a caller sees is
/// decided here, in one place, and tested: players see only their own cards, the calls only if a
/// setting shows them, and the leaderboard only when the host shows it.
/// </summary>
public sealed class GameQueries(ISessionStore store)
{
    public async Task<Result<GameSnapshot>> SnapshotAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(caller);

        var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        switch (caller.Role)
        {
            case CallerRole.Host:
                var participants = await store.GetParticipantsAsync(code, cancellationToken).ConfigureAwait(false);
                return Result.Success(new GameSnapshot(
                    Public(session, showCalls: true, showLeaderboard: true, Audience.Host), null, HostOf(session, participants)));
            case CallerRole.Participant:
                var stored = await store.GetParticipantAsync(code, caller.ParticipantId!, cancellationToken).ConfigureAwait(false);
                if (stored is null)
                {
                    return Errors.NotInGame();
                }

                var settings = session.Settings;
                var seesCalls = settings.ShowBallToPlayers || settings.HighlightMatches || settings.ShowCallBoard;
                return Result.Success(new GameSnapshot(
                    Public(session, seesCalls, session.LeaderboardVisible, Audience.Players), PlayerOf(stored.Participant), null));
            case CallerRole.Display:
                return Result.Success(new GameSnapshot(Public(session, showCalls: true, session.LeaderboardVisible, Audience.Display), null, null));
            default:
                return new DomainError(DomainErrorCode.Forbidden, "Only people in the game can see it.");
        }
    }

    /// <summary>The host inspects a player's card to check a claim (FR-38).</summary>
    public async Task<Result<PlayerView>> InspectAsync(GameCode code, Caller caller, string participantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        if (await store.GetAsync(code, cancellationToken).ConfigureAwait(false) is null)
        {
            return Errors.SessionNotFound();
        }

        var stored = await store.GetParticipantAsync(code, participantId, cancellationToken).ConfigureAwait(false);
        return stored is null ? Errors.PlayerNotFound() : Result.Success(PlayerOf(stored.Participant));
    }

    /// <summary>Wins per player and the round summaries, for the host (FR-40, FR-41).</summary>
    public async Task<Result<LeaderboardView>> LeaderboardAsync(GameCode code, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.RequireHost() is { } forbidden)
        {
            return forbidden;
        }

        var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return session is null ? Errors.SessionNotFound() : Result.Success(new LeaderboardView(session.Leaderboard(), session.Rounds));
    }

    private static GameView Public(Session session, bool showCalls, bool showLeaderboard, Audience stream)
    {
        var variant = session.Variant;
        return new GameView(
            session.Code.ToString(),
            session.State,
            session.RoundNumber,
            session.UpcomingRound,
            session.CountdownEndsAt,
            session.WinConditionId,
            [.. variant.Patterns.Select(pattern => new PatternView(pattern.Id, pattern.Name, [.. pattern.Masks]))],
            new CardShape(variant.Layout.Rows, variant.Layout.Columns, variant.Layout.FreeCell),
            [.. variant.Columns.Select(column => new BoardGroup(column.Label, [.. Enumerable.Range(column.Min, column.Count)]))],
            showCalls ? [.. session.Calls] : null,
            session.BallsRemaining,
            session.NextBallFrom,
            VisibleSettings.From(session.Settings),
            showLeaderboard ? session.Leaderboard() : null,
            session.Version,
            session.StreamSeqs.For(stream));
    }

    private static PlayerView PlayerOf(Participant participant) => new(
        participant.Id,
        participant.Name.Display,
        participant.AutoFillOn,
        [.. participant.Cards.Select(card => new CardView(card.Id, [.. card.Card.Cells], card.Marks))]);

    private static HostView HostOf(Session session, IReadOnlyList<StoredParticipant> participants) => new(
        session.Settings,
        [.. participants.Select(stored => new ParticipantSummary(stored.Participant.Id, stored.Participant.Name.Display, stored.Participant.JoinedAt))],
        [.. session.Claims.Where(claim => claim.Round == session.RoundNumber && claim.Status == ClaimStatus.Pending)],
        session.Rounds);
}
