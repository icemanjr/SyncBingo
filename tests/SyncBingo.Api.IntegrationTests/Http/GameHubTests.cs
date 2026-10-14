using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using SyncBingo.Api.IntegrationTests.Redis;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>
/// The SignalR hub end to end (docs/05 §4, ADR 0002): each audience gets its own events with its
/// own unbroken sequence, private messages reach one player, and tokens decide who may listen.
/// </summary>
public sealed class GameHubTests : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly List<Listener> _listeners = [];

    public GameHubTests(RedisFixture redis)
    {
        // Low wrong-password limits so the host alerts can be reached; no other test here types a wrong password.
        _factory = new ApiFactory(redis, new Dictionary<string, string>
        {
            ["RateLimits:WrongPasswordWarning"] = "3",
            ["RateLimits:WrongPasswords"] = "6",
        });
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (var listener in _listeners)
        {
            await listener.Connection.DisposeAsync();
        }

        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Players_get_the_balls_but_not_the_hosts_claims_and_their_numbers_never_skip()
    {
        var (code, hostToken) = await NewGameAsync();
        var (_, avaToken) = await JoinAsync(code, "Ava");
        var host = await ListenAsync(code, hostToken);
        var ava = await ListenAsync(code, avaToken);
        await StartManualRoundAsync(code, hostToken);
        await PostAsync(code, "balls", hostToken);
        await PostAsync(code, "claims", avaToken, new { cardId = 1 });
        await PostAsync(code, "balls", hostToken);

        await host.WaitForAsync("BallCalled", 2);
        await ava.WaitForAsync("BallCalled", 2);

        host.Types.ShouldContain("WinClaimed");
        ava.Types.ShouldNotContain("WinClaimed");
        ava.Seqs.ShouldBe(Enumerable.Range((int)ava.Seqs[0], ava.Seqs.Count).Select(n => (long)n), "no gaps in the player's stream");
        host.Seqs.ShouldBe(Enumerable.Range((int)host.Seqs[0], host.Seqs.Count).Select(n => (long)n), "no gaps in the host's stream");
    }

    [Fact]
    public async Task The_snapshot_tells_a_client_where_its_stream_continues()
    {
        var (code, hostToken) = await NewGameAsync();
        var (_, avaToken) = await JoinAsync(code, "Ava");
        var ava = await ListenAsync(code, avaToken);
        await StartManualRoundAsync(code, hostToken);
        await ava.WaitForAsync("RoundStarted", 1);

        using var snapshot = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", avaToken);
        var seq = (await snapshot.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("game").GetProperty("seq").GetInt64();
        await PostAsync(code, "balls", hostToken);

        (await ava.WaitForAsync("BallCalled", 1)).GetProperty("seq").GetInt64().ShouldBe(seq + 1);
    }

    [Fact]
    public async Task Everyone_hears_who_had_bingo_by_name_and_without_ids()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, _) = await JoinAsync(code, "Ava");
        var (_, benToken) = await JoinAsync(code, "Ben");
        var ben = await ListenAsync(code, benToken);
        await StartManualRoundAsync(code, hostToken);

        await PostAsync(code, "wins", hostToken, new { participantId = avaId });

        var win = (await ben.WaitForAsync("WinAwarded", 1)).GetProperty("data");
        win.GetProperty("name").GetString().ShouldBe("Ava");
        win.GetRawText().ShouldNotContain(avaId);
    }

    [Fact]
    public async Task Settings_reach_players_without_the_hosts_settings()
    {
        var (code, hostToken) = await NewGameAsync();
        var (_, avaToken) = await JoinAsync(code, "Ava");
        var ava = await ListenAsync(code, avaToken);

        using var response = await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/rules", hostToken, new { settings = new { freeMarking = false } });

        var settings = (await ava.WaitForAsync("SettingsChanged", 1)).GetProperty("data");
        settings.GetProperty("freeMarking").GetBoolean().ShouldBeFalse();
        settings.TryGetProperty("winDetection", out _).ShouldBeFalse("how the host is alerted is the host's business");
    }

    [Fact]
    public async Task Auto_fill_reaches_only_that_player()
    {
        var (code, hostToken) = await NewGameAsync(new { autoFillAvailable = true });
        var (_, avaToken) = await JoinAsync(code, "Ava");
        var (_, benToken) = await JoinAsync(code, "Ben");
        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/me/auto-fill", avaToken, new { on = true })).Dispose();
        var ava = await ListenAsync(code, avaToken);
        var ben = await ListenAsync(code, benToken);
        await StartManualRoundAsync(code, hostToken);

        // Auto-fill marks the previous ball, so draw until one of Ava's numbers is filled.
        for (var i = 0; i < 40 && !ava.Notices.Any(); i++)
        {
            await PostAsync(code, "balls", hostToken);
        }

        await ava.WaitForNoticeAsync("AutoFillApplied");
        await ben.WaitForAsync("BallCalled", 1);
        ben.Notices.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_host_is_warned_when_many_passwords_are_wrong_and_told_of_a_lockout()
    {
        var (code, hostToken) = await NewGameAsync();
        var host = await ListenAsync(code, hostToken);

        for (var i = 0; i < 6; i++)
        {
            (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "wrong", name = $"Guess{i}" })).Dispose();
        }

        await host.WaitForNoticeAsync("WrongPasswordsAlert");
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (host.Notices.Count(n => n.GetProperty("type").GetString() == "WrongPasswordsAlert") < 2 && DateTime.UtcNow < until)
        {
            await Task.Delay(20, Ct);
        }

        var alerts = host.Notices.Where(n => n.GetProperty("type").GetString() == "WrongPasswordsAlert").Select(n => n.GetProperty("data")).ToList();
        alerts.Count.ShouldBe(2, "once at the warning level, once at the lockout; not on every typo");
        alerts[0].GetProperty("wrongInLastMinute").GetInt32().ShouldBe(3);
        alerts[0].GetProperty("lockedFor").ValueKind.ShouldBe(JsonValueKind.Null);
        alerts[1].GetProperty("lockedFor").GetString().ShouldBe("00:10:00");
    }

    [Fact]
    public async Task A_display_link_shows_public_state_and_the_join_note_and_can_do_nothing()
    {
        var (code, hostToken) = await NewGameAsync();
        var (_, avaToken) = await JoinAsync(code, "Ava");
        var (displayId, displayToken) = await CreateDisplayLinkAsync(code, hostToken);
        var display = await ListenAsync(code, displayToken);
        var ava = await ListenAsync(code, avaToken);

        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/join-note", hostToken, new { note = "Password: secret" })).Dispose();
        await StartManualRoundAsync(code, hostToken);
        await PostAsync(code, "balls", hostToken);
        await PostAsync(code, "claims", avaToken, new { cardId = 1 });

        (await display.WaitForAsync("JoinNoteChanged", 1)).GetProperty("data").GetProperty("note").GetString().ShouldBe("Password: secret");
        await display.WaitForAsync("BallCalled", 1);
        display.Types.ShouldNotContain("WinClaimed");
        await ava.WaitForAsync("BallCalled", 1);
        ava.Types.ShouldNotContain("JoinNoteChanged", "players have already joined");

        using var snapshot = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", displayToken);
        (await snapshot.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("game").GetProperty("joinNote").GetString().ShouldBe("Password: secret");
        foreach (var (method, path) in new[] { (HttpMethod.Post, "balls"), (HttpMethod.Post, "marks"), (HttpMethod.Post, "display-links"), (HttpMethod.Put, "join-note") })
        {
            using var refused = await _client.SendAsync(method, $"/api/sessions/{code}/{path}", displayToken, new { cardId = 1, cell = 0, marked = true, note = "x" });
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        displayId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_disconnected_display_is_told_and_its_token_stops_working()
    {
        var (code, hostToken) = await NewGameAsync();
        var (displayId, displayToken) = await CreateDisplayLinkAsync(code, hostToken);
        var display = await ListenAsync(code, displayToken);

        using var list = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/displays", hostToken);
        (await list.Content.ReadFromJsonAsync<JsonElement>(Ct))[0].GetProperty("kind").GetString().ShouldBe("Link");
        (await _client.SendAsync(HttpMethod.Delete, $"/api/sessions/{code}/displays/{displayId}", hostToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await display.WaitForNoticeAsync("DisplayDisconnected");
        (await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", displayToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_the_host_creates_display_links()
    {
        var (code, _) = await NewGameAsync();
        var (_, avaToken) = await JoinAsync(code, "Ava");

        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/display-links", avaToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_a_token_for_that_game_can_listen()
    {
        var (code, _) = await NewGameAsync();
        var (_, otherHostToken) = await NewGameAsync();

        await Should.ThrowAsync<Exception>(() => ListenAsync(code, "made-up"));
        await Should.ThrowAsync<Exception>(() => ListenAsync(code, otherHostToken));
    }

    [Fact]
    public async Task Browsers_can_sign_in_with_the_access_token_query()
    {
        var (code, hostToken) = await NewGameAsync();

        var host = await ListenAsync(code, hostToken, inQuery: true);
        await PostAsync(code, "leaderboard/show", hostToken, new { visible = true });

        await host.WaitForAsync("LeaderboardVisibilityChanged", 1);
    }

    private async Task<Listener> ListenAsync(string code, string token, bool inQuery = false)
    {
        var server = _factory.Server;
        var url = new Uri(server.BaseAddress, $"/hubs/game?code={code}" + (inQuery ? $"&access_token={token}" : ""));
        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var client = server.CreateWebSocketClient();
                    if (!inQuery)
                    {
                        client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
                    }

                    return await client.ConnectAsync(context.Uri, ct);
                };
            })
            .Build();
        var listener = new Listener(connection);
        await connection.StartAsync(Ct);
        _listeners.Add(listener);
        return listener;
    }

    /// <summary>Creates a display link and reads its token from the URL fragment, as the display page does.</summary>
    private async Task<(string DisplayId, string Token)> CreateDisplayLinkAsync(string code, string hostToken)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/display-links", hostToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var url = new Uri(body.GetProperty("displayUrl").GetString()!);
        url.AbsolutePath.ShouldBe($"/display/{code.Replace("-", "", StringComparison.Ordinal)}");
        url.Fragment.ShouldStartWith("#t=");
        url.Query.ShouldBeEmpty("the token never goes in the query, where servers would log it");
        return (body.GetProperty("displayId").GetString()!, url.Fragment["#t=".Length..]);
    }

    private async Task<(string Code, string HostToken)> NewGameAsync(object? settings = null)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret", settings });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (body.GetProperty("code").GetString()!, body.GetProperty("hostToken").GetString()!);
    }

    private async Task<(string Id, string Token)> JoinAsync(string code, string name)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "secret", name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (body.GetProperty("participantId").GetString()!, body.GetProperty("token").GetString()!);
    }

    private async Task PostAsync(string code, string path, string token, object? body = null)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/{path}", token, body);
        ((int)response.StatusCode).ShouldBeInRange(200, 299, path);
    }

    private async Task StartManualRoundAsync(string code, string hostToken)
    {
        await PostAsync(code, "rounds/countdown", hostToken, new { startInAuto = false });
        _factory.Time.Shift += Domain.Sessions.Session.CountdownLength;
        (await _factory.Service<Application.Calling.DrawHandler>().TickAsync(Domain.Sessions.GameCode.Parse(code), Ct)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>One connected client, recording what it hears.</summary>
    private sealed class Listener
    {
        private readonly ConcurrentQueue<JsonElement> _events = new();
        private readonly ConcurrentQueue<JsonElement> _notices = new();

        public Listener(HubConnection connection)
        {
            Connection = connection;
            connection.On<JsonElement>("event", e => _events.Enqueue(e));
            connection.On<JsonElement>("notice", n => _notices.Enqueue(n));
        }

        public HubConnection Connection { get; }

        public IReadOnlyList<string> Types => [.. _events.Select(e => e.GetProperty("type").GetString()!)];

        public IReadOnlyList<long> Seqs => [.. _events.Select(e => e.GetProperty("seq").GetInt64())];

        public IEnumerable<JsonElement> Notices => _notices;

        /// <summary>Waits until <paramref name="count"/> events of <paramref name="type"/> arrived; returns the last.</summary>
        public async Task<JsonElement> WaitForAsync(string type, int count)
        {
            var until = DateTime.UtcNow + Patience;
            while (DateTime.UtcNow < until)
            {
                var matching = _events.Where(e => e.GetProperty("type").GetString() == type).ToList();
                if (matching.Count >= count)
                {
                    return matching[count - 1];
                }

                await Task.Delay(20, Ct);
            }

            throw new TimeoutException($"Expected {count} × {type}; heard {string.Join(", ", Types)}.");
        }

        public async Task WaitForNoticeAsync(string type)
        {
            var until = DateTime.UtcNow + Patience;
            while (DateTime.UtcNow < until && !_notices.Any(n => n.GetProperty("type").GetString() == type))
            {
                await Task.Delay(20, Ct);
            }

            _notices.ShouldContain(n => n.GetProperty("type").GetString() == type);
        }
    }
}
