using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncBingo.Application.Calling;

namespace SyncBingo.Infrastructure.Scheduling;

/// <summary>
/// Runs on every API instance (docs/02 §5): every poll it claims the sessions that are due and
/// runs their tick, which starts a counted-down round or draws the next auto-call ball, then
/// re-schedules them. A tick the rules refuse (an empty hopper, a session that is gone) takes the
/// session off the schedule; a tick that crashes is left to its lease, so another poll retries it.
/// </summary>
public sealed partial class CallSchedulerService(
    RedisCallScheduler scheduler,
    DrawHandler draws,
    CallSchedulerOptions options,
    TimeProvider time,
    ILogger<CallSchedulerService> logger) : BackgroundService
{
    /// <summary>One poll: claim what is due and tick it. Returns how many sessions were claimed.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var claims = await scheduler.ClaimDueAsync(options.Lease, options.MaxClaimsPerPoll, cancellationToken).ConfigureAwait(false);
        await Parallel.ForEachAsync(
                claims,
                new ParallelOptions { MaxDegreeOfParallelism = options.MaxParallelTicks, CancellationToken = cancellationToken },
                TickAsync)
            .ConfigureAwait(false);
        return claims.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                PollFailed(logger, exception);
            }
        }
    }

    private async ValueTask TickAsync(ScheduleClaim claim, CancellationToken cancellationToken)
    {
        try
        {
            var result = await draws.TickAsync(claim.Code, cancellationToken).ConfigureAwait(false);
            if (result.Error is { } refused)
            {
                TickRefused(logger, claim.LogCode, refused.Code);

                await scheduler.DropIfStillClaimedAsync(claim, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TickFailed(logger, claim.LogCode, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduler poll failed")]
    private static partial void PollFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tick for session {Code}… failed; it retries when the lease ends")]
    private static partial void TickFailed(ILogger logger, string code, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tick for session {Code}… refused ({Reason}); taken off the schedule")]
    private static partial void TickRefused(ILogger logger, string code, Domain.DomainErrorCode reason);
}
