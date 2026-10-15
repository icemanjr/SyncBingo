using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncBingo.Api.IntegrationTests.Redis;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>Rate limits behind a reverse proxy see each client's own address, and only a trusted proxy can say what it is.</summary>
public sealed class ProxyHeadersTests(RedisFixture redis)
{
    private const string Proxy = "172.18.0.5"; // a container network address, like Caddy's
    private const string Stranger = "198.51.100.7"; // a public address, talking to the API directly

    [Fact]
    public async Task Behind_the_proxy_each_client_has_its_own_allowance()
    {
        await using var factory = Api(enabled: true, from: Proxy);
        using var client = factory.CreateClient();

        (await Create(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.1")).ShouldBe(HttpStatusCode.TooManyRequests);
        (await Create(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Created, "another client behind the same proxy");
    }

    [Fact]
    public async Task Only_the_address_the_proxy_added_counts()
    {
        await using var factory = Api(enabled: true, from: Proxy);
        using var client = factory.CreateClient();

        // The client sends a made-up address first; the proxy appends the real one.
        for (var i = 0; i < 2; i++)
        {
            (await Create(client, $"10.9.9.{i}, 203.0.113.1")).ShouldBe(HttpStatusCode.Created);
        }

        (await Create(client, "10.9.9.9, 203.0.113.1")).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_client_outside_the_private_network_cannot_pick_its_address()
    {
        await using var factory = Api(enabled: true, from: Stranger);
        using var client = factory.CreateClient();

        (await Create(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.3")).ShouldBe(HttpStatusCode.TooManyRequests, "the header is ignored: it's still one caller");
    }

    [Fact]
    public async Task Without_the_setting_the_header_is_ignored()
    {
        await using var factory = Api(enabled: false, from: Proxy);
        using var client = factory.CreateClient();

        (await Create(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Created);
        (await Create(client, "203.0.113.3")).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private WebApplicationFactoryWithAddress Api(bool enabled, string from) => new(redis, enabled, IPAddress.Parse(from));

    private static async Task<HttpStatusCode> Create(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/sessions", UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { password = "secret" }),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>The API with a create limit of 2, seeing every request arrive from <paramref name="from"/>.</summary>
    private sealed class WebApplicationFactoryWithAddress(RedisFixture redis, bool enabled, IPAddress from) : IAsyncDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory =
            new ApiFactory(redis, new Dictionary<string, string>
            {
                ["RateLimits:CreatesPerHour"] = "2",
                ["ForwardedHeaders:Enabled"] = enabled ? "true" : "false",
            }).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new ConnectionFrom(from))));

        public HttpClient CreateClient() => factory.CreateClient();

        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }

    /// <summary>The test server sets no remote address, so put one on each request before the app runs.</summary>
    private sealed class ConnectionFrom(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
