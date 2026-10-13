namespace SyncBingo.Domain;

/// <summary>Why the domain refused a command. The API maps these to HTTP status codes.</summary>
public enum DomainErrorCode
{
    /// <summary>The command is not allowed in the current session state (409).</summary>
    WrongState,

    /// <summary>A setting or rule forbids the command (422).</summary>
    NotAllowed,

    /// <summary>The input is not valid for this session (422).</summary>
    Invalid,

    /// <summary>Every ball has been called; start a new round (409).</summary>
    HopperEmpty,

    /// <summary>The session has reached its participant limit (409).</summary>
    SessionFull,
}

/// <summary>An expected refusal, such as a draw while the round is over.</summary>
public sealed record DomainError(DomainErrorCode Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>
/// The outcome of a domain command: a value, or a <see cref="DomainError"/>.
/// Expected refusals are results, not exceptions; exceptions mean a bug.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T value)
    {
        _value = value;
    }

    internal Result(DomainError error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public DomainError? Error { get; }

    /// <summary>The value of a successful result.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result failed: {Error}");

    public static implicit operator Result<T>(DomainError error) => new(error);

    public override string ToString() => IsSuccess ? $"Success: {_value}" : $"Failure: {Error}";
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(DomainErrorCode code, string message) => new(new DomainError(code, message));
}
