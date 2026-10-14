using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using SyncBingo.Api.Auth;
using SyncBingo.Api.Endpoints;
using SyncBingo.Api.Http;
using SyncBingo.Api.Realtime;
using SyncBingo.Application.Calling;
using SyncBingo.Application.Displays;
using SyncBingo.Application.Pairing;
using SyncBingo.Application.Players;
using SyncBingo.Application.Ports;
using SyncBingo.Application.Queries;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain;
using SyncBingo.Infrastructure;
using SyncBingo.Infrastructure.Pairing;
using SyncBingo.Infrastructure.Redis;
using SyncBingo.Infrastructure.Scheduling;
using SyncBingo.Infrastructure.Security;

namespace SyncBingo.Api;

/// <summary>The composition root: every port gets its production adapter here.</summary>
internal static class ServiceRegistration
{
    public static IServiceCollection AddSyncBingo(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:ConnectionString"] ?? "localhost:6379";

        // Don't fail start-up if Redis is late: the connection retries, and /readyz reports it.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton(configuration.GetSection("Redis").Get<RedisStoreOptions>() ?? new RedisStoreOptions());
        services.AddSingleton(configuration.GetSection("Scheduler").Get<CallSchedulerOptions>() ?? new CallSchedulerOptions());
        services.Configure<WebOptions>(configuration.GetSection("Web"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRandomSource, CryptoRandomSource>();
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddSingleton<ITokenService, RandomTokenService>();
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddSingleton<RedisCallScheduler>();
        services.AddSingleton<ICallScheduler>(sp => sp.GetRequiredService<RedisCallScheduler>());
        services.AddSingleton<IEventPublisher, SignalREventPublisher>();
        services.AddSignalR()
            .AddJsonProtocol(json => json.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .AddStackExchangeRedis(redisConnection, backplane =>
            {
                // Its own connection and channel prefix; one instance or many, events reach every connection (docs/02).
                backplane.Configuration.ChannelPrefix = RedisChannel.Literal("bingo:signalr");
                backplane.Configuration.AbortOnConnectFail = false;
            });

        services.AddSingleton<SessionCommandRunner>();
        services.AddSingleton<CreateSessionHandler>();
        services.AddSingleton<JoinSessionHandler>();
        services.AddSingleton<JoinInfoQuery>();
        services.AddSingleton<GameQueries>();
        services.AddSingleton<HostCommandHandler>();
        services.AddSingleton<RoundHandler>();
        services.AddSingleton<DrawHandler>();
        services.AddSingleton<PlayerCommandHandler>();
        services.AddSingleton<PlayerManagementHandler>();
        services.AddSingleton<DisplayHandler>();
        services.AddSingleton<IPairingStore, RedisPairingStore>();
        services.AddSingleton<IPairingNotifier, SignalRPairingNotifier>();
        services.AddSingleton<PairingHandler>();
        services.AddHostedService<CallSchedulerService>();

        services.AddAuthentication(SessionTokenAuthentication.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SessionTokenAuthentication>(
                SessionTokenAuthentication.SchemeName, null);
        services.AddAuthorization();

        services.AddProblemDetails();
        services.AddExceptionHandler<Problems.ExceptionHandler>();
        services.Configure<JsonOptions>(json => json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

        services.AddSyncBingoRateLimits(configuration.GetSection("RateLimits").Get<RateLimitOptions>() ?? new RateLimitOptions());
        services.AddOpenApi();

        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        return services;
    }

    /// <summary>Ready when Redis answers a ping.</summary>
    private sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await redis.GetDatabase().PingAsync().ConfigureAwait(false);
                return HealthCheckResult.Healthy();
            }
            catch (RedisException exception)
            {
                return HealthCheckResult.Unhealthy("Redis is unreachable.", exception);
            }
        }
    }
}
