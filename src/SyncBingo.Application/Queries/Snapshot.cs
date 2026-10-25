using SyncBingo.Application.Ports;
using SyncBingo.Domain;
using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Application.Queries;

/// <summary>
/// The state of a game as one caller may see it (docs/05 §2): on load, on reconnect and after a
/// missed event. <see cref="You"/> is set for a participant, <see cref="Host"/> for the host.
/// </summary>
public sealed record GameSnapshot(GameView Game, PlayerView? You, HostView? Host);

/// <summary>
/// What everyone in the game may see (the display view sees exactly this), except <see cref="JoinNote"/>
/// (FR-49), which only the host and the display get. <see cref="Seq"/> is the
/// caller's own stream position (ADR 0002): events in that stream with a higher seq come after this snapshot.
/// <see cref="FreeText"/> is what the free space says: the custom set's own text, or "FREE".
/// </summary>
public sealed record GameView(
    string Code,
    SessionState State,
    int Round,
    int UpcomingRound,
    DateTimeOffset? CountdownEndsAt,
    string WinCondition,
    IReadOnlyList<PatternView> Patterns,
    IReadOnlyList<PresetView> Presets,
    CardShape Card,
    string FreeText,
    IReadOnlyList<CardSizeView> CardSizes,
    IReadOnlyList<BoardGroup> Board,
    IReadOnlyList<BallCall>? Calls,
    int BallsRemaining,
    DateTimeOffset? NextBallFrom,
    VisibleSettings Settings,
    IReadOnlyList<LeaderboardEntry>? Leaderboard,
    long Version,
    long Seq,
    string? JoinNote,
    bool LeaderboardVisible,
    ContentView? Content = null);

/// <summary>
/// The custom set in use (ADR 0009), or <c>null</c> for numbers. Never the answer key: <see cref="Board"/> is
/// each item's ball text (<c>Board[n − 1]</c> is item n, as the board's groups list item numbers), and
/// <see cref="Squares"/> every square's text in alphabetical order, to size text for Max (FR-51) without
/// saying which square goes with which ball. Calls carry both of their own texts.
/// </summary>
/// <see cref="GroupItems"/>: the set has groups, so it is dealt by group, one per card column.
/// <see cref="Labels"/> are the groups' short labels, in board order, for the card's column headers.
/// <see cref="BoardSide"/> says which face <see cref="Board"/> holds (FR-29): ball text by default, square text
/// when the host chose it. <see cref="PlayersMayDownload"/>: the host lets players download the set (in the lobby).
public sealed record ContentView(
    string Name,
    bool GroupItems,
    IReadOnlyList<string> Board,
    IReadOnlyList<string> Squares,
    IReadOnlyList<string> Labels,
    BoardSide BoardSide = BoardSide.Ball,
    bool PlayersMayDownload = false);

/// <summary>
/// A win pattern as data, so clients draw it and never hard-code shapes (FR-48): bit <c>i</c> of a mask is cell <c>i</c>.
/// <see cref="WinCondition"/> in the game view is a selection of these, joined by "+" (FR-21).
/// </summary>
public sealed record PatternView(string Id, string Name, IReadOnlyList<uint> Masks, bool IsLine, string AnnouncedAs)
{
    public static PatternView Of(WinPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return new PatternView(pattern.Id, pattern.Name, [.. pattern.Masks], pattern.IsLine, pattern.AnnouncedAs);
    }
}

/// <summary>A named group of patterns, e.g. Normal Bingo = rows + columns + diagonals; clients name a selection with it.</summary>
public sealed record PresetView(string Id, string Name, IReadOnlyList<string> PatternIds);

/// <summary>The card's grid: cells are numbered row by row; <see cref="FreeCell"/> is the free space, if any.</summary>
public sealed record CardShape(int Rows, int Columns, int? FreeCell);

/// <summary>
/// A card size the host can choose (FR-26a) and the patterns it can be won with, so Options lists the right
/// ones while the host picks a size, e.g. no letters on 3 × 3.
/// </summary>
public sealed record CardSizeView(CardSize Size, int Rows, int Columns, IReadOnlyList<PatternView> Patterns)
{
    /// <summary>Every size, smallest last, as the standard rules define them.</summary>
    public static IReadOnlyList<CardSizeView> All { get; } =
    [
        .. Enum.GetValues<CardSize>().Select(size =>
        {
            var variant = StandardVariants.For(size);
            return new CardSizeView(size, variant.Layout.Rows, variant.Layout.Columns, [.. variant.Patterns.Select(PatternView.Of)]);
        }),
    ];
}

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

