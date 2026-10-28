using System.Globalization;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Sessions;

/// <summary>
/// The shared state of one game: lifecycle, settings, win condition and the balls
/// called this round (docs/03). Every command checks the state first and returns
/// the events it caused, numbered with <see cref="SessionEvent.Seq"/>. Changes run
/// under the per-session lock (docs/02 §6); each player's cards and marks live in a
/// separate aggregate so marking never contends with the host.
/// </summary>
public sealed class Session
{
    /// <summary>Default participant cap (assumption A7).</summary>
    public const int MaxParticipants = 100;

    /// <summary>How many award <c>Idempotency-Key</c>s are remembered; far more than a host awards in one sitting.</summary>
    public const int RememberedRequestKeys = 100;

    public const int MaxRequestKeyLength = 100;

    /// <summary>Longest join note (FR-49): a password or a short hint.</summary>
    public const int MaxJoinNoteLength = 80;

    /// <summary>Patterns a host may draw for one game (FR-21a).</summary>
    public const int MaxCustomPatterns = 5;

    /// <summary>Longest name for a pattern the host drew, e.g. "Plus sign".</summary>
    public const int MaxPatternNameLength = 20;

    /// <summary>The rules without custom content: the card, patterns and presets a set is played with.</summary>
    private readonly BingoVariant _standard;

    private readonly List<BallCall> _calls = [];
    private readonly HashSet<int> _calledNumbers = [];
    private readonly List<Claim> _claims = [];
    private readonly List<WinRecord> _wins = [];
    private readonly List<RoundSummary> _rounds = [];

    /// <summary>Recent award <c>Idempotency-Key</c>s, oldest first; a repeat within these changes nothing.</summary>
    private readonly List<string> _awardKeys = [];

    /// <summary>The host's own patterns (FR-21a), every card size's, in the order they were drawn.</summary>
    private readonly List<CustomPattern> _customPatterns = [];
    /// <summary>Lines already alerted per card and win condition this round, so alerts never repeat.</summary>
    private readonly Dictionary<(string ParticipantId, int CardId, string WinConditionId), int> _alerted = [];

    private Session(GameCode code, BingoVariant variant, SessionSettings settings, string winConditionId, DateTimeOffset now)
    {
        Code = code;
        _standard = variant;
        Variant = RulesFor(settings, content: null).Value;
        Settings = settings;
        WinConditionId = winConditionId;
        CreatedAt = now;
        LastActivityAt = now;
    }

    public GameCode Code { get; }

    /// <summary>The rules in force: the standard ones, or those of the custom set in use (<see cref="ContentVariant"/>).</summary>
    public BingoVariant Variant { get; private set; }

    /// <summary>The host's custom set (ADR 0009), or <c>null</c> for numbers.</summary>
    public ContentSet? Content { get; private set; }

    /// <summary>The custom set is dealt by group, one per card column: it has groups (ADR 0009).</summary>
    public bool GroupItems => Content is { Groups.IsEmpty: false };

    /// <summary>The patterns the host drew for this game (FR-21a), for every card size.</summary>
    public IReadOnlyList<CustomPattern> CustomPatterns => _customPatterns;

    /// <summary>Which face of a custom set's items the call boards show (FR-29); the ball side by default.</summary>
    public BoardSide BoardSide { get; private set; }

    /// <summary>
    /// The host lets players download the set in use (ADR 0009), e.g. a teacher sharing a set with another
    /// who joined as a player. Off by default, and off again whenever the set changes.
    /// </summary>
    public bool PlayersMayDownload { get; private set; }

    /// <summary>
    /// A player may download the set now: the host allows it and the game is in the lobby, so the file (which
    /// pairs every clue with its word) is never at hand during a round.
    /// </summary>
    public bool PlayersCanDownloadNow => Content is not null && PlayersMayDownload && State == SessionState.BetweenRounds;

    public SessionState State { get; private set; } = SessionState.BetweenRounds;

    public bool IsInRound => State is SessionState.Manual or SessionState.Auto;

    /// <summary>Length of the countdown after "Start Round N" (FR-45). Fixed: there is no setting.</summary>
    public static readonly TimeSpan CountdownLength = TimeSpan.FromSeconds(10);

    /// <summary>When the running countdown reaches zero; <c>null</c> when no countdown runs.</summary>
    public DateTimeOffset? CountdownEndsAt { get; private set; }

    /// <summary>How long players get to read a "Rules updated" notice before the next ball (FR-47).</summary>
    public static readonly TimeSpan RulesReadingTime = TimeSpan.FromSeconds(3);

    /// <summary>No ball may be called before this, after rules changed mid-round (FR-47); <c>null</c> when there is no wait.</summary>
    public DateTimeOffset? NextBallFrom { get; private set; }

    /// <summary>Whether the round being counted down starts in auto mode.</summary>
    public bool StartInAuto { get; private set; }

    /// <summary>Settings in force now.</summary>
    public SessionSettings Settings { get; private set; }

    /// <summary>The selection key of the patterns this round is played for (<see cref="WinSelection"/>), e.g. <c>rows+columns+diagonals</c>.</summary>
    public string WinConditionId { get; private set; }

    /// <summary>The patterns <see cref="WinConditionId"/> names.</summary>
    public WinSelection Selection => Variant.GetSelection(WinConditionId);

    /// <summary>Rounds started so far; 0 in the lobby before the first round.</summary>
    public int RoundNumber { get; private set; }

    /// <summary>The round being played, or between rounds the one that comes next: the round a card dealt now is for.</summary>
    public int UpcomingRound => IsInRound ? RoundNumber : RoundNumber + 1;

    /// <summary>Balls called in the current (or, between rounds, the last) round, in order.</summary>
    public IReadOnlyList<BallCall> Calls => _calls;

    public BallCall? LastCall => _calls.Count == 0 ? null : _calls[^1];

    public int BallsRemaining => Variant.BallMax - _calls.Count;

    /// <summary>Number of the last event raised; clients use it to spot gaps (ADR 0002).</summary>
    public long LastEventSeq { get; private set; }

    /// <summary>How many events each audience has received; the snapshot gives a client its own stream's count.</summary>
    public StreamSeqs StreamSeqs { get; private set; }

