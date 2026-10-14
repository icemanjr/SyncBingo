using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncBingo.Api.IntegrationTests.Redis;
using SyncBingo.Application.Ports;
using SyncBingo.Infrastructure.Security;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>The real API on the test Redis, with a clock tests can move forward and a cheap password hash.</summary>
public sealed class ApiFactory(RedisFixture redis) : WebApplicationFactory<Program>
{
    public ShiftedTime Time { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", redis.ConnectionString);
        builder.UseSetting("Redis:LockWait", "00:00:00.3");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 1));
        });
    }

    public T Service<T>()
        where T : notnull => Services.GetRequiredService<T>();
}

/// <summary>Real time plus an offset, so locks still time out while tests skip ahead.</summary>
public sealed class ShiftedTime : TimeProvider
{
    public TimeSpan Shift { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Shift;
}

/// <summary>Small helpers for calling the API as a host or a player.</summary>
public static class ApiClientExtensions
{
    public static async Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