/// <summary>
/// A card. <see cref="Faces"/> is each square's text with a custom set (ADR 0009). For a player in such a
/// game, <see cref="Cells"/> holds only placeholders (cell position + 1; the free cell 0), never item numbers:
/// item numbers and the board's ball text together would be the answer key (which clue goes with which word).
/// A player's app knows a square was called when its text matches a called ball's square text.
/// </summary>
public sealed record CardView(int CardId, IReadOnlyList<int> Cells, uint Marks, IReadOnlyList<string>? Faces = null)
{
    /// <summary>A card as its holder may see it: with a set, faces and placeholder cells (see above).</summary>
    public static CardView ForPlayer(int cardId, Card card, uint marks, ContentSet? content) =>
        content is null
            ? new CardView(cardId, [.. card.Cells], marks)
            : new CardView(cardId, Placeholders(card), marks, FacesOf(card, content));

    /// <summary>A card as the host sees it when checking a claim: real item numbers and, with a set, the faces.</summary>
    public static CardView ForHost(int cardId, Card card, uint marks, ContentSet? content) =>
        new(cardId, [.. card.Cells], marks, content is null ? null : FacesOf(card, content));

    /// <summary>Cell position + 1, the free cell 0: what a player's card shows instead of item numbers with a set.</summary>
    public static IReadOnlyList<int> Placeholders(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return [.. card.Cells.Select((item, cell) => item == Card.FreeCellNumber ? Card.FreeCellNumber : cell + 1)];
    }

    private static string[] FacesOf(Card card, ContentSet content) =>
        [.. card.Cells.Select(item => item == Card.FreeCellNumber ? string.Empty : content[item].Square)];
}

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
                    Public(session, seesCalls, session.LeaderboardVisible, Audience.Players), PlayerOf(stored.Participant, session.Content), null));
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

        var session = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Errors.SessionNotFound();
        }

        var stored = await store.GetParticipantAsync(code, participantId, cancellationToken).ConfigureAwait(false);
        return stored is null ? Errors.PlayerNotFound() : Result.Success(HostInspection(stored.Participant, session.Content));
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
            [.. variant.Patterns.Select(PatternView.Of)],
            [.. variant.Presets.Select(preset => new PresetView(preset.Id, preset.Name, [.. preset.PatternIds]))],
            new CardShape(variant.Layout.Rows, variant.Layout.Columns, variant.Layout.FreeCell),
            session.Content?.FreeText ?? ContentSet.DefaultFreeText,
            CardSizeView.All,
            variant.IsFreeForm
                ? [new BoardGroup(string.Empty, [.. Enumerable.Range(1, variant.BallMax)])] // one block, no labels (docs/06)
                : [.. variant.Columns.Select(column => new BoardGroup(column.Label, [.. Enumerable.Range(column.Min, column.Count)]))],
            showCalls ? [.. session.Calls] : null,
            session.BallsRemaining,
            session.NextBallFrom,
            VisibleSettings.From(session.Settings),
            showLeaderboard ? session.Leaderboard() : null,
            session.Version,
            session.StreamSeqs.For(stream),
            stream == Audience.Players ? null : session.JoinNote,
            session.LeaderboardVisible,
            session.Content is { } content
                ? new ContentView(
                    content.Name,
                    session.GroupItems,
                    [.. content.Pairs.Select(pair => session.BoardSide == BoardSide.Square ? pair.Square : pair.Ball)],
                    [.. content.Pairs.Select(pair => pair.Square).Order(StringComparer.OrdinalIgnoreCase)],
                    session.GroupItems ? [.. content.Labels] : [],
                    session.BoardSide,
                    session.PlayersMayDownload)
                : null);
    }

    private static PlayerView PlayerOf(Participant participant, ContentSet? content) => new(
        participant.Id,
        participant.Name.Display,
        participant.AutoFillOn,
        [.. participant.Cards.Select(card => CardView.ForPlayer(card.Id, card.Card, card.Marks, content))]);

    private static PlayerView HostInspection(Participant participant, ContentSet? content) => new(
        participant.Id,
        participant.Name.Display,
        participant.AutoFillOn,
        [.. participant.Cards.Select(card => CardView.ForHost(card.Id, card.Card, card.Marks, content))]);

    private static HostView HostOf(Session session, IReadOnlyList<StoredParticipant> participants) => new(
        session.Settings,
        [.. participants.Select(stored => new ParticipantSummary(stored.Participant.Id, stored.Participant.Name.Display, stored.Participant.JoinedAt))],
        [.. session.Claims.Where(claim => claim.Round == session.RoundNumber && claim.Status == ClaimStatus.Pending)],
        session.Rounds);
}