    /// <summary>Bumped by every successful command, for optimistic checks in storage.</summary>
    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>Every Bingo press this session, in order (FR-37a).</summary>
    public IReadOnlyList<Claim> Claims => _claims;

    /// <summary>Every win the host credited this session (FR-40).</summary>
    public IReadOnlyList<WinRecord> Wins => _wins;

    /// <summary>One summary per round started (FR-40).</summary>
    public IReadOnlyList<RoundSummary> Rounds => _rounds;

    public bool LeaderboardVisible { get; private set; }

    /// <summary>
    /// What the host shows with the join QR on the display (FR-49), e.g. "Password: harvest2026" or a
    /// hint. Typed by the host, never taken from the password, which is only stored hashed.
    /// </summary>
    public string? JoinNote { get; private set; }

    private RoundSummary? CurrentRound => _rounds.Count == 0 ? null : _rounds[^1];

    public static Result<Session> Create(
        GameCode code,
        BingoVariant variant,
        SessionSettings settings,
        string winConditionId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Validate() is { } invalid)
        {
            return invalid;
        }

        if (!variant.TryGetSelection(winConditionId, out var selection))
        {
            return UnknownWinCondition(variant, winConditionId);
        }

        return Result.Success(new Session(code, variant, settings, selection!.Key, now));
    }

    public bool HasBeenCalled(int number) => _calledNumbers.Contains(number);

    /// <summary>The session as plain data, for storage.</summary>
    public SessionSnapshot ToSnapshot() => new(
        Code.Value,
        _standard.Id,
        State,
        CountdownEndsAt,
        StartInAuto,
        Settings,
        WinConditionId,
        RoundNumber,
        [.. _calls],
        LastEventSeq,
        Version,
        CreatedAt,
        LastActivityAt,
        [.. _claims],
        [.. _wins],
        [.. _rounds],
        LeaderboardVisible,
        [.. _alerted.Select(alert => new WinAlert(alert.Key.ParticipantId, alert.Key.CardId, alert.Key.WinConditionId, alert.Value))],
        NextBallFrom,
        StreamSeqs,
        [.. _awardKeys],
        JoinNote,
        Content?.Name,
        GroupItems,
        BoardSide,
        PlayersMayDownload,
        [.. _customPatterns]);

    /// <summary>
    /// Rebuilds a session saved with <see cref="ToSnapshot"/>, with its custom set (stored apart) when it uses
    /// one. A snapshot naming an unknown variant or game code, or missing its set, is a corrupt record, not a
    /// user error, so it throws.
    /// </summary>
    public static Session Restore(SessionSnapshot snapshot, ContentSet? content = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!GameCode.TryParse(snapshot.Code, out var code))
        {
            throw new InvalidOperationException($"Stored session has an invalid code '{snapshot.Code}'.");
        }

        if (!StandardVariants.TryGet(snapshot.VariantId, out var variant))
        {
            throw new InvalidOperationException($"Stored session {snapshot.Code} uses unknown variant '{snapshot.VariantId}'.");
        }

        // Sessions saved before selections existed say "normal"; read every stored key as today's canonical one.
        string Canonical(string key) => variant.TryGetSelection(key, out var selection) ? selection!.Key : key;

        var session = new Session(code, variant, snapshot.Settings, Canonical(snapshot.WinConditionId), snapshot.CreatedAt)
        {
            State = snapshot.State,
            CountdownEndsAt = snapshot.CountdownEndsAt,
            StartInAuto = snapshot.StartInAuto,
            RoundNumber = snapshot.RoundNumber,
            LastEventSeq = snapshot.LastEventSeq,
            Version = snapshot.Version,
            LastActivityAt = snapshot.LastActivityAt,
            LeaderboardVisible = snapshot.LeaderboardVisible,
            NextBallFrom = snapshot.NextBallFrom,
            StreamSeqs = snapshot.StreamSeqs,
            JoinNote = snapshot.JoinNote,
            BoardSide = snapshot.BoardSide,
            PlayersMayDownload = snapshot.PlayersMayDownload,
        };
        session._calls.AddRange(snapshot.Calls);
        session._calledNumbers.UnionWith(snapshot.Calls.Select(call => call.Number));
        session._claims.AddRange(snapshot.Claims.Select(claim => claim with { WinConditionId = Canonical(claim.WinConditionId) }));
        session._wins.AddRange(snapshot.Wins.Select(win => win with { WinConditionId = Canonical(win.WinConditionId) }));
        session._rounds.AddRange(snapshot.Rounds.Select(round =>
            round.WinConditionsUsed.All(key => Canonical(key) == key) && round.Wins.All(win => Canonical(win.WinConditionId) == win.WinConditionId)
                ? round
                : round with
                {
                    WinConditionsUsed = [.. round.WinConditionsUsed.Select(Canonical)],
                    Wins = [.. round.Wins.Select(win => win with { WinConditionId = Canonical(win.WinConditionId) })],
                }));
        session._awardKeys.AddRange(snapshot.AwardKeys ?? []);
        session._customPatterns.AddRange(snapshot.CustomPatterns ?? []);
        session.Variant = session.RulesFor(snapshot.Settings, content: null).Value;
        if (snapshot.ContentName is not null)
        {
            if (content is null)
            {
                throw new InvalidOperationException($"Stored session {snapshot.Code} uses a custom set that wasn't found.");
            }

            // Saved before a set's groups always meant dealing by group: such a set was dealt free-form.
            if (!snapshot.GroupItems)
            {
                content = content.WithoutGroups();
            }

            var custom = session.RulesFor(snapshot.Settings, content);
            if (!custom.IsSuccess)
            {
                throw new InvalidOperationException($"Stored session {snapshot.Code}'s custom set no longer fits: {custom.Error}");
            }

            session.Content = content;
            session.Variant = custom.Value;
        }

        foreach (var alert in snapshot.Alerts)
        {
            session._alerted[(alert.ParticipantId, alert.CardId, Canonical(alert.WinConditionId))] = alert.Lines;
        }

        return session;
    }

    /// <summary>
    /// "Start Round N" (FR-45): begin a <see cref="CountdownLength"/> countdown. Joining and
    /// redraws close now; at zero the scheduler calls <see cref="StartRound"/>. With
    /// <paramref name="startInAuto"/> the round starts in auto mode and draws its first ball
    /// at zero; otherwise it starts in manual mode (the default).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> StartCountdown(bool startInAuto, DateTimeOffset now)
    {
        if (State != SessionState.BetweenRounds)
        {
            return WrongState("start a round");
        }

        State = SessionState.Starting;
        CountdownEndsAt = now + CountdownLength;
        StartInAuto = startInAuto;
        return Commit(now, new RoundCountdownStarted(RoundNumber + 1, CountdownEndsAt.Value, startInAuto, WinConditionId));
    }

    /// <summary>The host stops the countdown; back to the lobby, and joining reopens.</summary>
    public Result<IReadOnlyList<SessionEvent>> CancelCountdown(DateTimeOffset now)
    {
        if (State != SessionState.Starting)
        {
            return WrongState("cancel the countdown");
        }

        State = SessionState.BetweenRounds;
        CountdownEndsAt = null;
        return Commit(now, new RoundCountdownCanceled(RoundNumber + 1));
    }

    /// <summary>
    /// The countdown reached zero: the round starts with a full hopper, under any settings
    /// that were waiting for it, in manual mode, or in auto mode with its first ball drawn.
    /// Callers reset every participant's marks for the new round afterwards.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> StartRound(IRandomSource random, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (State != SessionState.Starting)
        {
            return WrongState("start the round");
        }

        if (now < CountdownEndsAt)
        {
            return Failure(DomainErrorCode.NotAllowed, "The countdown is still running.");
        }

        ClearCalls();
        RoundNumber++;
        CountdownEndsAt = null;
        _rounds.Add(new RoundSummary(RoundNumber, now, null, [WinConditionId], [], Content?.Name));
        State = SessionState.Manual;

        var events = new List<SessionEvent> { new RoundStarted(RoundNumber) };
        if (StartInAuto)
        {
            State = SessionState.Auto;
            events.Add(new AutoCallingStarted());
            events.AddRange(CallNext(DrawSource.Auto, random, now));
        }

        return Commit(now, events);
    }

    /// <summary>
    /// "Clear cards" (FR-34): end the round if one is running, clear the called balls (the
    /// hopper is full again) and go back to the lobby, where late arrivals can join before
    /// the host starts the next round. Callers clear every participant's marks, and replace
    /// every card when <paramref name="redrawCards"/> is set.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> ClearCards(bool redrawCards, DateTimeOffset now)
    {
        if (State is SessionState.Starting or SessionState.Ended)
        {
            return WrongState("clear the cards");
        }

        var events = new List<SessionEvent>();
        if (IsInRound)
        {
            CloseRound(now);
            events.Add(new RoundEnded(RoundNumber));
        }

        ClearCalls();
        State = SessionState.BetweenRounds;
        events.Add(new CardsCleared(redrawCards));
        return Commit(now, events);
    }

    /// <summary>
    /// "Use this set" (ADR 0009), or back to numbers with <paramref name="content"/> <c>null</c>: lobby only,
    /// since every card is redealt. The free space and patterns stay as they are; the card size too, unless the
    /// set editor sends <paramref name="cardSize"/> with the set (FR-26a). A set with groups is dealt by group
    /// (<see cref="ContentVariant"/>); a set that doesn't fit the card is refused with the reason. The last
    /// round's calls are cleared, because item ids mean something else now. Callers deal every participant a
    /// new card.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> UseContent(ContentSet? content, DateTimeOffset now, CardSize? cardSize = null)
    {
        if (State != SessionState.BetweenRounds)
        {
            return WrongState("change the set");
        }

        // The set editor sends the card size with the set (FR-26a), so a set and its card change in one redeal.
        var settings = cardSize is { } size ? Settings with { CardSize = size } : Settings;
        var rules = RulesFor(settings, content);
        if (!rules.IsSuccess)
        {
            return rules.Error!;
        }

        var variant = rules.Value;
        if (MissingPatterns(variant, WinConditionId) is { } missing)
        {
            return missing;
        }

        var settingsChanged = settings != Settings;
        Settings = settings;

        Content = content;
        PlayersMayDownload = false; // a new set starts private
        Variant = variant;
        ClearCalls();
        return Commit(now, settingsChanged
            ? [new ContentChanged(content?.Name, variant.BallMax, GroupItems), new SettingsChanged(Settings)]
            : [new ContentChanged(content?.Name, variant.BallMax, GroupItems)]);
    }

    /// <summary>End the round early; cards and called balls stay visible until the next round.</summary>
    public Result<IReadOnlyList<SessionEvent>> EndRound(DateTimeOffset now)
    {
        if (!IsInRound)
        {
            return WrongState("end the round");
        }

        State = SessionState.BetweenRounds;
        CloseRound(now);
        return Commit(now, new RoundEnded(RoundNumber));
    }

    /// <summary>
    /// "Auto mode": draw a ball at once, then the scheduler draws every
    /// <see cref="SessionSettings.AutoCallInterval"/> until the host pauses.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> StartAutoCalling(IRandomSource random, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (State != SessionState.Manual)
        {
            return WrongState("switch to auto mode");
        }

        if (StillReadingRules(now) is { } reading)
        {
            return reading;
        }

        if (BallsRemaining == 0)
        {
            return HopperEmpty();
        }

        State = SessionState.Auto;
        return Commit(now, [new AutoCallingStarted(), .. CallNext(DrawSource.Auto, random, now)]);
    }

    /// <summary>"Pause": stop automatic calls and return to manual calling.</summary>
    public Result<IReadOnlyList<SessionEvent>> Pause(DateTimeOffset now)
    {
        if (State != SessionState.Auto)
        {
            return WrongState("pause");
        }

        State = SessionState.Manual;
        return Commit(now, new AutoCallingPaused());
    }

    /// <summary>
    /// Draw a ball from the hopper: the host's "Grab ball" in manual mode, or the
    /// scheduler's tick in auto mode. Each source is refused in the other mode.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> DrawBall(DrawSource source, IRandomSource random, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(random);

        var required = source == DrawSource.Manual ? SessionState.Manual : SessionState.Auto;
        if (State != required)
        {
            return WrongState(source == DrawSource.Manual ? "grab a ball" : "auto-draw a ball");
        }

        if (StillReadingRules(now) is { } reading)
        {
            return reading;
        }

        if (BallsRemaining == 0)
        {
            return HopperEmpty();
        }

        return Commit(now, CallNext(source, random, now));
    }

    /// <summary>
    /// The host's <b>Apply</b> in Options (docs/06): the settings and the win pattern, changed
    /// together in one step. Allowed only in the lobby and in manual mode: in auto mode balls keep
    /// coming and during the countdown the round is about to start, so there is no quiet moment
    /// to announce new rules (FR-32). The round-scoped settings (<see cref="SessionSettings.RoundScopedChanges"/>)
    /// can change only in the lobby. Raises <see cref="SettingsChanged"/> and <see cref="WinConditionChanged"/>
    /// only for what changed, and mid-round a <see cref="RulesUpdated"/> notice (FR-47).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> ApplyRules(SessionSettings settings, string winConditionId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        switch (State)
        {
            case SessionState.Auto:
                return Failure(DomainErrorCode.WrongState, "Pause before changing the rules.");
            case SessionState.Starting:
                return Failure(DomainErrorCode.WrongState, "The round is about to start. Cancel the countdown to change the rules.");
            case SessionState.Ended:
                return WrongState("change the rules");
        }

        // Check everything before changing anything, so a refusal leaves the session as it was.
        if (settings.Validate() is { } invalid)
        {
            return invalid;
        }

        if (IsInRound && Settings.RoundScopedChanges(settings) is [_, ..] lobbyOnly)
        {
            var names = JoinWithAnd(lobbyOnly);
            return Failure(DomainErrorCode.NotAllowed, $"{char.ToUpperInvariant(names[0])}{names[1..]} can only be changed between rounds.");
        }

        // A grouped set's groups are the card's columns, so its card changes with the set, in the set editor.
        if (GroupItems && settings.CardSize != Settings.CardSize)
        {
            return Failure(DomainErrorCode.Invalid, "This set's groups are the card's columns: change the card size in the set editor.");
        }

        // A free space added or taken away, or another card size, changes the card itself (lobby only, checked
        // above): the set must still fill it, and the caller deals everyone a new card (CardShapeChanged).
        var rules = RulesFor(settings, Content);
        if (!rules.IsSuccess)
        {
            return rules.Error!;
        }

        if (MissingPatterns(rules.Value, winConditionId) is { } missing)
        {
            return missing;
        }

        if (!rules.Value.TryGetSelection(winConditionId, out var selection))
        {
            return UnknownWinCondition(rules.Value, winConditionId);
        }

        winConditionId = selection!.Key;

        var events = new List<SessionEvent>();
        List<RuleChange> noticed = winConditionId != WinConditionId ? [new RuleChange("winCondition", winConditionId)] : [];
        noticed.AddRange(SessionSettings.PlayerFacingChanges(Settings, settings));
        CardShapeChanged = false;
        if (settings != Settings)
        {
            CardShapeChanged = !SameShape(rules.Value.Layout, Variant.Layout);
            Settings = settings;
            Variant = rules.Value;
            if (CardShapeChanged)
            {
                ClearCalls(); // the last round's calls were judged on the old cards
            }

            events.Add(new SettingsChanged(Settings));
        }

        if (winConditionId != WinConditionId)
        {
            WinConditionId = winConditionId;
            if (IsInRound && !CurrentRound!.WinConditionsUsed.Contains(winConditionId))
            {
                ReplaceCurrentRound(CurrentRound with { WinConditionsUsed = [.. CurrentRound.WinConditionsUsed, winConditionId] });
            }

            events.Add(new WinConditionChanged(winConditionId));
        }

        // Mid-round, everyone is told what changed, and gets a moment to read it before the next ball (FR-47).
        if (IsInRound && noticed.Count > 0)
        {
            NextBallFrom = now + RulesReadingTime;
            events.Add(new RulesUpdated(noticed, NextBallFrom.Value));
        }

        return events.Count == 0 ? Result.Success<IReadOnlyList<SessionEvent>>([]) : Commit(now, events);
    }

    /// <summary>
    /// The last <see cref="ApplyRules"/> changed the card's shape (its size, or a free space added or taken away), so every
    /// participant needs a new card. Not stored: read straight after the command.
    /// </summary>
    public bool CardShapeChanged { get; private set; }

    /// <summary>
    /// The rules for the settings and set: the standard rules for the card size (this game's own on 5×5),
    /// without the free cell for <see cref="FreeSpace.None"/>, then the set's items as the balls
    /// (<see cref="ContentVariant"/>), or why the set doesn't fit that card.
    /// </summary>
    private Result<BingoVariant> RulesFor(SessionSettings settings, ContentSet? content)
    {
        var standard = settings.CardSize == CardSize.FiveByFive ? _standard : StandardVariants.For(settings.CardSize);
        if (settings.FreeSpace == FreeSpace.None)
        {
            standard = standard.WithoutFreeCell();
        }

        standard = standard.WithPatterns(_customPatterns.Where(pattern => pattern.Size == settings.CardSize).Select(pattern => pattern.ToWinPattern()));

        return content is null ? Result.Success(standard) : ContentVariant.For(standard, content);
    }

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string JoinWithAnd(List<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]}";

    private static bool SameShape(CardLayout a, CardLayout b) =>
        a.Rows == b.Rows && a.Columns == b.Columns && a.FreeCell == b.FreeCell;

    /// <summary>
    /// Why a pattern selection can't be played on the rules' card, e.g. a letter on a 3×3 card (FR-26a), or
    /// <c>null</c> when it can (or isn't a selection of this game at all, which is reported as unknown).
    /// </summary>
    private DomainError? MissingPatterns(BingoVariant rules, string winConditionId)
    {
        if (!Variant.TryGetSelection(winConditionId, out var current) || rules.TryGetSelection(winConditionId, out _))
        {
            return null;
        }

        var missing = current!.Patterns.Where(pattern => !rules.TryGetPattern(pattern.Id, out _)).Select(pattern => pattern.Name).ToList();
        var names = JoinWithAnd(missing);
        return new DomainError(
            DomainErrorCode.Invalid,
            $"A {rules.Layout.Rows} × {rules.Layout.Columns} card has no {names}: choose another pattern too.");
    }

    /// <summary>
    /// A Bingo press (FR-37a). Every press is recorded and sent to the host, valid or not,
    /// like a shout the caller must hear; the host decides. The player is not told
    /// whether it qualified.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> ClaimBingo(Participant participant, int cardId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (!IsInRound)
        {
            return WrongState("call Bingo");
        }

        var matches = participant.Matches(cardId, this);
        if (!matches.IsSuccess)
        {
            return matches.Error!;
        }

        var alreadyAwarded = _wins
            .Where(w => w.Round == RoundNumber && w.ParticipantId == participant.Id && w.CardId == cardId && w.WinConditionId == WinConditionId)
            .Sum(w => w.Wins);
        var alreadyWon = Settings.MultipleWins switch
        {
            MultipleWins.OncePerRound => HasWonThisRound(participant.Id, cardId),
            MultipleWins.OncePerPattern => alreadyAwarded > 0,
            _ => false,
        };
        var awardable = Settings.MultipleWins switch
        {
            MultipleWins.EveryDeclaredWin => Math.Max(0, matches.Value - alreadyAwarded),
            _ => alreadyWon ? 0 : Math.Min(matches.Value, 1),
        };
        var claim = new Claim(
            _claims.Count + 1,
            RoundNumber,
            participant.Id,
            participant.Name.Display,
            cardId,
            WinConditionId,
            now,
            matches.Value,
            alreadyAwarded,
            awardable,
            ClaimStatus.Pending,
            alreadyWon,
            participant.CompletedPatterns(cardId, this));
        _claims.Add(claim);
        return Commit(now, new WinClaimed(claim));
    }

    /// <summary>The host clears a claim without awarding it.</summary>
    public Result<IReadOnlyList<SessionEvent>> DismissClaim(int claimId, DateTimeOffset now)
    {
        if (!IsInRound)
        {
            return WrongState("dismiss a claim");
        }

        var index = _claims.FindIndex(claim => claim.Id == claimId);
        if (index < 0)
        {
            return Failure(DomainErrorCode.Invalid, $"There is no claim {claimId}.");
        }

        if (_claims[index].Status != ClaimStatus.Pending)
        {
            return Failure(DomainErrorCode.NotAllowed, $"Claim {claimId} has already been {_claims[index].Status.ToString().ToLowerInvariant()}.");
        }

        _claims[index] = _claims[index] with { Status = ClaimStatus.Dismissed };
        return Commit(now, new ClaimDismissed(claimId));
    }

    /// <summary>
    /// Win detection after a draw (FR-19): alert the host about cards that meet the win
    /// condition on called numbers. Alerts never repeat: once per card, condition and round,
    /// except that with <see cref="MultipleWins.EveryDeclaredWin"/> a card alerts again when
    /// it completes more lines; with <see cref="MultipleWins.OncePerRound"/> a card that has
    /// won is not checked again. With <see cref="WinDetection.AlertHostAndPause"/> an alert
    /// in auto mode also pauses (back to manual). Marks play no part, so this runs only on
    /// draws, under the lock.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> DetectWins(IEnumerable<Participant> participants, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(participants);

        if (!IsInRound)
        {
            return WrongState("check for wins");
        }

        if (Settings.WinDetection == WinDetection.Off)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        var events = new List<SessionEvent>();
        foreach (var participant in participants)
        {
            foreach (var card in participant.Cards)
            {
                if (Settings.MultipleWins == MultipleWins.OncePerRound && HasWonThisRound(participant.Id, card.Id))
                {
                    continue;
                }

                var matches = CardRules.MatchCount(card.Card, null, this);
                var key = (participant.Id, card.Id, WinConditionId);
                var alreadyAlerted = _alerted.GetValueOrDefault(key);
                var isNew = Settings.MultipleWins == MultipleWins.EveryDeclaredWin ? matches > alreadyAlerted : alreadyAlerted == 0;
                if (matches > 0 && isNew)
                {
                    _alerted[key] = matches;
                    events.Add(new WinDetected(
                        participant.Id, participant.Name.Display, card.Id, WinConditionId, matches, CardRules.CompletedPatterns(card.Card, null, this)));
                }
            }
        }

        if (events.Count == 0)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        if (Settings.WinDetection == WinDetection.AlertHostAndPause && State == SessionState.Auto)
        {
            State = SessionState.Manual;
            events.Add(new AutoCallingPaused());
        }

        return Commit(now, events);
    }

    /// <summary>
    /// The host credits a win on a card for the current win condition (ADR 0008), as
    /// <see cref="SessionSettings.MultipleWins"/> allows (FR-43):
    /// <list type="bullet">
    /// <item><b>Once per round</b>: a card that has won is done until the next round.</item>
    /// <item><b>Once per pattern</b>: one win per card and condition; a new pattern allows another.</item>
    /// <item><b>Every declared win</b>: each award adds <paramref name="wins"/>, e.g. 2 for a "double Bingo".</item>
    /// </list>
    /// Repeating an award that is already recorded changes nothing: the same card and condition
    /// in the "once" modes, the same <paramref name="claimId"/>, or the same <paramref name="requestKey"/>
    /// (an <c>Idempotency-Key</c>, so a double-clicked award for a shouted Bingo counts once) in any mode.
    /// The host may award without any alert, e.g. after checking a shouted Bingo (FR-38).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> AwardWin(
        Participant participant, int? cardId, DateTimeOffset now, int wins = 1, int? claimId = null, string? requestKey = null)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (!IsInRound)
        {
            return WrongState("award a win");
        }

        if (requestKey is not null && (requestKey.Length is 0 or > MaxRequestKeyLength))
        {
            return Failure(DomainErrorCode.Invalid, $"An Idempotency-Key must be 1–{MaxRequestKeyLength} characters.");
        }

        if (requestKey is not null && _awardKeys.Contains(requestKey))
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        var card = cardId ?? (participant.Cards.Count == 1 ? participant.Cards[0].Id : (int?)null);
        if (card is not { } awardedCard || participant.Cards.All(c => c.Id != awardedCard))
        {
            return Failure(DomainErrorCode.Invalid, cardId is null
                ? $"Say which of {participant.Name.Display}'s cards won."
                : $"{participant.Name.Display} has no card {cardId}.");
        }

        if (wins < 1 || (wins > 1 && Settings.MultipleWins != MultipleWins.EveryDeclaredWin))
        {
            return Failure(DomainErrorCode.Invalid, wins < 1
                ? "Award at least one win."
                : "This game counts one win per award. Choose \"Every declared win\" to award several.");
        }

        if (claimId is { } id)
        {
            var claim = _claims.Find(c => c.Id == id);
            if (claim is null || claim.ParticipantId != participant.Id || claim.CardId != awardedCard)
            {
                return Failure(DomainErrorCode.Invalid, $"Claim {id} is not {participant.Name.Display}'s claim for that card.");
            }

            switch (claim.Status)
            {
                case ClaimStatus.Awarded:
                    return Result.Success<IReadOnlyList<SessionEvent>>([]);
                case ClaimStatus.Dismissed:
                    return Failure(DomainErrorCode.NotAllowed, $"Claim {id} was dismissed.");
            }
        }

        var earlier = _wins.Where(w => w.Round == RoundNumber && w.ParticipantId == participant.Id && w.CardId == awardedCard).ToList();
        var samePattern = earlier.Any(w => w.WinConditionId == WinConditionId);
        switch (Settings.MultipleWins)
        {
            case MultipleWins.OncePerRound when samePattern:
            case MultipleWins.OncePerPattern when samePattern:
                // Already won: the award changes nothing, but the card's open claims close, so none stays stuck.
                return CloseOpenClaims(participant.Id, awardedCard, now);
            case MultipleWins.OncePerRound when earlier.Count > 0:
                return Failure(DomainErrorCode.NotAllowed, "This card has already won this round.");
        }

        var source = claimId is not null || _claims.Any(c => IsOpenClaim(c, participant.Id, awardedCard))
            ? WinSource.Claimed
            : _alerted.ContainsKey((participant.Id, awardedCard, WinConditionId))
                ? WinSource.Detected
                : WinSource.Manual;

        for (var i = 0; i < _claims.Count; i++)
        {
            if (IsOpenClaim(_claims[i], participant.Id, awardedCard))
            {
                _claims[i] = _claims[i] with { Status = ClaimStatus.Awarded };
            }
        }

        if (requestKey is not null)
        {
            _awardKeys.Add(requestKey);
            if (_awardKeys.Count > RememberedRequestKeys)
            {
                _awardKeys.RemoveAt(0);
            }
        }

        // Named from the called numbers alone, so the announcement is the same whoever marked what.
        var announced = Selection.Announced(CardRules.CalledMask(participant.Cards.First(c => c.Id == awardedCard).Card, this));
        var win = new WinRecord(RoundNumber, participant.Id, participant.Name.Display, awardedCard, WinConditionId, now, source, wins, announced);
        _wins.Add(win);
        ReplaceCurrentRound(CurrentRound! with { Wins = [.. CurrentRound!.Wins, win] });
        return Commit(now, new WinAwarded(win));
    }

    /// <summary>
    /// "Players can download this set" (ADR 0009): any time before the game ends; downloads themselves work
    /// only in the lobby (<see cref="PlayersCanDownloadNow"/>). Everyone hears, so players' apps show or hide
    /// the button.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> SetContentSharing(bool playersMayDownload, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("change who can download the set");
        }

        if (Content is null)
        {
            return Failure(DomainErrorCode.WrongState, "This game uses numbers, not a custom set.");
        }

        if (playersMayDownload == PlayersMayDownload)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        PlayersMayDownload = playersMayDownload;
        return Commit(now, new ContentSharingChanged(playersMayDownload));
    }

    /// <summary>
    /// Adds a pattern the host drew (FR-21a): a name of up to <see cref="MaxPatternNameLength"/> characters, unlike
    /// every other pattern's, and the squares it needs (cell indexes, row by row) on the card in use. Up to
    /// <see cref="MaxCustomPatterns"/> per game. It is offered at once but not selected; choosing it is an Apply.
    /// Patterns can't be deleted (owner, 2026-10-24): round summaries and wins keep naming them; a host who wants a
    /// clean slate starts a new game.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> AddPattern(string? name, IReadOnlyCollection<int> cells, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (State == SessionState.Ended)
        {
            return WrongState("add a pattern");
        }

        if (_customPatterns.Count >= MaxCustomPatterns)
        {
            return Failure(DomainErrorCode.Invalid, $"A game can have up to {MaxCustomPatterns} patterns of its own.");
        }

        if (CheckPatternName(name, except: null) is { } badName)
        {
            return badName;
        }

        var tidy = ContentText.Tidy(name);

        var layout = Variant.Layout;
        if (cells.Any(cell => cell < 0 || cell >= layout.CellCount))
        {
            return Failure(DomainErrorCode.Invalid, "Every square of the pattern must be on the card.");
        }

        var mask = cells.Aggregate(0u, (all, cell) => all | (1u << cell));
        if ((mask & ~layout.FreeCellMask) == 0)
        {
            return Failure(DomainErrorCode.Invalid, "Pick at least one square besides the free space.");
        }

        if (Variant.Patterns.FirstOrDefault(pattern => pattern.Masks is [var only] && only == mask) is { } same)
        {
            return Failure(DomainErrorCode.Invalid, $"That's the same shape as '{same.Name}'.");
        }

        _customPatterns.Add(new CustomPattern($"custom-{_customPatterns.Count + 1}", tidy, Settings.CardSize, mask));
        Variant = RulesFor(Settings, Content).Value; // the set already fits this card; only the patterns grow
        return Commit(now, new PatternsChanged());
    }

    /// <summary>
    /// Renames one of the host's patterns (FR-21a), e.g. a mistake to "OLD – Plus" or "Do not use", since patterns
    /// can't be deleted. Its id stays, so past rounds and wins still point at it.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> RenamePattern(string id, string? name, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("rename a pattern");
        }

        var index = _customPatterns.FindIndex(pattern => pattern.Id == id);
        if (index < 0)
        {
            return Failure(DomainErrorCode.NotFound, "That pattern isn't one of this game's own.");
        }

        if (CheckPatternName(name, except: id) is { } badName)
        {
            return badName;
        }

        var tidy = ContentText.Tidy(name);
        if (tidy == _customPatterns[index].Name)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        _customPatterns[index] = _customPatterns[index] with { Name = tidy };
        Variant = RulesFor(Settings, Content).Value;
        return Commit(now, new PatternsChanged());
    }

    /// <summary>A pattern's name: up to <see cref="MaxPatternNameLength"/> printable characters, unlike any other pattern's.</summary>
    private DomainError? CheckPatternName(string? name, string? except)
    {
        var tidy = ContentText.Tidy(name);
        if (tidy.Length == 0 || !ContentText.Fits(tidy, MaxPatternNameLength) || !ContentText.IsPrintable(tidy))
        {
            return new DomainError(DomainErrorCode.Invalid, $"Give the pattern a name of up to {MaxPatternNameLength} characters.");
        }

        var named = _customPatterns.Where(pattern => pattern.Id != except).Select(pattern => pattern.Name)
            .Concat(Variant.Patterns.Where(pattern => pattern.Id != except && !_customPatterns.Exists(own => own.Id == pattern.Id)).Select(pattern => pattern.Name));
        return named.Any(other => ContentText.Key(other) == ContentText.Key(tidy))
            ? new DomainError(DomainErrorCode.Invalid, $"There is already a pattern called '{tidy}'.")
            : null;
    }

    /// <summary>
    /// The host's "Call board shows" (FR-29), under Displays: which face of a custom set's items every call
    /// board shows. Presentation, not a rule, so like the join note it changes in any state but Ended, at
    /// once; everyone hears, since every board changes.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> SetBoardSide(BoardSide side, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("change the call board");
        }

        if (!Enum.IsDefined(side))
        {
            return Failure(DomainErrorCode.Invalid, "The call board shows the ball side or the square side.");
        }

        if (side == BoardSide)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        BoardSide = side;
        return Commit(now, new BoardSideChanged(side));
    }

    /// <summary>
    /// Set or clear the join note (FR-49). Not a rule, so it can change in any state but Ended, even in
    /// auto mode; only the host and the display hear about it.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> SetJoinNote(string? note, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("change the join note");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > MaxJoinNoteLength)
        {
            return Failure(DomainErrorCode.Invalid, $"The note can be up to {MaxJoinNoteLength} characters.");
        }

        if (trimmed == JoinNote)
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        JoinNote = trimmed;
        return Commit(now, new JoinNoteChanged(JoinNote));
    }

    /// <summary>Show or hide the leaderboard on participants' screens and the display (FR-41).</summary>
    public Result<IReadOnlyList<SessionEvent>> ShowLeaderboard(bool visible, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("show the leaderboard");
        }

        LeaderboardVisible = visible;
        return Commit(now, new LeaderboardVisibilityChanged(visible));
    }

    /// <summary>
    /// Remove a participant (FR-7), e.g. one who keeps calling false Bingos. Their pending
    /// claims are dismissed; wins already awarded stay in the history. The caller deletes
    /// the participant and releases their name and card fingerprints.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> RemoveParticipant(Participant participant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(participant);

        return State == SessionState.Ended
            ? WrongState("remove a participant")
            : Drop(participant, now, new ParticipantRemoved(participant.Id, participant.Name.Display));
    }

    /// <summary>
    /// A participant leaves on their own, at any time (they can join again only between rounds).
    /// The same as being removed, except that everyone hears "Ava left the game".
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> Leave(Participant participant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(participant);

        return State == SessionState.Ended
            ? WrongState("leave")
            : Drop(participant, now, new ParticipantLeft(participant.Id, participant.Name.Display));
    }

    /// <summary>Dismisses the participant's pending claims, then announces that they are gone.</summary>
    private Result<IReadOnlyList<SessionEvent>> Drop(Participant participant, DateTimeOffset now, SessionEvent gone)
    {
        var events = new List<SessionEvent>();
        for (var i = 0; i < _claims.Count; i++)
        {
            if (_claims[i].ParticipantId == participant.Id && _claims[i].Status == ClaimStatus.Pending)
            {
                _claims[i] = _claims[i] with { Status = ClaimStatus.Dismissed };
                events.Add(new ClaimDismissed(_claims[i].Id));
            }
        }

        events.Add(gone);
        return Commit(now, events);
    }

    /// <summary>Wins per participant across the session, most first (FR-41).</summary>
    public IReadOnlyList<LeaderboardEntry> Leaderboard() =>
        [.. _wins
            .GroupBy(win => win.ParticipantId)
            .Select(group => new LeaderboardEntry(group.Key, group.Last().Name, group.Sum(win => win.Wins)))
            .OrderByDescending(entry => entry.Wins)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Someone joining now waits for the round to end (the waiting room, A3 amended 2026-10-25): during the
    /// countdown and the round. They get a card when the game is back in the lobby.
    /// </summary>
    public bool JoinersWait => State is SessionState.Starting or SessionState.Manual or SessionState.Auto;

    /// <summary>
    /// Whether one more participant may join (FR-5, A7): any time until the game ends, up to the player limit and
    /// the number of different cards the rules make. During a round they wait (<see cref="JoinersWait"/>).
    /// </summary>
    public Result<int> CheckCanAdmit(int currentParticipants)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("join");
        }

        if (currentParticipants >= MaxParticipants)
        {
            return Failure(DomainErrorCode.SessionFull, $"This game is full ({MaxParticipants} players).");
        }

        // A small grouped set can make fewer different cards than the game has room for (FR-39).
        return currentParticipants >= Variant.PossibleCards
            ? NoUniqueCards(Variant.PossibleCards)
            : Result.Success((int)Math.Min(MaxParticipants - currentParticipants, Variant.PossibleCards - currentParticipants));
    }

    public Result<IReadOnlyList<SessionEvent>> End(DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("end the game");
        }

        State = SessionState.Ended;
        return Commit(now, new SessionEnded());
    }

    private bool IsOpenClaim(Claim claim, string participantId, int cardId) =>
        claim.Round == RoundNumber
        && claim.ParticipantId == participantId
        && claim.CardId == cardId
        && claim.WinConditionId == WinConditionId
        && claim.Status == ClaimStatus.Pending;

    /// <summary>Dismisses a card's open claims, e.g. ones made after it already won; nothing to close, nothing changes.</summary>
    private Result<IReadOnlyList<SessionEvent>> CloseOpenClaims(string participantId, int cardId, DateTimeOffset now)
    {
        var closed = new List<SessionEvent>();
        for (var i = 0; i < _claims.Count; i++)
        {
            if (IsOpenClaim(_claims[i], participantId, cardId))
            {
                _claims[i] = _claims[i] with { Status = ClaimStatus.Dismissed };
                closed.Add(new ClaimDismissed(_claims[i].Id));
            }
        }

        return closed.Count == 0 ? Result.Success<IReadOnlyList<SessionEvent>>([]) : Commit(now, closed);
    }

    private bool HasWonThisRound(string participantId, int cardId) =>
        _wins.Any(w => w.Round == RoundNumber && w.ParticipantId == participantId && w.CardId == cardId);

    private void ClearCalls()
    {
        _calls.Clear();
        _calledNumbers.Clear();
        _alerted.Clear();
        NextBallFrom = null;
    }

    private void CloseRound(DateTimeOffset now) => ReplaceCurrentRound(CurrentRound! with { EndedAt = now });

    private void ReplaceCurrentRound(RoundSummary summary) => _rounds[^1] = summary;

    /// <summary>
    /// Calls the next ball. In auto mode the last ball also returns to manual mode (FR-10):
    /// nothing is left to call, so the schedule stops and the host is back on the manual controls.
    /// </summary>
    private List<SessionEvent> CallNext(DrawSource source, IRandomSource random, DateTimeOffset now)
    {
        List<SessionEvent> events = [CallNextBall(source, random, now)];
        if (State == SessionState.Auto && BallsRemaining == 0)
        {
            State = SessionState.Manual;
            events.Add(new AutoCallingPaused());
        }

        return events;
    }

    private BallCalled CallNextBall(DrawSource source, IRandomSource random, DateTimeOffset now)
    {
        var number = PickFromHopper(random);
        var previous = LastCall?.Number;
        var pair = Content?[number];
        var call = new BallCall(_calls.Count + 1, number, Variant.LabelOf(number), now, source, pair?.Ball, pair?.Square);
        _calls.Add(call);
        _calledNumbers.Add(number);
        return new BallCalled(call, previous);
    }

    /// <summary>Every card these rules can deal is held: no one else can get a card of their own (FR-39).</summary>
    public static DomainError NoUniqueCards(long possibleCards) =>
        new(DomainErrorCode.SessionFull, $"No unique cards available: all {possibleCards.ToString("N0", CultureInfo.InvariantCulture)} possible cards are in use. The host can add entries to the set.");

    private static DomainError HopperEmpty() =>
        new(DomainErrorCode.HopperEmpty, "Every ball has been called. Clear the cards to start a new round.");

    private int PickFromHopper(IRandomSource random)
    {
        // The n-th uncalled ball, counting up from 1: uniform over the hopper.
        var target = random.NextInt(BallsRemaining);
        for (var number = 1; number <= Variant.BallMax; number++)
        {
            if (!_calledNumbers.Contains(number) && target-- == 0)
            {
                return number;
            }
        }

        throw new InvalidOperationException("The hopper count and the called balls disagree.");
    }

    private Result<IReadOnlyList<SessionEvent>> Commit(DateTimeOffset now, params IEnumerable<SessionEvent> events)
    {
        var numbered = new List<SessionEvent>();
        foreach (var e in events)
        {
            var audience = AudienceOf(e);
            var streams = StreamSeqs;
            StreamSeqs = new StreamSeqs(
                audience.HasFlag(Audience.Players) ? streams.Players + 1 : streams.Players,
                audience.HasFlag(Audience.Host) ? streams.Host + 1 : streams.Host,
                audience.HasFlag(Audience.Display) ? streams.Display + 1 : streams.Display);
            numbered.Add(e with
            {
                Seq = ++LastEventSeq,
                Audience = audience,
                Streams = new StreamSeqs(
                    audience.HasFlag(Audience.Players) ? StreamSeqs.Players : 0,
                    audience.HasFlag(Audience.Host) ? StreamSeqs.Host : 0,
                    audience.HasFlag(Audience.Display) ? StreamSeqs.Display : 0),
            });
        }

        Version++;
        LastActivityAt = now;
        return Result.Success<IReadOnlyList<SessionEvent>>(numbered);
    }

    /// <summary>
    /// Who receives an event (docs/05 §4), decided here so that it is counted in the same step as
    /// the change. The host hears everything; the display and players never hear claims, alerts or
    /// the auto/manual switch, and players hear the balls only when a setting shows them. Wins and
    /// removals reach everyone ("Sam had Bingo!", "Sam was removed from the game", "Sam left the game").
    /// </summary>
    private Audience AudienceOf(SessionEvent e) => e switch
    {
        BallCalled => Settings.PlayersSeeCalls ? Audience.Everyone : Audience.Host | Audience.Display,
        AutoCallingStarted or AutoCallingPaused or WinClaimed or ClaimDismissed or WinDetected => Audience.Host,
        JoinNoteChanged => Audience.Host | Audience.Display,
        _ => Audience.Everyone,
    };

    /// <summary>Refuses a ball while players still have time to read new rules (FR-47).</summary>
    private DomainError? StillReadingRules(DateTimeOffset now) =>
        now < NextBallFrom
            ? Failure(DomainErrorCode.WrongState, "Give everyone a moment to read the new rules.")
            : null;

    private DomainError WrongState(string action) =>
        new(DomainErrorCode.WrongState, State == SessionState.Ended
            ? "This game has ended."
            : $"You can't {action} while the game is {Describe(State)}.");

    private static DomainError Failure(DomainErrorCode code, string message) => new(code, message);

    private static DomainError UnknownWinCondition(BingoVariant variant, string id) =>
        new(DomainErrorCode.Invalid, $"'{id}' is not a win condition of variant '{variant.Id}'.");

    private static string Describe(SessionState state) => state switch
    {
        SessionState.BetweenRounds => "between rounds",
        SessionState.Starting => "counting down to the next round",
        SessionState.Manual => "in manual mode",
        SessionState.Auto => "in auto mode",
        SessionState.Ended => "ended",
        _ => state.ToString(),
    };
}
