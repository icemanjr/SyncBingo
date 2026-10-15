using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyncBingo.Api.IntegrationTests.Redis;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>The snapshot and host queries over HTTP: each caller gets exactly their view.</summary>
public sealed class SnapshotApiTests : IAsyncDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SnapshotApiTests(RedisFixture redis)
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
    public async Task Create_and_join_answer_with_the_callers_snapshot()
    {
        using var created = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });
        var host = await Json(created);
        var code = host.GetProperty("code").GetString()!;
        using var joined = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "secret", name = "Ava" });
        var player = await Json(joined);

        host.GetProperty("snapshot").GetProperty("host").GetProperty("settings").GetProperty("autoCallIntervalSeconds").GetInt32().ShouldBe(10);
        var you = player.GetProperty("snapshot").GetProperty("you");
        you.GetProperty("name").GetString().ShouldBe("Ava");
        you.GetProperty("cards")[0].GetProperty("cells").GetArrayLength().ShouldBe(25);
        player.GetProperty("snapshot").GetProperty("host").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_players_snapshot_carries_nothing_about_other_players()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, _) = await JoinAsync(code, "Avalanche");
        var (_, benToken) = await JoinAsync(code, "Ben");
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false })).Dispose();

        using var response = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", benToken);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        raw.ShouldNotContain(avaId);
        raw.ShouldNotContain("Avalanche");
    }

    [Fact]
    public async Task The_snapshot_draws_patterns_from_data()
    {
        var (code, hostToken) = await NewGameAsync();

        using var response = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", hostToken);
        var game = (await Json(response)).GetProperty("game");

        game.GetProperty("patterns")[0].GetProperty("masks").GetArrayLength().ShouldBe(5, "rows");
        game.GetProperty("presets")[0].GetProperty("name").GetString().ShouldBe("Normal Bingo");
        game.GetProperty("card").GetProperty("freeCell").GetInt32().ShouldBe(12);
        game.GetProperty("state").GetString().ShouldBe("BetweenRounds");
    }

    [Fact]
    public async Task Only_the_host_inspects_cards_and_reads_the_leaderboard()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, avaToken) = await JoinAsync(code, "Ava");

        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/participants/{avaId}/card", hostToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/participants/{avaId}/card", avaToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/leaderboard", hostToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/leaderboard", avaToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(string Code, string HostToken)> NewGameAsync()
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });
        var body = await Json(response);
        return (body.GetProperty("code").GetString()!, body.GetProperty("hostToken").GetString()!);
    }

    private async Task<(string Id, string Token)> JoinAsync(string code, string name)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "secret", name });
        var body = await Json(response);
        return (body.GetProperty("participantId").GetString()!, body.GetProperty("token").GetString()!);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
}
