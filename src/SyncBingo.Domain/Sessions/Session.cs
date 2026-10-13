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
    /// <summary>Lines already alerted per card and win condition this round, so alerts never repeat.</summary>
    private readonly Dictionary<(string ParticipantId, int CardId, string WinConditionId), int> _alerted = [];

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

    /// <summary>Length of the countdown after "Start Round N" (FR-45). Fixed: there is no setting.</summary>
    public static readonly TimeSpan CountdownLength = TimeSpan.FromSeconds(10);

    /// <summary>When the running countdown reaches zero; <c>null</c> when no countdown runs.</summary>
    public DateTimeOffset? CountdownEndsAt { get; private set; }

    /// <summary>Whether the round being counted down starts in auto mode.</summary>
    public bool StartInAuto { get; private set; }

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

    /// <summary>The round being played, or between rounds the one that comes next: the round a card dealt now is for.</summary>
    public int UpcomingRound => IsInRound ? RoundNumber : RoundNumber + 1;

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
        return Commit(now, new RoundCountdownStarted(RoundNumber + 1, CountdownEndsAt.Value, startInAuto));
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
        return Commit(now, new RoundCountdownCancelled(RoundNumber + 1));
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

        if (NextRoundSettings is { } next)
        {
            Settings = next;
            NextRoundSettings = null;
        }

        ClearCalls();
        RoundNumber++;
        CountdownEndsAt = null;
        _rounds.Add(new RoundSummary(RoundNumber, now, null, [WinConditionId], []));
        State = SessionState.Manual;

        var events = new List<SessionEvent> { new RoundStarted(RoundNumber) };
        if (StartInAuto)
        {
            State = SessionState.Auto;
            events.Add(new AutoCallingStarted());
            events.Add(CallNextBall(DrawSource.Auto, random, now));
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

        var matches = participant.Matches(cardId, this);
        if (!matches.IsSuccess)
        {
            return matches.Error!;
        }

        var alreadyAwarded = _wins
            .Where(w => w.Round == RoundNumber && w.ParticipantId == participant.Id && w.CardId == cardId && w.WinConditionId == WinConditionId)
            .Sum(w => w.Wins);
        var awardable = Settings.MultipleWins switch
        {
            MultipleWins.EveryDeclaredWin => Math.Max(0, matches.Value - alreadyAwarded),
            MultipleWins.OncePerRound when HasWonThisRound(participant.Id, cardId) => 0,
            _ => alreadyAwarded == 0 ? Math.Min(matches.Value, 1) : 0,
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
            ClaimStatus.Pending);
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
                    events.Add(new WinDetected(participant.Id, participant.Name.Display, card.Id, WinConditionId, matches));
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
    /// in the "once" modes, or the same <paramref name="claimId"/> in any mode. The host may
    /// award without any alert, e.g. after checking a shouted Bingo (FR-38).
    /// </summary>
    public Result<IReadOnlyList<SessionEvent>> AwardWin(
        Participant participant, int? cardId, DateTimeOffset now, int wins = 1, int? claimId = null)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (!IsInRound)
        {
            return WrongState("award a win");
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
                return Result.Success<IReadOnlyList<SessionEvent>>([]);
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

        var win = new WinRecord(RoundNumber, participant.Id, participant.Name.Display, awardedCard, WinConditionId, now, source, wins);
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
            .Select(group => new LeaderboardEntry(group.Key, group.Last().Name, group.Sum(win => win.Wins)))
            .OrderByDescending(entry => entry.Wins)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];

    public Result<int> CheckCanAdmit(int currentParticipants)
    {
        if (State != SessionState.BetweenRounds)
        {
            return State == SessionState.Ended
                ? WrongState("join")
                : Failure(DomainErrorCode.WrongState, "A round is in progress. You can join between rounds.");
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

    private bool IsOpenClaim(Claim claim, string participantId, int cardId) =>
        claim.Round == RoundNumber
        && claim.ParticipantId == participantId
        && claim.CardId == cardId
        && claim.WinConditionId == WinConditionId
        && claim.Status == ClaimStatus.Pending;

    private bool HasWonThisRound(string participantId, int cardId) =>
        _wins.Any(w => w.Round == RoundNumber && w.ParticipantId == participantId && w.CardId == cardId);

    private void ClearCalls()
    {
        _calls.Clear();
        _calledNumbers.Clear();
        _alerted.Clear();
    }

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
        SessionState.Starting => "counting down to the next round",
        SessionState.Manual => "in manual mode",
        SessionState.Auto => "in auto mode",
        SessionState.Ended => "ended",
        _ => state.ToString(),
    };
}
