using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SyncBingo.Api.IntegrationTests.Redis;
using SyncBingo.Application.Calling;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>A host's custom set over HTTP (FR-44, ADR 0009): use it, play it, download it, go back to numbers.</summary>
public sealed class ContentApiTests : IAsyncDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ContentApiTests(RedisFixture redis)
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

    /// <summary>A set file of <paramref name="count"/> sums: "1 + 1" called, "2" marked, and so on.</summary>
    private static string Sums(int count = 30, string name = "Sums") =>
        $$"""{ "format": 1, "name": "{{name}}", "pairs": [{{string.Join(", ", Enumerable.Range(1, count).Select(i => $"[\"{i} + 1\", \"{i + 1}\"]"))}}] }""";

    [Fact]
    public async Task Using_a_set_deals_everyone_a_card_of_its_items()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");

        using var used = await UseAsync(code, hostToken, Sums());
        used.StatusCode.ShouldBe(HttpStatusCode.NoContent, await used.Content.ReadAsStringAsync(Ct));

        var snapshot = await SnapshotAsync(code, avaToken);
        var content = snapshot.GetProperty("game").GetProperty("content");
        content.GetProperty("name").GetString().ShouldBe("Sums");
        content.GetProperty("groupItems").GetBoolean().ShouldBeFalse();
        content.GetProperty("board")[0].GetString().ShouldBe("1 + 1");
        snapshot.GetProperty("game").GetProperty("ballsRemaining").GetInt32().ShouldBe(30);
        var card = snapshot.GetProperty("you").GetProperty("cards")[0];
        var faces = card.GetProperty("faces").EnumerateArray().Select(face => face.GetString()!).ToList();
        faces.Where(face => face.Length > 0).ShouldAllBe(face => int.Parse(face, System.Globalization.CultureInfo.InvariantCulture) >= 2);
        faces[12].ShouldBe(string.Empty, "the free space");
    }

    [Fact]
    public async Task A_grouped_set_sends_its_short_labels_for_the_cards_columns()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");
        var sample = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "samples", "science-vocabulary.json"), Ct);

        (await UseAsync(code, hostToken, sample, groupItems: true)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var content = (await SnapshotAsync(code, avaToken)).GetProperty("game").GetProperty("content");
        content.GetProperty("labels").EnumerateArray().Select(label => label.GetString()).ShouldBe(["B", "I", "N", "G", "O"]);
    }

    [Fact]
    public async Task The_host_can_show_the_square_side_on_every_board()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");
        await UseAsync(code, hostToken, Sums());

        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/board-side", avaToken, new { side = "Square" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/board-side", hostToken, new { side = "Square" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var content = (await SnapshotAsync(code, avaToken)).GetProperty("game").GetProperty("content");
        content.GetProperty("boardSide").GetString().ShouldBe("Square");
        content.GetProperty("board")[0].GetString().ShouldBe("2", "item 1's square, not its ball '1 + 1'");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "SyncBingo.slnx")))
        {
            dir = dir.Parent;
        }

        return dir.FullName;
    }

    [Fact]
    public async Task A_player_never_receives_the_answer_key()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");
        await UseAsync(code, hostToken, Sums());

        var snapshot = await SnapshotAsync(code, avaToken);

        // The card's cells are positions, not items: with them and the board's ball text, a player could
        // look up which clue goes with each of their squares.
        var cells = snapshot.GetProperty("you").GetProperty("cards")[0].GetProperty("cells").EnumerateArray().Select(cell => cell.GetInt32()).ToList();
        cells.ShouldBe([.. Enumerable.Range(1, 25).Select(cell => cell == 13 ? 0 : cell)]);

        // The set's squares come alphabetically, pairing with nothing, and no pairs are sent at all.
        var content = snapshot.GetProperty("game").GetProperty("content");
        var squares = content.GetProperty("squares").EnumerateArray().Select(square => square.GetString()!).ToList();
        squares.ShouldBe(squares.Order(StringComparer.OrdinalIgnoreCase));
        content.TryGetProperty("items", out _).ShouldBeFalse();
        snapshot.GetRawText().ShouldNotContain("\"square\":\"2\",\"ball\"");
    }

    [Fact]
    public async Task The_host_checking_a_claim_sees_the_real_card()
    {
        var (code, hostToken) = await NewGameAsync();
        var (avaId, avaToken) = await JoinWithIdAsync(code, "Ava");
        await UseAsync(code, hostToken, Sums());

        using var inspected = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/participants/{avaId}/card", hostToken);

        var card = (await Json(inspected)).GetProperty("cards")[0];
        var cells = card.GetProperty("cells").EnumerateArray().Select(cell => cell.GetInt32()).ToList();
        var faces = card.GetProperty("faces").EnumerateArray().Select(face => face.GetString()!).ToList();
        for (var cell = 0; cell < 25; cell++)
        {
            faces[cell].ShouldBe(cell == 12 ? string.Empty : $"{cells[cell] + 1}", "item n's square is n + 1");
        }

        var mine = (await SnapshotAsync(code, avaToken)).GetProperty("you").GetProperty("cards")[0].GetProperty("faces");
        mine.GetRawText().ShouldBe(card.GetProperty("faces").GetRawText(), "the same squares the player sees");
    }

    [Fact]
    public async Task A_called_ball_says_what_to_call_and_what_to_mark()
    {
        var (code, hostToken) = await NewGameAsync();
        await UseAsync(code, hostToken, Sums());
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false })).Dispose();
        _factory.Time.Shift += Session.CountdownLength;
        (await _factory.Service<DrawHandler>().TickAsync(GameCode.Parse(code), Ct)).IsSuccess.ShouldBeTrue();

        using var grabbed = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/balls", hostToken);

        var ball = await Json(grabbed);
        var number = ball.GetProperty("number").GetInt32();
        ball.GetProperty("call").GetString().ShouldBe($"{number} + 1");
        ball.GetProperty("square").GetString().ShouldBe($"{number + 1}");
    }

    [Fact]
    public async Task The_host_downloads_the_set_as_a_file_to_keep()
    {
        var (code, hostToken) = await NewGameAsync();
        await UseAsync(code, hostToken, Sums(name: "Week 5: sums"));

        using var download = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/content", hostToken);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        download.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("Week 5- sums.json");
        var file = await download.Content.ReadAsStringAsync(Ct);
        file.ShouldContain("\"name\": \"Week 5: sums\"");
        file.ShouldContain("[\"1 + 1\", \"2\"]");
    }

    [Fact]
    public async Task Back_to_numbers()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");
        await UseAsync(code, hostToken, Sums());

        (await _client.SendAsync(HttpMethod.Delete, $"/api/sessions/{code}/content", hostToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var game = (await SnapshotAsync(code, avaToken)).GetProperty("game");
        (!game.TryGetProperty("content", out var content) || content.ValueKind == JsonValueKind.Null).ShouldBeTrue("no set in use");
        game.GetProperty("ballsRemaining").GetInt32().ShouldBe(75);
        using var download = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/content", hostToken);
        download.StatusCode.ShouldBe(HttpStatusCode.Conflict, "no set to download, which is not 'the game is over'");
    }

    [Fact]
    public async Task Only_the_host_changes_the_set()
    {
        var (code, _) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");

        (await UseAsync(code, avaToken, Sums())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/content/sharing", avaToken, new { playersMayDownload = true }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Players_download_the_set_only_when_the_host_shares_it_and_only_in_the_lobby()
    {
        var (code, hostToken) = await NewGameAsync();
        var avaToken = await JoinAsync(code, "Ava");
        await UseAsync(code, hostToken, Sums(name: "Shared sums"));
        var download = () => _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/content", avaToken);

        using (var privateSet = await download())
        {
            privateSet.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await Json(privateSet)).GetProperty("detail").GetString().ShouldBe("The host hasn't shared this set.");
        }

        (await _client.SendAsync(HttpMethod.Put, $"/api/sessions/{code}/content/sharing", hostToken, new { playersMayDownload = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SnapshotAsync(code, avaToken)).GetProperty("game").GetProperty("content").GetProperty("playersMayDownload").GetBoolean().ShouldBeTrue();
        using (var shared = await download())
        {
            shared.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await shared.Content.ReadAsStringAsync(Ct)).ShouldContain("\"name\": \"Shared sums\"");
        }

        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false })).Dispose();
        using var duringRound = await download();
        duringRound.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Json(duringRound)).GetProperty("detail").GetString().ShouldBe("The set can be downloaded between rounds.");
    }

    [Fact]
    public async Task Not_during_a_round()
    {
        var (code, hostToken) = await NewGameAsync();
        (await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/rounds/countdown", hostToken, new { startInAuto = false })).Dispose();

        using var response = await UseAsync(code, hostToken, Sums());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("""{ "format": 1, "pairs": [ ["too few"] ] }""", "A set needs 24–500 entries, not 1")]
    [InlineData("""{ "not": "a set" }""", "This isn't a SyncBingo set file.")]
    [InlineData("""[1, 2, 3]""", "This isn't a SyncBingo set file.")]
    public async Task A_bad_set_is_refused_with_what_to_fix(string file, string detail)
    {
        var (code, hostToken) = await NewGameAsync();

        using var response = await UseAsync(code, hostToken, file);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("detail").GetString()!.ShouldContain(detail);
    }

    [Fact]
    public async Task A_set_that_doesnt_fit_the_card_by_group_is_refused()
    {
        var (code, hostToken) = await NewGameAsync();

        using var response = await UseAsync(code, hostToken, Sums(), groupItems: true);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("detail").GetString().ShouldBe("Group items needs every entry in a group.");
    }

    [Fact]
    public async Task A_huge_body_is_refused()
    {
        var (code, hostToken) = await NewGameAsync();
        var huge = $$"""{ "format": 1, "pairs": [], "padding": "{{new string('x', 200 * 1024)}}" }""";

        using var response = await UseAsync(code, hostToken, huge);

        // Kestrel stops it at the endpoint's size limit (413); the in-memory test server doesn't enforce
        // that limit, so here the set file's own 64 KB check refuses it.
        if (response.StatusCode != HttpStatusCode.RequestEntityTooLarge)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await Json(response)).GetProperty("detail").GetString().ShouldBe("A set file can be at most 64 KB.");
        }
    }

    private async Task<HttpResponseMessage> UseAsync(string code, string token, string file, bool groupItems = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/sessions/{code}/content?groupItems={groupItems}", UriKind.Relative))
        {
            Content = new StringContent(file, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<(string Code, string HostToken)> NewGameAsync()
    {
        using var response = await _client.SendAsync(HttpMethod.Post, "/api/sessions", body: new { password = "secret" });
        var body = await Json(response);
        return (body.GetProperty("code").GetString()!.Replace("-", "", StringComparison.Ordinal), body.GetProperty("hostToken").GetString()!);
    }

    private async Task<(string Id, string Token)> JoinWithIdAsync(string code, string name)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "secret", name });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        return (body.GetProperty("participantId").GetString()!, body.GetProperty("token").GetString()!);
    }

    private async Task<string> JoinAsync(string code, string name)
    {
        using var response = await _client.SendAsync(HttpMethod.Post, $"/api/sessions/{code}/join", body: new { password = "secret", name });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await Json(response)).GetProperty("token").GetString()!;
    }

    private async Task<JsonElement> SnapshotAsync(string code, string token)
    {
        using var response = await _client.SendAsync(HttpMethod.Get, $"/api/sessions/{code}/snapshot", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await Json(response);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
}
