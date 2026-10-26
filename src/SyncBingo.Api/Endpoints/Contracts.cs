using SyncBingo.Application.Queries;
using SyncBingo.Domain.Rules;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

// Request and response bodies (docs/05). Kept apart from domain types so the wire format
// changes only on purpose.

public sealed record CreateGameRequest(string Password, SettingsBody? Settings = null, string? WinCondition = null);

public sealed record GameCreated(string Code, Uri JoinUrl, string HostToken, SnapshotBody Snapshot);

public sealed record JoinRequest(string Password, string Name);

public sealed record Joined(string ParticipantId, string Token, SnapshotBody Snapshot);

/// <summary>
/// The caller's view of the game (docs/05 §2): <c>game</c> for everyone, <c>you</c> for a player, <c>host</c>
/// for the host. Settings use the same wire shape as <c>PUT /rules</c>.
/// </summary>
public sealed record SnapshotBody(GameView Game, PlayerView? You, HostBody? Host)
{
    public static SnapshotBody From(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SnapshotBody(
            snapshot.Game,
            snapshot.You,
            snapshot.Host is { } host
                ? new HostBody(SettingsBody.From(host.Settings), host.Participants, host.PendingClaims, host.Rounds)
                : null);
    }
}

public sealed record HostBody(
    SettingsBody Settings,
    IReadOnlyList<ParticipantSummary> Participants,
    IReadOnlyList<Claim> PendingClaims,
    IReadOnlyList<RoundSummary> Rounds);

public sealed record MarkRequest(int CardId, int Cell, bool Marked);

public sealed record MarksBody(uint Marks);

public sealed record CardRequest(int CardId);

public sealed record CardBody(int CardId, IReadOnlyList<int> Cells);

public sealed record ClaimAccepted(DateTimeOffset ClaimedAt);

public sealed record AutoFillBody(bool On);

public sealed record CountdownRequest(bool StartInAuto);

public sealed record CountdownStarted(int Round, DateTimeOffset EndsAt);

public sealed record ClearCardsRequest(bool RedrawCards);

/// <summary>The ball just drawn; with a custom set, what to call and what players mark (ADR 0009).</summary>
public sealed record BallBody(int Number, string Letter, int Sequence, string? Call = null, string? Square = null)
{
    public static BallBody From(BallCall call) => new(call.Number, call.Letter, call.Sequence, call.Call, call.Square);
}

public sealed record AwardRequest(string ParticipantId, int? CardId = null, int? Wins = null, int? ClaimId = null);

public sealed record VisibleRequest(bool Visible);

public sealed record JoinNoteRequest(string? Note);

/// <summary>"Players can download this set" (ADR 0009).</summary>
public sealed record ContentSharingRequest(bool PlayersMayDownload);

/// <summary>A pattern the host drew (FR-21a): its name and its squares, as cell indexes row by row.</summary>
public sealed record PatternRequest(string Name, IReadOnlyList<int> Cells);

/// <summary>A new name for one of the host's patterns (FR-21a).</summary>
public sealed record PatternNameRequest(string Name);

/// <summary>"Call board shows" (FR-29): <c>Ball</c> or <c>Square</c>.</summary>
public sealed record BoardSideRequest(BoardSide Side);

/// <summary>A TV's pairing (ADR 0010): it listens on <c>/hubs/pairing?pairingId=</c> and shows <see cref="PairingCode"/>.</summary>
public sealed record PairingCreated(string PairingId, string PairingCode, DateTimeOffset ExpiresAt);

public sealed record PairingApproval(string PairingCode);

/// <summary>A new display link (FR-28): the token is in the URL fragment, which browsers never send to a server.</summary>
public sealed record DisplayLinkCreated(string DisplayId, Uri DisplayUrl);

/// <summary>The host's Apply: the settings changed (only fields sent) and, optionally, the new pattern.</summary>
public sealed record RulesRequest(SettingsBody? Settings = null, string? WinCondition = null);

/// <summary>The rules in force: the settings and the pattern.</summary>
public sealed record RulesView(SettingsBody Settings, string WinCondition);

/// <summary>
/// Host settings on the wire. In a request every field is optional and only those sent change
/// (docs/05 "partial settings"); in a response every field is set.
/// </summary>
public sealed record SettingsBody
{
    public int? AutoCallIntervalSeconds { get; init; }

    public bool? ShowBallToPlayers { get; init; }

    public bool? HighlightMatches { get; init; }

    public bool? ShowCallBoard { get; init; }

    public bool? AutoFillAvailable { get; init; }

    public bool? WinHighlight { get; init; }

    public WinDetection? WinDetection { get; init; }

    public RedrawPolicy? RedrawPolicy { get; init; }

    public bool? FreeMarking { get; init; }

    public bool? RequireMarksForClaims { get; init; }

    public Unmarking? Unmarking { get; init; }

    public FreeSpace? FreeSpace { get; init; }

    public CardSize? CardSize { get; init; }

    public bool? ShowRoundNumbers { get; init; }

    public MultipleWins? MultipleWins { get; init; }

    public static SettingsBody From(SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SettingsBody
        {
            AutoCallIntervalSeconds = (int)settings.AutoCallInterval.TotalSeconds,
            ShowBallToPlayers = settings.ShowBallToPlayers,
            HighlightMatches = settings.HighlightMatches,
            ShowCallBoard = settings.ShowCallBoard,
            AutoFillAvailable = settings.AutoFillAvailable,
            WinHighlight = settings.WinHighlight,
            WinDetection = settings.WinDetection,
            RedrawPolicy = settings.RedrawPolicy,
            FreeMarking = settings.FreeMarking,
            RequireMarksForClaims = settings.RequireMarksForClaims,
            Unmarking = settings.Unmarking,
            FreeSpace = settings.FreeSpace,
            CardSize = settings.CardSize,
            ShowRoundNumbers = settings.ShowRoundNumbers,
            MultipleWins = settings.MultipleWins,
        };
    }

    /// <summary>The fields that were sent, laid over <paramref name="settings"/>. The domain validates the result.</summary>
    public SessionSettings ApplyTo(SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            AutoCallInterval = AutoCallIntervalSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : settings.AutoCallInterval,
            ShowBallToPlayers = ShowBallToPlayers ?? settings.ShowBallToPlayers,
            HighlightMatches = HighlightMatches ?? settings.HighlightMatches,
            ShowCallBoard = ShowCallBoard ?? settings.ShowCallBoard,
            AutoFillAvailable = AutoFillAvailable ?? settings.AutoFillAvailable,
            WinHighlight = WinHighlight ?? settings.WinHighlight,
            WinDetection = WinDetection ?? settings.WinDetection,
            RedrawPolicy = RedrawPolicy ?? settings.RedrawPolicy,
            FreeMarking = FreeMarking ?? settings.FreeMarking,
            RequireMarksForClaims = RequireMarksForClaims ?? settings.RequireMarksForClaims,
            Unmarking = Unmarking ?? settings.Unmarking,
            FreeSpace = FreeSpace ?? settings.FreeSpace,
            CardSize = CardSize ?? settings.CardSize,
            ShowRoundNumbers = ShowRoundNumbers ?? settings.ShowRoundNumbers,
            MultipleWins = MultipleWins ?? settings.MultipleWins,
        };
    }
}
