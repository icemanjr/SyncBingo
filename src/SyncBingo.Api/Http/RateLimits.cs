using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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
    /// Wrong game passwords per game and IP address within <see cref="WrongPasswordWindow"/>, after which
    /// joining that game from that address waits for the window to end. Only <i>wrong</i> passwords count:
    /// a class of 30 behind one school IP must be able to join at once.
    /// </summary>
    public int WrongPasswords { get; set; } = 20;

    public TimeSpan WrongPasswordWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Marks a player may send in a burst; the bucket refills at <see cref="MarksPerSecond"/>.</summary>
    public int MarkBurst { get; set; } = 10;

    public int MarksPerSecond { get; set; } = 5;
}

internal static class RateLimits
{
    public const string Create = "create";
    public const string Player = "player";

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

    /// <summary>Counts wrong game passwords per game and address; while the count is used up, joins from there wait.</summary>
    public sealed class WrongPasswords(RateLimitOptions limits) : IDisposable
    {
        private readonly PartitionedRateLimiter<string> _failures = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.WrongPasswords,
                Window = limits.WrongPasswordWindow,
            }));

        public static string Key(string code, HttpContext http) =>
            $"{(Domain.Sessions.GameCode.TryParse(code, out var parsed) ? parsed.Value : code)}|{ClientAddress(http)}";

        public bool IsLockedOut(string key) => _failures.GetStatistics(key)?.CurrentAvailablePermits == 0;

        public void Record(string key)
        {
            using var lease = _failures.AttemptAcquire(key);
        }

        public void Dispose() => _failures.Dispose();
    }
}
