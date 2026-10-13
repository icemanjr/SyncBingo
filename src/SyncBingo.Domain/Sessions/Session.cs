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

    private readonly List<BallCall> _calls = [];
    private readonly HashSet<int> _calledNumbers = [];
    private readonly List<Claim> _claims = [];
    private readonly List<WinRecord> _wins = [];
    private readonly List<RoundSummary> _rounds = [];
    private readonly HashSet<(string ParticipantId, int CardId, string WinConditionId)> _alerted = [];

    private Session(GameCode code, BingoVariant variant, SessionSettings settings, string winConditionId, DateTimeOffset now)
    {
        Code = code;
        Variant = variant;
        Settings = settings;
        WinConditionId = winConditionId;
        CreatedAt = now;
        LastActivityAt = now;
    }

    public GameCode Code { get; }

    public BingoVariant Variant { get; }

    public SessionState State { get; private set; } = SessionState.BetweenRounds;

    public bool IsInRound => State is SessionState.Manual or SessionState.Auto;

    /// <summary>Settings in force now.</summary>
    public SessionSettings Settings { get; private set; }

    /// <summary>
    /// Settings saved during a round whose round-scoped values (<see cref="SessionSettings.FreeSpace"/>,
    /// <see cref="SessionSettings.RedrawPolicy"/>) wait for the next round; <c>null</c> when nothing waits.
    /// </summary>
    public SessionSettings? NextRoundSettings { get; private set; }

    /// <summary>Id of the current <see cref="WinPattern"/>, e.g. <c>normal</c>.</summary>
    public string WinConditionId { get; private set; }

    /// <summary>Rounds started so far; 0 in the lobby before the first round.</summary>
    public int RoundNumber { get; private set; }

    /// <summary>Balls called in the current (or, between rounds, the last) round, in order.</summary>
    public IReadOnlyList<BallCall> Calls => _calls;

    public BallCall? LastCall => _calls.Count == 0 ? null : _calls[^1];

    public int BallsRemaining => Variant.BallMax - _calls.Count;

    /// <summary>Number of the last event raised; clients use it to spot gaps (ADR 0002).</summary>
    public long LastEventSeq { get; private set; }

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

        if (!variant.TryGetPattern(winConditionId, out _))
        {
            return UnknownWinCondition(variant, winConditionId);
        }

        return Result.Success(new Session(code, variant, settings, winConditionId, now));
    }

    public bool HasBeenCalled(int number) => _calledNumbers.Contains(number);

    /// <summary>
    /// Start the next round, or "Clear cards" mid-round (ends the current one first).
    /// Every round starts in <see cref="SessionState.Manual"/>: nothing is called until the
    /// host grabs a ball or switches to auto mode.
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> StartRound(bool redrawCards, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("start a round");
        }

        var events = new List<SessionEvent>();
        if (IsInRound)
        {
            events.Add(new RoundEnded(RoundNumber));
            CloseRound(now);
        }

        if (NextRoundSettings is { } next)
        {
            Settings = next;
            NextRoundSettings = null;
        }

        _calls.Clear();
        _calledNumbers.Clear();
        _alerted.Clear();
        RoundNumber++;
        _rounds.Add(new RoundSummary(RoundNumber, now, null, [WinConditionId], []));
        State = SessionState.Manual;
        events.Add(new RoundStarted(RoundNumber, redrawCards));
        return Commit(now, events);
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

        if (BallsRemaining == 0)
        {
            return HopperEmpty();
        }

        State = SessionState.Auto;
        return Commit(now, new AutoCallingStarted(), CallNextBall(DrawSource.Auto, random, now));
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

        if (BallsRemaining == 0)
        {
            return HopperEmpty();
        }

        return Commit(now, CallNextBall(source, random, now));
    }

    public Result<IReadOnlyList<SessionEvent>> ChangeWinCondition(string winConditionId, DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("change the win condition");
        }

        if (!Variant.TryGetPattern(winConditionId, out _))
        {
            return UnknownWinCondition(Variant, winConditionId);
        }

        WinConditionId = winConditionId;
        if (IsInRound && !CurrentRound!.WinConditionsUsed.Contains(winConditionId))
        {
            ReplaceCurrentRound(CurrentRound with { WinConditionsUsed = [.. CurrentRound.WinConditionsUsed, winConditionId] });
        }

        return Commit(now, new WinConditionChanged(winConditionId));
    }

    /// <summary>
    /// Replace the settings. Between rounds everything applies at once; during a round
    /// the round-scoped settings wait for the next round (<see cref="NextRoundSettings"/>).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> UpdateSettings(SessionSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (State == SessionState.Ended)
        {
            return WrongState("change settings");
        }

        if (settings.Validate() is { } invalid)
        {
            return invalid;
        }

        if (IsInRound)
        {
            var current = settings.KeepRoundScopedFrom(Settings);
            NextRoundSettings = current == settings ? null : settings;
            Settings = current;
        }
        else
        {
            Settings = settings;
            NextRoundSettings = null;
        }

        return Commit(now, new SettingsChanged(Settings, NextRoundSettings));
    }

    /// <summary>Whether one more participant may join now (FR-5, A3, A7).</summary>
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

        if (!Settings.BingoButton)
        {
            return Failure(DomainErrorCode.NotAllowed, "The Bingo button is off in this game. Shout \"Bingo!\" instead.");
        }

        var qualified = participant.Qualifies(cardId, this);
        if (!qualified.IsSuccess)
        {
            return qualified.Error!;
        }

        var claim = new Claim(
            _claims.Count + 1, RoundNumber, participant.Id, participant.Name.Display, cardId, WinConditionId, now, qualified.Value, ClaimStatus.Pending);
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
    /// Win detection after a draw (FR-19): alert the host about every card that now meets
    /// the win condition on called numbers, at most once per card, condition and round.
    /// With <see cref="WinDetection.AlertHostAndPause"/> an alert in auto mode also pauses
    /// (back to manual). Marks play no part, so this runs only on draws, under the lock.
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
                if (CardRules.MeetsWinCondition(card.Card, this) && _alerted.Add((participant.Id, card.Id, WinConditionId)))
                {
                    events.Add(new WinDetected(participant.Id, participant.Name.Display, card.Id, WinConditionId));
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
    /// The host credits a win for the current win condition (ADR 0008). Awarding the same
    /// participant twice for the same condition in a round changes nothing. The host may
    /// award without any alert, e.g. after checking a shouted Bingo (FR-38).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> AwardWin(Participant participant, int? cardId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (!IsInRound)
        {
            return WrongState("award a win");
        }

        if (cardId is { } id && participant.Cards.All(card => card.Id != id))
        {
            return Failure(DomainErrorCode.Invalid, $"{participant.Name.Display} has no card {id}.");
        }

        if (_wins.Any(win => win.Round == RoundNumber && win.ParticipantId == participant.Id && win.WinConditionId == WinConditionId))
        {
            return Result.Success<IReadOnlyList<SessionEvent>>([]);
        }

        var source = _claims.Any(claim => IsOpenClaimBy(claim, participant.Id))
            ? WinSource.Claimed
            : _alerted.Any(alert => alert.ParticipantId == participant.Id && alert.WinConditionId == WinConditionId)
                ? WinSource.Detected
                : WinSource.Manual;

        for (var i = 0; i < _claims.Count; i++)
        {
            if (IsOpenClaimBy(_claims[i], participant.Id))
            {
                _claims[i] = _claims[i] with { Status = ClaimStatus.Awarded };
            }
        }

        var win = new WinRecord(RoundNumber, participant.Id, participant.Name.Display, cardId, WinConditionId, now, source);
        _wins.Add(win);
        ReplaceCurrentRound(CurrentRound! with { Wins = [.. CurrentRound!.Wins, win] });
        return Commit(now, new WinAwarded(win));
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

        if (State == SessionState.Ended)
        {
            return WrongState("remove a participant");
        }

        var events = new List<SessionEvent>();
        for (var i = 0; i < _claims.Count; i++)
        {
            if (_claims[i].ParticipantId == participant.Id && _claims[i].Status == ClaimStatus.Pending)
            {
                _claims[i] = _claims[i] with { Status = ClaimStatus.Dismissed };
                events.Add(new ClaimDismissed(_claims[i].Id));
            }
        }

        events.Add(new ParticipantRemoved(participant.Id, participant.Name.Display));
        return Commit(now, events);
    }

    /// <summary>Wins per participant across the session, most first (FR-41).</summary>
    public IReadOnlyList<LeaderboardEntry> Leaderboard() =>
        [.. _wins
            .GroupBy(win => win.ParticipantId)
            .Select(group => new LeaderboardEntry(group.Key, group.Last().Name, group.Count()))
            .OrderByDescending(entry => entry.Wins)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];

    public Result<int> CheckCanAdmit(int currentParticipants)
    {
        if (State != SessionState.BetweenRounds)
        {
            return State == SessionState.Ended
                ? WrongState("join")
                : Failure(DomainErrorCode.WrongState, "A round is in progress. You can join when it ends.");
        }

        return currentParticipants >= MaxParticipants
            ? Failure(DomainErrorCode.SessionFull, $"This game is full ({MaxParticipants} players).")
            : Result.Success(MaxParticipants - currentParticipants);
    }

    public Result<IReadOnlyList<SessionEvent>> End(DateTimeOffset now)
    {
        if (State == SessionState.Ended)
        {
            return WrongState("end the session");
        }

        State = SessionState.Ended;
        return Commit(now, new SessionEnded());
    }

    private bool IsOpenClaimBy(Claim claim, string participantId) =>
        claim.Round == RoundNumber
        && claim.ParticipantId == participantId
        && claim.WinConditionId == WinConditionId
        && claim.Status == ClaimStatus.Pending;

    private void CloseRound(DateTimeOffset now) => ReplaceCurrentRound(CurrentRound! with { EndedAt = now });

    private void ReplaceCurrentRound(RoundSummary summary) => _rounds[^1] = summary;

    private BallCalled CallNextBall(DrawSource source, IRandomSource random, DateTimeOffset now)
    {
        var number = PickFromHopper(random);
        var previous = LastCall?.Number;
        var call = new BallCall(_calls.Count + 1, number, Variant.Columns[Variant.ColumnOf(number)].Label, now, source);
        _calls.Add(call);
        _calledNumbers.Add(number);
        return new BallCalled(call, previous);
    }

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
        var numbered = events.Select(e => e with { Seq = ++LastEventSeq }).ToList();
        Version++;
        LastActivityAt = now;
        return Result.Success<IReadOnlyList<SessionEvent>>(numbered);
    }

    private DomainError WrongState(string action) =>
        new(DomainErrorCode.WrongState, $"Cannot {action} while the session is {Describe(State)}.");

    private static DomainError Failure(DomainErrorCode code, string message) => new(code, message);

    private static DomainError UnknownWinCondition(BingoVariant variant, string id) =>
        new(DomainErrorCode.Invalid, $"'{id}' is not a win condition of variant '{variant.Id}'.");

    private static string Describe(SessionState state) => state switch
    {
        SessionState.BetweenRounds => "between rounds",
        SessionState.Manual => "in manual mode",
        SessionState.Auto => "in auto mode",
        SessionState.Ended => "ended",
        _ => state.ToString(),
    };
}
