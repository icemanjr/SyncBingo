using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyncBingo.Api.Http;
using SyncBingo.Api.IntegrationTests.Redis;
using SyncBingo.Application.Calling;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>The command endpoints over HTTP on real Redis (docs/05): tokens, roles, errors and a played round.</summary>
public sealed class ApiTests : IAsyncDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(RedisFixture redis)
    {
        _factory = new ApiFactory(redis);
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Readiness_checks_redis()
    {
        using var ready = await _client.GetAsync(new Uri("/readyz", UriKind.Relative), Ct);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creating_a_game_returns_its_code_join_link_and_host_token()
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await Json(response);
        var code = body.GetProperty("code").GetString()!;
        code.ShouldMatch("^[a-z2-9]{4}-[a-z2-9]{4}-[a-z2-9]{4}-[a-z2-9]{4}$");
        body.GetProperty("joinUrl").GetString().ShouldBe($"http://localhost:8080/join/{code.Replace("-", "", StringComparison.Ordinal)}");
        body.GetProperty("hostToken").GetString()!.Length.ShouldBe(43);
        response.Headers.Location!.ToString().ShouldBe($"/api/sessions/{code.Replace("-", "", StringComparison.Ordinal)}");
    }

    [Fact]
    public async Task A_rule_violation_is_a_problem_with_a_code_players_can_read()
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "abc" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await Json(response);
        problem.GetProperty("code").GetString().ShouldBe("Invalid");
        problem.GetProperty("detail").GetString().ShouldBe("The game password must be 4–64 characters.");
    }

    [Fact]
    public async Task Malformed_json_is_a_bad_request()
    {
        using var response = await _client.SendAsync(
            HttpMethod.Post, "/api/sessions", body: new { password = "secret", settings = new { unmarking = "Sometimes" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Joining_answers_each_way_it_can_go()
    {
        var (code, _) = await NewGameAsync();

        (await Join(code, "secret", "Ava")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Join(code, "wrong", "Ben")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Join(code, "secret", "AVA")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Join("9ve4-shp3-k7mw-a2qx", "secret", "Ben")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Join("nonsense", "secret", "Ben")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var info = await _client.GetAsync(new Uri($"/api/sessions/{code}/join-info", UriKind.Relative), Ct);
        var body = await Json(info);
        body.GetProperty("acceptingJoins").GetBoolean().ShouldBeTrue();
        body.GetProperty("round").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Commands_need_a_token_for_that_game()
    {
        var (code, hostToken) = await NewGameAsync();
        var (otherCode, otherHostToken) = await NewGameAsync();

        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/end")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/end", "made-up")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/end", otherHostToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a host token works only for its own game");
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{otherCode}/end", hostToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Players_cannot_run_host_commands_and_the_host_cannot_play()
    {
        var (code, hostToken) = await NewGameAsync();
        var (_, playerToken) = await JoinAsync(code, "Ava");

        foreach (var (method, path) in new[] { (HttpMethod.Post, "balls"), (HttpMethod.Post, "end"), (HttpMethod.Put, "settings") })
        {
            using var response = await _client.SendAsync(method, $"/api/sessions/{code}/{path}", playerToken, new { });
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        using var hostMarks = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/marks", hostToken, new { cardId = 1, cell = 0, marked = true });
        hostMarks.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Json(hostMarks)).GetProperty("detail").GetString().ShouldBe("Only players can do that.");
    }

    [Fact]
    public async Task A_command_in_the_wrong_state_is_a_conflict_that_says_why()
    {
        var (code, hostToken) = await NewGameAsync();

        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/balls", hostToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await Json(response);
        problem.GetProperty("code").GetString().ShouldBe("WrongState");
        problem.GetProperty("detail").GetString().ShouldBe("Cannot grab a ball while the session is between rounds.");
    }

    [Fact]
    public async Task A_round_played_over_http()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, avaToken) = await JoinAsync(code, "Ava");

        using var countdown = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false });
        countdown.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(countdown)).GetProperty("round").GetInt32().ShouldBe(1);
        await CountdownEndsAsync(code);

        using var ball = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/balls", hostToken);
        ball.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(ball)).GetProperty("sequence").GetInt32().ShouldBe(1);

        using var mark = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/marks", avaToken, new { cardId = 1, cell = 0, marked = true });
        mark.StatusCode.ShouldBe(HttpStatusCode.OK);
        ((await Json(mark)).GetProperty("marks").GetUInt32() & 1u).ShouldBe(1u);

        using var claim = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/claims", avaToken, new { cardId = 1 });
        claim.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/wins", hostToken, new { participantId = avaId, claimId = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/win-condition", hostToken, new { condition = "blackout" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/cards/clear", hostToken, new { redrawCards = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var session = (await _factory.Service<Application.Ports.ISessionStore>().GetAsync(GameCode.Parse(code), Ct))!;
        session.Wins.ShouldHaveSingleItem().ParticipantId.ShouldBe(avaId);
        session.State.ShouldBe(SessionState.BetweenRounds);
    }

    [Fact]
    public async Task Settings_change_only_the_fields_sent()
    {
        var (code, hostToken) = await NewGameAsync();

        using var response = await _client.SendAsync(
            HttpMethod.Put, $"/api/sessions/{code}/settings", hostToken, new { freeMarking = false, autoCallIntervalSeconds = 6 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = (await Json(response)).GetProperty("settings");
        settings.GetProperty("freeMarking").GetBoolean().ShouldBeFalse();
        settings.GetProperty("autoCallIntervalSeconds").GetInt32().ShouldBe(6);
        settings.GetProperty("showCallBoard").GetBoolean().ShouldBeTrue("unsent fields keep their values");
        settings.GetProperty("unmarking").GetString().ShouldBe("Any", "enums are sent as names");
    }

    [Fact]
    public async Task A_removed_players_token_stops_working()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, avaToken) = await JoinAsync(code, "Ava");

        (await _client.SendAsync(HttpMethod.Delete, $"/api/sessions/{code}/participants/{avaId}", hostToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/me/auto-fill", avaToken, new { on = false }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_game_stuck_behind_its_lock_answers_service_unavailable()
    {
        var (code, hostToken) = await NewGameAsync();
        await using var held = await _factory.Service<Application.Ports.ISessionStore>().LockAsync(GameCode.Parse(code), Ct);

        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/end", hostToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await Json(response)).GetProperty("detail").GetString().ShouldBe("The game is busy. Please try again.");
    }

    [Theory]
    [MemberData(nameof(ErrorCodes))]
    public void Every_domain_error_has_a_client_error_status(DomainErrorCode code)
    {
        Problems.StatusOf(code).ShouldBeInRange(400, 499);
    }

    public static TheoryData<DomainErrorCode> ErrorCodes => [.. Enum.GetValues<DomainErrorCode>()];

    private async Task<(string Code, string HostToken)> NewGameAsync()
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });
        var body = await Json(response);
        return (body.GetProperty("code").GetString()!, body.GetProperty("hostToken").GetString()!);
    }

    private async Task<(string Id, string Token)> JoinAsync(string code, string name)
    {
        using var response = await Join(code, "secret", name);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        return (body.GetProperty("participantId").GetString()!, body.GetProperty("token").GetString()!);
    }

    private Task<HttpResponseMessage> Join(string code, string password, string name) =>
        _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password, name });

    /// <summary>Skips the clock past the countdown and runs the tick the scheduler would run.</summary>
    private async Task CountdownEndsAsync(string code)
    {
        _factory.Time.Shift += Session.CountdownLength;
        (await _factory.Service<DrawHandler>().TickAsync(GameCode.Parse(code), Ct)).IsSuccess.ShouldBeTrue();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
}
