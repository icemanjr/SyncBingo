namespace SyncBingo.Infrastructure.Scheduling;

/// <summary>Settings for <see cref="CallSchedulerService"/> (docs/02 §5).</summary>
public sealed record CallSchedulerOptions
{
    /// <summary>How often each instance looks for due sessions.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long a claim keeps other instances away. Longer than any tick takes; if the claiming
    /// instance dies, the session is picked up again this long after the claim.
    /// </summary>
    public TimeSpan Lease { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Most sessions one instance claims per poll, so one instance cannot take on every session at once.</summary>
    public int MaxClaimsPerPoll { get; init; } = 50;

    /// <summary>Claimed sessions ticked at the same time.</summary>
    public int MaxParallelTicks { get; init; } = 8;
}
