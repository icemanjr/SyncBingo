using SyncBingo.Application.Ports;

namespace SyncBingo.Infrastructure;

/// <summary><see cref="IClock"/> on a <see cref="TimeProvider"/>, so tests can share one controllable clock.</summary>
public sealed class SystemClock(TimeProvider time) : IClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
