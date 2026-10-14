using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SyncBingo.Application.Ports;

namespace SyncBingo.Api.Http;

/// <summary>
/// Limits on how fast callers may act (docs/05 §5), from configuration section <c>RateLimits</c>.
/// Counts are kept per API instance, which is enough for a guard against abuse.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Games one IP address may create per hour.</summary>
    public int CreatesPerHour { get; set; } = 30;

    /// <summary>
    /// Wrong game passwords per game and IP address allowed within <see cref="WrongPasswordWindow"/>.
    /// Only <i>wrong</i> passwords count, and the limit is high, because a whole event (up to 100 people)
    /// can share one WiFi address and children mistype. Going past it means a bot or something broken,
    /// so joining that game from that address is then locked for <see cref="WrongPasswordLockout"/>.
    /// </summary>
    public int WrongPasswords { get; set; } = 60;

    public TimeSpan WrongPasswordWindow { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan WrongPasswordLockout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Wrong passwords within the window after which the host is warned (the shared password may be wrong).</summary>
    public int WrongPasswordWarning { get; set; } = 10;

    /// <summary>Marks a player may send in a burst; the bucket refills at <see cref="MarksPerSecond"/>.</summary>
    public int MarkBurst { get; set; } = 10;

    public int MarksPerSecond { get; set; } = 5;

    /// <summary>TV pairings one IP address may start per hour (ADR 0010).</summary>
    public int PairingsPerHour { get; set; } = 30;

    /// <summary>Pairing codes a host may try per minute, so a host token can't be used to guess codes.</summary>
    public int ApprovalsPerMinute { get; set; } = 10;
}

internal static class RateLimits
{
    public const string Create = "create";
    public const string Player = "player";
    public const string Pairing = "pairing";
    public const string Approve = "approve";

    public static IServiceCollection AddSyncBingoRateLimits(this IServiceCollection services, RateLimitOptions limits)
    {
        services.AddSingleton(limits);
        services.AddSingleton<WrongPasswords>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => TooManyAsync(
                context.HttpContext, context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry : null);
            options.AddPolicy(Create, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.CreatesPerHour, Window = TimeSpan.FromHours(1) }));
            options.AddPolicy(Pairing, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.PairingsPerHour, Window = TimeSpan.FromHours(1) }));
            options.AddPolicy(Approve, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Request.Headers.Authorization.ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.ApprovalsPerMinute, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy(Player, http => RateLimitPartition.GetTokenBucketLimiter(
                http.Request.Headers.Authorization.ToString(),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limits.MarkBurst,
                    TokensPerPeriod = limits.MarksPerSecond,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                }));
        });
        return services;
    }

    /// <summary>The caller's address. Behind a reverse proxy this needs forwarded headers configured (roadmap Phase 1b).</summary>
    public static string ClientAddress(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static async ValueTask TooManyAsync(HttpContext http, TimeSpan? retryAfter)
    {
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (retryAfter is { } wait)
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(
                detail: "Too many attempts. Please wait a moment and try again.",
                statusCode: StatusCodes.Status429TooManyRequests,
                extensions: new Dictionary<string, object?> { ["code"] = "TooManyRequests" })
            .ExecuteAsync(http)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Counts wrong game passwords per game and address in a short window; using up the window's
    /// allowance locks joining from there for the (longer) lockout. Kept in memory, per instance.
    /// </summary>
    public sealed class WrongPasswords(RateLimitOptions limits, TimeProvider time) : IDisposable
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lockedUntil = new();

        private readonly PartitionedRateLimiter<string> _failures = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.WrongPasswords,
                Window = limits.WrongPasswordWindow,
            }));

        public static string Key(string code, HttpContext http) =>
            $"{(Domain.Sessions.GameCode.TryParse(code, out var parsed) ? parsed.Value : code)}|{ClientAddress(http)}";

        /// <summary>How long joining from here stays locked, or <c>null</c> when it is open.</summary>
        public TimeSpan? LockedFor(string key)
        {
            if (!_lockedUntil.TryGetValue(key, out var until))
            {
                return null;
            }

            var left = until - time.GetUtcNow();
            if (left > TimeSpan.Zero)
            {
                return left;
            }

            _lockedUntil.TryRemove(key, out _);
            return null;
        }

        /// <summary>
        /// Counts a wrong password. Returns an alert for the host when this one reaches the warning level,
        /// or locks joining from here; otherwise <c>null</c>, so the host is told once, not on every typo.
        /// </summary>
        public WrongPasswordsAlert? Record(string key)
        {
            using var lease = _failures.AttemptAcquire(key);
            var wrong = limits.WrongPasswords - (int)(_failures.GetStatistics(key)?.CurrentAvailablePermits ?? 0);
            if (!lease.IsAcquired || wrong >= limits.WrongPasswords)
            {
                var newlyLocked = !_lockedUntil.ContainsKey(key);
                _lockedUntil[key] = time.GetUtcNow() + limits.WrongPasswordLockout;
                return newlyLocked ? new WrongPasswordsAlert(limits.WrongPasswords, limits.WrongPasswordLockout) : null;
            }

            return wrong == limits.WrongPasswordWarning ? new WrongPasswordsAlert(wrong, null) : null;
        }

        public void Dispose() => _failures.Dispose();
    }
}
