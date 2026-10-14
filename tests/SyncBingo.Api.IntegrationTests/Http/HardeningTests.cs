using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyncBingo.Api.IntegrationTests.Redis;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>Rate limits, idempotent awards and the OpenAPI document (docs/05 §5).</summary>
public sealed class HardeningTests(RedisFixture redis)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Wrong_passwords_are_limited_per_game_and_address()
    {
        await using var factory = new ApiFactory(redis, new Dictionary<string, string> { ["RateLimits:WrongPasswords"] = "3" });
        using var client = factory.CreateClient();
        var (code, _) = await NewGameAsync(client);
        var (otherCode, _) = await NewGameAsync(client);

        for (var i = 0; i < 3; i++)
        {
            (await Join(client, code, "wrong", $"Guess{i}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var locked = await Join(client, code, "secret", "Ava");
        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "the right password waits too, or guessing would go on");
        (await locked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("TooManyRequests");
        (await Join(client, otherCode, "secret", "Ava")).StatusCode.ShouldBe(HttpStatusCode.OK, "other games are unaffected");
    }

    [Fact]
    public async Task A_whole_class_can_join_from_one_address()
    {
        await using var factory = new ApiFactory(redis, new Dictionary<string, string> { ["RateLimits:WrongPasswords"] = "3" });
        using var client = factory.CreateClient();
        var (code, _) = await NewGameAsync(client);

        for (var i = 0; i < 30; i++)
        {
            (await Join(client, code, "secret", $"Pupil{i}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Creating_games_is_limited_per_address()
    {
        await using var factory = new ApiFactory(redis, new Dictionary<string, string> { ["RateLimits:CreatesPerHour"] = "2" });
        using var client = factory.CreateClient();
        await NewGameAsync(client);
        await NewGameAsync(client);

        using var third = await client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });

        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Marking_is_limited_per_player()
    {
        await using var factory = new ApiFactory(redis, new Dictionary<string, string>
        {
            ["RateLimits:MarkBurst"] = "3",
            ["RateLimits:MarksPerSecond"] = "1",
        });
        using var client = factory.CreateClient();
        var (code, hostToken) = await NewGameAsync(client);
        var avaToken = await JoinAsync(client, code, "Ava");
        var benToken = await JoinAsync(client, code, "Ben");
        await StartRoundAsync(factory, client, code, hostToken);

        var statuses = new List<HttpStatusCode>();
        for (var cell = 0; cell < 5; cell++)
        {
            using var response = await client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/marks", avaToken, new { cardId = 1, cell, marked = true });
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
        using var ben = await client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/marks", benToken, new { cardId = 1, cell = 0, marked = true });
        ben.StatusCode.ShouldBe(HttpStatusCode.OK, "each player has their own allowance");
    }

    [Fact]
    public async Task An_award_sent_twice_with_one_idempotency_key_counts_once()
    {
        await using var factory = new ApiFactory(redis);
        using var client = factory.CreateClient();
        var (code, hostToken) = await NewGameAsync(client, new { multipleWins = "EveryDeclaredWin" });
        var avaId = await JoinIdAsync(client, code, "Ava");
        await StartRoundAsync(factory, client, code, hostToken);

        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/sessions/{code}/wins", UriKind.Relative))
            {
                Content = JsonContent.Create(new { participantId = avaId }),
            };
            request.Headers.Authorization = new("Bearer", hostToken);
            request.Headers.Add("Idempotency-Key", "double-click");
            using var response = await client.SendAsync(request, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var session = (await factory.Service<Application.Ports.ISessionStore>().GetAsync(GameCode.Parse(code), Ct))!;
        session.Leaderboard().ShouldHaveSingleItem().Wins.ShouldBe(1);
    }

    [Fact]
    public async Task The_api_describes_itself_in_openapi()
    {
        await using var factory = new ApiFactory(redis);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);
        var paths = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var path in new[] { "/api/sessions", "/api/sessions/{code}/join", "/api/sessions/{code}/marks", "/api/sessions/{code}/rules", "/api/sessions/{code}/snapshot" })
        {
            paths.TryGetProperty(path, out _).ShouldBeTrue(path);
        }
    }

    private static async Task<(string Code, string HostToken)> NewGameAsync(HttpClient client, object? settings = null)
    {
        using var response = await client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret", settings });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (body.GetProperty("code").GetString()!, body.GetProperty("hostToken").GetString()!);
    }

    private static Task<HttpResponseMessage> Join(HttpClient client, string code, string password, string name) =>
        client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password, name });

    private static async Task<string> JoinAsync(HttpClient client, string code, string name) =>
        (await JoinBodyAsync(client, code, name)).GetProperty("token").GetString()!;

    private static async Task<string> JoinIdAsync(HttpClient client, string code, string name) =>
        (await JoinBodyAsync(client, code, name)).GetProperty("participantId").GetString()!;

    private static async Task<JsonElement> JoinBodyAsync(HttpClient client, string code, string name)
    {
        using var response = await Join(client, code, "secret", name);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task StartRoundAsync(ApiFactory factory, HttpClient client, string code, string hostToken)
    {
        (await client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false })).Dispose();
        factory.Time.Shift += Session.CountdownLength;
        (await factory.Service<Application.Calling.DrawHandler>().TickAsync(GameCode.Parse(code), Ct)).IsSuccess.ShouldBeTrue();
    }
}
