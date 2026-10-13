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
        }

        if (NextRoundSettings is { } next)
        {
            Settings = next;
            NextRoundSettings = null;
        }

        _calls.Clear();
        _calledNumbers.Clear();
        RoundNumber++;
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
